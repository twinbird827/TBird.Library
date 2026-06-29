"""ウォークフォワード学習（LambdaRank）＋optuna＋Purged K-Fold ＋ OOS スコア書き戻し。

実行（ウォークフォワード A/B 用）:
  uv run python train.py --db ../TradeAnalyzer.Worker/trade.db --is 2024 --oos 2025

実行（段階3a 当日運用の再学習＝全確定ラベルで1モデル）:
  uv run python train.py --db ../TradeAnalyzer.Worker/trade.db --full --train-end 2025-06-27 [--retune]

ワークフロー（境界は trade.db のみ・プロセス連携なし）:
  1. signals が保存した Passed 母集団（IS/OOS）を読む。
  2. features.py / labels.py で点-in-time 特徴量・フォワード相対ラベルを生成。
  3. IS の確定ラベル行で optuna（Purged K-Fold + embargo）→ ハイパラ確定 → 全 IS で最終学習。
  4. OOS の全 Passed 行（ラベル不要）を out-of-sample スコアリングし Signal.MlScore に書き戻し。
     ※ ラベル未確定の OOS 末尾（直近約 H 日）の Passed 行にも MlScore を必ず書く
       （ラベル除外は学習専用。書かないと C# の null 検査で backtest が止まる）。

OOS スコアは「その区間より前の IS で学習したモデル」の出力＝先読みなし。

--full 経路（段階3a）: IS/OOS 窓に切らず `Date <= train-end` の全確定ラベル行で `lgb.train` を1回呼び、
  models/lambdarank_FULL_<train-end>.txt を保存する（OOS スコア書戻しはしない＝当日採点は predict.py の責務）。
  ハイパラは既定で models/best_params.json を読む（初回は --retune で seed → 以降の --full はこれを読む）。
  --retune 指定時のみ optuna を再探索し best_params.json を更新する（週次再学習で毎回 optuna は重く不安定なため）。
"""
from __future__ import annotations

import argparse
import json
import os
from datetime import date

import lightgbm as lgb
import numpy as np
import optuna
import pandas as pd

import db as dbmod
import features as featmod
import labels as labmod

optuna.logging.set_verbosity(optuna.logging.WARNING)

MODELS_DIR = os.path.join(os.path.dirname(__file__), "models")
# --full のハイパラ永続化先（--retune が書き、以降の --full が読む）。
BEST_PARAMS_PATH = os.path.join(MODELS_DIR, "best_params.json")


# --------- 期間パース（C# RequireRange と同形式: YYYY または YYYY-MM-DD:YYYY-MM-DD）---------
def parse_window(s: str) -> tuple[pd.Timestamp, pd.Timestamp]:
    if ":" in s:
        a, b = s.split(":", 1)
        return pd.Timestamp(a), pd.Timestamp(b)
    year = int(s)
    return pd.Timestamp(date(year, 1, 1)), pd.Timestamp(date(year, 12, 31))


# --------- データ組み立て（train/evaluate 共通）---------
def load_dataset(db_path: str, horizon: int) -> pd.DataFrame:
    """Passed 母集団に特徴量・ラベルを結合した1枚のフレームを返す。

    列: Date, Code, RuleScore, {feature cols}, FwdReturn, LabelGain, EntryFeasible, LabelConfirmed。
    """
    with dbmod.connect(db_path) as conn:
        signals = dbmod.read_signals(conn, passed_only=True)
        bars = dbmod.read_daily_bars(conn)
        fin = dbmod.read_fin_summaries(conn)

    if signals.empty:
        raise SystemExit(
            "Signals(Passed) が空です。先に C# の `signals --is <期間> --oos <期間>` を実行してください。"
        )

    feats = featmod.build_features(bars, fin, signals)
    labs = labmod.build_labels(bars, signals, horizon=horizon)

    df = (
        signals[["Date", "Code", "RuleScore", "MlScore"]]
        .merge(feats, on=["Date", "Code"], how="inner")
        .merge(labs, on=["Date", "Code"], how="left")
    )

    # 回帰ガード: 特徴量結合(inner)で Passed 行が欠落していないこと。落ちると Python は
    # その行に MlScore を書けず、C# ML backtest が Passed かつ MlScore=null で停止する。
    n_universe = signals[["Date", "Code"]].drop_duplicates().shape[0]
    if len(df) != n_universe:
        raise SystemExit(
            f"特徴量結合で Passed 行が欠落: signals(unique)={n_universe} != joined={len(df)}。"
            "features.build_features の as-of 結合が universe を完全被覆しているか確認してください。"
        )
    return df.sort_values(["Date", "Code"]).reset_index(drop=True)


