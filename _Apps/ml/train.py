"""ウォークフォワード学習（LambdaRank）＋optuna＋Purged K-Fold ＋ OOS スコア書き戻し。

実行:
  uv run python train.py --db ../TradeAnalyzer.Worker/trade.db --is 2024 --oos 2025

ワークフロー（境界は trade.db のみ・プロセス連携なし）:
  1. signals が保存した Passed 母集団（IS/OOS）を読む。
  2. features.py / labels.py で点-in-time 特徴量・フォワード相対ラベルを生成。
  3. IS の確定ラベル行で optuna（Purged K-Fold + embargo）→ ハイパラ確定 → 全 IS で最終学習。
  4. OOS の全 Passed 行（ラベル不要）を out-of-sample スコアリングし Signal.MlScore に書き戻し。
     ※ ラベル未確定の OOS 末尾（直近約 H 日）の Passed 行にも MlScore を必ず書く
       （ラベル除外は学習専用。書かないと C# の null 検査で backtest が止まる）。

OOS スコアは「その区間より前の IS で学習したモデル」の出力＝先読みなし。
"""
from __future__ import annotations

import argparse
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


# --------- Purged K-Fold（positional embargo = H 取引日）---------
def _purged_folds(dates: np.ndarray, k: int, embargo: int) -> list[tuple[np.ndarray, np.ndarray]]:
    """ユニーク日付の位置を K 連続ブロックに分割し、検証ブロック前後 embargo 本を学習から除外。

    返り値: [(train_dates, val_dates), ...]。日付集合（np.datetime64）で返す。
    """
    uniq = np.array(sorted(pd.unique(dates)))
    m = len(uniq)
    folds: list[tuple[np.ndarray, np.ndarray]] = []
    if m < k:
        return folds
    bounds = np.linspace(0, m, k + 1).astype(int)
    for i in range(k):
        a, b = bounds[i], bounds[i + 1]  # 検証ブロック位置 [a, b)
        if b <= a:
            continue
        val = uniq[a:b]
        lo = max(0, a - embargo)
        hi = min(m, b + embargo)
        train_pos = np.concatenate([np.arange(0, lo), np.arange(hi, m)])
        if len(train_pos) == 0:
            continue
        folds.append((uniq[train_pos], val))
    return folds


def _make_dataset(df: pd.DataFrame, fcols: list[str]) -> lgb.Dataset:
    df = df.sort_values(["Date", "Code"]).reset_index(drop=True)
    return lgb.Dataset(
        df[fcols].to_numpy(),
        label=df["LabelGain"].to_numpy(),
        group=_group_sizes(df),
        free_raw_data=False,
    )


def _objective(trial: optuna.Trial, train_df: pd.DataFrame, fcols: list[str], embargo: int, eval_k: int):
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
    folds = _purged_folds(train_df["Date"].to_numpy(), k=4, embargo=embargo)
    if not folds:
        raise optuna.TrialPruned()

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


def main() -> None:
    ap = argparse.ArgumentParser(description="段階2 LambdaRank 学習＋OOS スコア書き戻し")
    ap.add_argument("--db", required=True, help="trade.db のパス")
    ap.add_argument("--is", dest="is_window", required=True, help="IS 期間（YYYY または FROM:TO）")
    ap.add_argument("--oos", dest="oos_window", required=True, help="OOS 期間")
    ap.add_argument("--horizon", type=int, default=labmod.DEFAULT_HORIZON, help="ラベルホライズン（既定=MaxHoldDays=20）")
    ap.add_argument("--trials", type=int, default=40, help="optuna 試行数")
    ap.add_argument("--topk", type=int, default=10, help="NDCG@K の K（既定=TopN=10）")
    ap.add_argument("--no-write", action="store_true", help="MlScore を書き戻さず学習のみ（検証用）")
    args = ap.parse_args()

    is_start, is_end = parse_window(args.is_window)
    oos_start, oos_end = parse_window(args.oos_window)
    fcols = featmod.feature_columns()

    df = load_dataset(args.db, args.horizon)
    is_df = _slice(df, is_start, is_end)
    oos_df = _slice(df, oos_start, oos_end)

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

    embargo = args.horizon
    study = optuna.create_study(direction="maximize")
    study.optimize(lambda t: _objective(t, train_df, fcols, embargo, args.topk),
                   n_trials=args.trials, show_progress_bar=False)
    best_params = {
        "objective": "lambdarank", "metric": "ndcg", "ndcg_eval_at": [args.topk],
        "verbosity": -1, "bagging_freq": 1, **study.best_params,
    }
    print(f"best CV NDCG@{args.topk}={study.best_value:.4f}, params={study.best_params}")

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
        updated = dbmod.write_mlscores(conn, oos_scored)
    print(f"Signal.MlScore 書き戻し: {updated} 行（OOS 全 Passed）")


if __name__ == "__main__":
    main()