def _group_sizes(df: pd.DataFrame) -> list[int]:
    """LightGBM group（同一 Date の連続行数）。df は Date 昇順前提。"""
    return df.groupby("Date", sort=True).size().tolist()


def _slice(df: pd.DataFrame, start: pd.Timestamp, end: pd.Timestamp) -> pd.DataFrame:
    return df[(df["Date"] >= start) & (df["Date"] <= end)].sort_values(["Date", "Code"]).reset_index(drop=True)


# --------- Purged K-Fold（embargo = H 取引日を「取引日距離の日付窓」で除外）---------
def _purged_folds(
    dates: np.ndarray, k: int, all_trading_days: np.ndarray, horizon: int
) -> list[tuple[np.ndarray, np.ndarray]]:
    """ユニークなリバランス日を K 連続ブロックに分割し、検証ブロック前後 horizon **取引日** を学習から除外。

    embargo はリバランス間隔（位置）でなく全取引日カレンダー上の取引日距離で計算する。各 val ブロック
    [val_start, val_end] の手前/後ろ horizon 取引日にあたる日付窓を `all_trading_days` から逆引きし、
    その窓に入るリバランス日を学習から落とす。これにより:
      - embargo の単位が「取引日」で正確（リバランス位置×間隔の換算ミスで過剰/過小パージしない）、
      - IS 末尾の未確定ラベル日が間引かれてリバランス間隔が不均一でもリーク防止が崩れない。

    返り値: [(train_dates, val_dates), ...]。日付集合（np.datetime64）で返す。
    """
    uniq = np.array(sorted(pd.unique(dates)))
    m = len(uniq)
    folds: list[tuple[np.ndarray, np.ndarray]] = []
    if m < k or len(all_trading_days) == 0:
        return folds
    n_cal = len(all_trading_days)
    bounds = np.linspace(0, m, k + 1).astype(int)
    for i in range(k):
        a, b = bounds[i], bounds[i + 1]  # 検証ブロック位置 [a, b)
        if b <= a:
            continue
        val = uniq[a:b]
        val_start = uniq[a]
        val_end = uniq[b - 1]
        # val_start の horizon 取引日前 / val_end の horizon 取引日後の日付（取引日距離で embargo）。
        i0 = int(np.searchsorted(all_trading_days, val_start))
        lo_date = all_trading_days[max(0, i0 - horizon)]
        i1 = int(np.searchsorted(all_trading_days, val_end, side="right")) - 1
        hi_date = all_trading_days[min(n_cal - 1, i1 + horizon)]
        # 学習に残すリバランス日 = [lo_date, hi_date]（val＋前後 horizon 窓）の外側。
        train = uniq[(uniq < lo_date) | (uniq > hi_date)]
        if len(train) == 0:
            continue
        folds.append((train, val))
    return folds


def _make_dataset(df: pd.DataFrame, fcols: list[str]) -> lgb.Dataset:
    df = df.sort_values(["Date", "Code"]).reset_index(drop=True)
    return lgb.Dataset(
        df[fcols].to_numpy(),
        label=df["LabelGain"].to_numpy(),
        group=_group_sizes(df),
        free_raw_data=False,
    )


def _objective(trial: optuna.Trial, train_df: pd.DataFrame, fcols: list[str],
               folds: list[tuple[np.ndarray, np.ndarray]], eval_k: int):
    # fold は train_df["Date"]/all_trading_days/horizon のみ依存で trial 不変のため main で1回 precompute し受け取る
    # （trial ごとの再計算を避け、_objective を CV 分割の生成から切り離す）。
    # netkeiba 確定値（leaves≈15-20, depth≈6-7, lr≈0.02）を中心レンジに（母数が違うので鵜呑みにしない）。
    params = {
        "objective": "lambdarank",
        "metric": "ndcg",
        "ndcg_eval_at": [eval_k],
        "verbosity": -1,
        "num_leaves": trial.suggest_int("num_leaves", 8, 31),
        "max_depth": trial.suggest_int("max_depth", 4, 8),
        "learning_rate": trial.suggest_float("learning_rate", 0.01, 0.1, log=True),
        "feature_fraction": trial.suggest_float("feature_fraction", 0.5, 1.0),
        "bagging_fraction": trial.suggest_float("bagging_fraction", 0.6, 1.0),
        "bagging_freq": 1,
        "lambda_l2": trial.suggest_float("lambda_l2", 1.0, 15.0),
        "min_child_samples": trial.suggest_int("min_child_samples", 20, 200),
    }
    scores = []
    for tr_dates, va_dates in folds:
        tr = train_df[train_df["Date"].isin(tr_dates)]
        va = train_df[train_df["Date"].isin(va_dates)]
        if tr.empty or va.empty:
            continue
        dtrain = _make_dataset(tr, fcols)
        dvalid = _make_dataset(va, fcols)
        booster = lgb.train(
            params, dtrain, num_boost_round=2000, valid_sets=[dvalid],
            callbacks=[lgb.early_stopping(150, verbose=False), lgb.log_evaluation(0)],
        )
        best = booster.best_score.get("valid_0", {}).get(f"ndcg@{eval_k}")
        if best is not None:
            scores.append(best)
    if not scores:
        raise optuna.TrialPruned()
    return float(np.mean(scores))


K_FOLDS = 4  # CV 分割数（_purged_folds とエラーメッセージの単一ソース）


def _build_lgb_params(tunable: dict, topk: int) -> dict:
    """optuna が探索する tunable subset に固定の lambdarank 既定を合成した完全パラメータ。

    ウォークフォワード（IS/OOS）と --full の双方が同じ合成規則を使い、両経路のモデル定義を一致させる。
    """
    return {
        "objective": "lambdarank", "metric": "ndcg", "ndcg_eval_at": [topk],
        "verbosity": -1, "bagging_freq": 1, **tunable,
    }


def _tune_hyperparams(
    train_df: pd.DataFrame, fcols: list[str], all_trading_days: np.ndarray,
    horizon: int, topk: int, trials: int,
) -> dict:
    """Purged K-Fold CV を optuna で最大化し、最良の tunable ハイパラ（study.best_params）を返す。

    ウォークフォワードの学習と --full --retune が共有する（fold 構成・F2 ガード・optuna 起動の単一ソース）。
    """
    # fold は trial 不変（train_df["Date"]/all_trading_days/horizon のみ依存）なので1回だけ構成する。
    folds = _purged_folds(train_df["Date"].to_numpy(), K_FOLDS, all_trading_days, horizon)
    # F2: optimize 前に fold 構成可否を確認。全 fold 空だと全 trial が pruned になり、
    # study.best_value 参照時に optuna が ValueError("No trials are completed yet") を投げて不親切に終わる。
    if not folds:
        raise SystemExit(
            f"CV fold を構成できません（リバランス日数 {train_df['Date'].nunique()} / k={K_FOLDS} / "
            f"horizon {horizon} 取引日）。学習期間を広げるか --horizon を見直してください。"
        )
    study = optuna.create_study(direction="maximize")
    study.optimize(lambda t: _objective(t, train_df, fcols, folds, topk),
                   n_trials=trials, show_progress_bar=False)
    print(f"best CV NDCG@{topk}={study.best_value:.4f}, params={study.best_params}")
    return study.best_params


def _save_best_params(tunable: dict) -> None:
    """best_params.json に tunable ハイパラを保存（--retune 経路のみ。--full はこれを読む）。"""
    os.makedirs(MODELS_DIR, exist_ok=True)
    with open(BEST_PARAMS_PATH, "w", encoding="utf-8") as f:
        json.dump(tunable, f, ensure_ascii=False, indent=2)
    print(f"ハイパラ保存: {BEST_PARAMS_PATH}")


def _load_best_params() -> dict:
    """best_params.json を読む。無ければ silent fallback せず原因明示で SystemExit（bootstrap 手順を案内）。"""
    if not os.path.exists(BEST_PARAMS_PATH):
        raise SystemExit(
            f"{BEST_PARAMS_PATH} がありません。初回は --retune を付けて optuna でハイパラを seed してください:\n"
            "  uv run python train.py --full --db <trade.db> --train-end <YYYY-MM-DD> --retune"
        )
    with open(BEST_PARAMS_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


def _run_walkforward(args: argparse.Namespace) -> None:
    """既存のウォークフォワード経路（--is/--oos）。IS で optuna→最終学習し OOS を採点・書戻し。"""
    is_start, is_end = parse_window(args.is_window)
    oos_start, oos_end = parse_window(args.oos_window)
    fcols = featmod.feature_columns()

    df = load_dataset(args.db, args.horizon)
    is_df = _slice(df, is_start, is_end)
    oos_df = _slice(df, oos_start, oos_end)

    # Purged K-Fold の embargo を「取引日距離」で計算するための全取引日カレンダー（1回 precompute）。
    # optuna は _objective を trials 回呼ぶため、ここで in-memory 化して trial/fold ごとの DB 往復を避ける。
    with dbmod.connect(args.db) as conn:
        all_trading_days = np.array(sorted(dbmod.read_daily_bars(conn)["Date"].unique()))

    # 学習母集団＝IS の確定ラベル行（entry 可能 かつ ラベル確定）。
    train_df = is_df[is_df["LabelConfirmed"] & is_df["EntryFeasible"]].copy()
    n_dates = train_df["Date"].nunique()
    print(f"IS rows(Passed)={len(is_df)}, 学習可能行={len(train_df)}（{n_dates} リバランス日）")
    print(f"OOS rows(Passed)={len(oos_df)}（全件スコアリング対象）")

    if len(train_df) == 0 or n_dates < 2:
        raise SystemExit(
            f"学習データ不足（確定ラベル {len(train_df)} 行 / {n_dates} 日）。"
            "ingest 済みの履歴（複数リバランス日ぶんの DailyBar）が必要です。"
        )

    best_params = _build_lgb_params(
        _tune_hyperparams(train_df, fcols, all_trading_days, args.horizon, args.topk, args.trials),
        args.topk,
    )

    # 全 IS で最終学習（num_boost_round は CV の早期停止傾向を踏まえ控えめ固定）。
    dtrain = _make_dataset(train_df, fcols)
    booster = lgb.train(best_params, dtrain, num_boost_round=800, callbacks=[lgb.log_evaluation(0)])

    os.makedirs(MODELS_DIR, exist_ok=True)
    model_path = os.path.join(MODELS_DIR, f"lambdarank_IS{is_start:%Y%m%d}-{is_end:%Y%m%d}.txt")
    booster.save_model(model_path)
    print(f"モデル保存: {model_path}")

    if oos_df.empty:
        print("OOS Passed 行が 0 です（書き戻しスキップ）。")
        return

    # OOS 全 Passed 行を out-of-sample スコアリング（ラベル不要＝特徴量だけで書ける）。
    oos_scored = oos_df[["Date", "Code"]].copy()
    oos_scored["MlScore"] = booster.predict(oos_df[fcols].to_numpy())

    # 回帰ガード: 全 OOS Passed 行にスコアが付いたこと（NaN は C# null 検査で backtest を止める）。
    n_null = int(np.isnan(oos_scored["MlScore"].to_numpy()).sum())
    if n_null:
        raise SystemExit(
            f"OOS スコアに NaN が {n_null} 件＝特徴量欠損の疑い。C# null 検査で backtest が停止します。"
        )

    if args.no_write:
        print("--no-write 指定: MlScore 書き戻しをスキップしました。")
        return

    with dbmod.connect(args.db) as conn:
        # expect=len(oos_scored): oos_scored の (Date,Code) は一意（load_dataset の回帰ガードが
        # signals の重複を弾くため）＝1行1 UPDATE で rowcount は行数と一致する。不一致なら write_mlscores
        # が rollback して ValueError を投げる（書戻し漏れを原子的に検出）。
        updated = dbmod.write_mlscores(conn, oos_scored, expect=len(oos_scored))
    print(f"Signal.MlScore 書き戻し: {updated} 行（OOS 全 Passed）")


def _run_full(args: argparse.Namespace) -> None:
    """段階3a 再学習経路（--full）。train-end までの全確定ラベルで1モデル学習し FULL モデルを保存する。

    IS/OOS 窓は使わず `Date <= train-end` で母集団を切る。OOS スコア書戻しはしない（当日採点は predict.py）。
    """
    train_end = pd.Timestamp(args.train_end)
    fcols = featmod.feature_columns()

    df = load_dataset(args.db, args.horizon)
    # train-end 以前かつラベル確定・entry 可能な行のみ（labels.py が直近 H 日を未確定として除外済み）。
    pop = df[(df["Date"] <= train_end) & df["LabelConfirmed"] & df["EntryFeasible"]].copy()
    n_dates = pop["Date"].nunique()
    print(f"FULL 学習可能行={len(pop)}（{n_dates} リバランス日, train-end={train_end:%Y-%m-%d}）")

    if len(pop) == 0 or n_dates < 2:
        raise SystemExit(
            f"学習データ不足（確定ラベル {len(pop)} 行 / {n_dates} 日）。"
            "ingest 済みの履歴と --train-end の妥当性を確認してください。"
        )

    if args.retune:
        with dbmod.connect(args.db) as conn:
            all_trading_days = np.array(sorted(dbmod.read_daily_bars(conn)["Date"].unique()))
        tunable = _tune_hyperparams(pop, fcols, all_trading_days, args.horizon, args.topk, args.trials)
        _save_best_params(tunable)
    else:
        tunable = _load_best_params()

    params = _build_lgb_params(tunable, args.topk)
    dtrain = _make_dataset(pop, fcols)
    booster = lgb.train(params, dtrain, num_boost_round=800, callbacks=[lgb.log_evaluation(0)])

    os.makedirs(MODELS_DIR, exist_ok=True)
    model_path = os.path.join(MODELS_DIR, f"lambdarank_FULL_{train_end:%Y%m%d}.txt")
    booster.save_model(model_path)
    print(f"FULL モデル保存: {model_path}（旧モデルは手動ロールバック用に残す）")


def main() -> None:
    ap = argparse.ArgumentParser(description="段階2/3a LambdaRank 学習（ウォークフォワード A/B ＋ --full 再学習）")
    ap.add_argument("--db", required=True, help="trade.db のパス")
    # --is/--oos は --full 以外で必須。argparse は「--full 時のみ条件付き」を宣言的に書けないため
    # required=False にし、parse 後に main() の手動検証（ap.error）で条件付き必須を表現する。
    ap.add_argument("--is", dest="is_window", default=None, help="IS 期間（YYYY または FROM:TO。--full 以外で必須）")
    ap.add_argument("--oos", dest="oos_window", default=None, help="OOS 期間（--full 以外で必須）")
    ap.add_argument("--full", action="store_true",
                    help="段階3a 再学習: train-end までの全確定ラベルで1モデル学習（ウォークフォワードなし）")
    ap.add_argument("--train-end", dest="train_end", default=None, help="--full の学習データ終端（YYYY-MM-DD）")
    ap.add_argument("--retune", action="store_true",
                    help="--full 時に optuna 再探索しハイパラ（best_params.json）を更新")
    ap.add_argument("--horizon", type=int, default=labmod.DEFAULT_HORIZON, help="ラベルホライズン（既定=MaxHoldDays=20）")
    ap.add_argument("--trials", type=int, default=40, help="optuna 試行数")
    ap.add_argument("--topk", type=int, default=10, help="NDCG@K の K（既定=TopN=10）")
    ap.add_argument("--no-write", action="store_true", help="MlScore を書き戻さず学習のみ（検証用。--full は元から書戻し無し）")
    args = ap.parse_args()

    if args.full:
        # --full は IS/OOS 窓を使わないため --is/--oos は不要・無視。--train-end のみ条件付き必須。
        if not args.train_end:
            ap.error("--full 指定時は --train-end YYYY-MM-DD が必要です。")
        _run_full(args)
        return

    # 通常のウォークフォワード経路（--full 不在）では --is/--oos を条件付き必須にする。
    if not args.is_window or not args.oos_window:
        ap.error("--is と --oos が必要です（--full を指定しない通常のウォークフォワード経路）。")
    _run_walkforward(args)


if __name__ == "__main__":
    main()
