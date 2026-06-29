"""段階3a 当日 EOD 推論。最新 FULL モデルで指定日（既定=DB 最新営業日）の Passed universe を採点し
Signal.MlScore へ書き戻す。

実行:
  uv run python predict.py --db ../TradeAnalyzer.Worker/trade.db --date 2025-06-27
  uv run python predict.py --db ../TradeAnalyzer.Worker/trade.db          # --date 省略=DB 最新営業日

設計の要点:
  - train.py の load_dataset/features をそのまま通すため train-serve skew は定義上ゼロ。
  - 先読みなし: モデルは train-end<=採点日前日 の学習、特徴量は Date<=t の点-in-time（features.py 規約）。
  - 書戻し契約: 当日 Passed 全行の MlScore を write_mlscores(expect=件数) で UPDATE。NaN/件数不一致は
    SystemExit（silent fallback 禁止）。これにより C# run-today の null 検査が当日経路でも止まらない。
"""
from __future__ import annotations

import argparse
import glob
import os

import lightgbm as lgb
import numpy as np
import pandas as pd

import db as dbmod
import features as featmod
import labels as labmod
from train import MODELS_DIR, load_dataset, _slice


def _resolve_latest_model() -> str:
    """models/lambdarank_FULL_*.txt をファイル名（<train-end> が固定長 YYYYMMDD）降順で最新に解決する。

    最新 FULL モデルが無ければ silent fallback せず即 SystemExit（採点根拠のモデルを曖昧にしない）。
    """
    paths = glob.glob(os.path.join(MODELS_DIR, "lambdarank_FULL_*.txt"))
    if not paths:
        raise SystemExit(
            f"FULL モデルが {MODELS_DIR} にありません。先に学習してください:\n"
            "  uv run python train.py --full --db <trade.db> --train-end <YYYY-MM-DD> --retune"
        )
    # <train-end> は固定長 8 桁（%Y%m%d）なので辞書順=時系列順＝降順先頭が最新。
    return sorted(paths, reverse=True)[0]


def _resolve_target_date(db_path: str) -> pd.Timestamp:
    """DB 内 DailyBar.Date の最大値（＝最新 EOD が入っている営業日）を採点対象日に解決する。

    DailyBar.Date は TEXT(yyyy-MM-dd) 格納で MAX は辞書順=時系列順＝正しい最新日。取得文字列は
    pd.Timestamp に変換して _slice（df["Date"] は datetime64 比較）へ渡す。load_dataset とは独立に
    軽量クエリで先に解決する（load_dataset は --date フィルタを持たず全期間を読むため）。
    """
    with dbmod.connect(db_path) as conn:
        row = conn.execute("SELECT MAX(Date) FROM DailyBars").fetchone()
    if row is None or row[0] is None:
        raise SystemExit("DailyBars が空です。先に ingest を実行してください（採点対象日を解決できません）。")
    return pd.Timestamp(row[0])


def main() -> None:
    ap = argparse.ArgumentParser(description="段階3a 当日 EOD 推論（最新 FULL モデルで Passed を採点→MlScore 書戻し）")
    ap.add_argument("--db", required=True, help="trade.db のパス（絶対パス推奨。C# run-today は絶対パスを渡す）")
    ap.add_argument("--date", dest="date", default=None, help="採点対象日（YYYY-MM-DD）。省略時は DB 最新営業日。")
    ap.add_argument("--model", dest="model", default=None, help="モデルパス。省略時は最新 FULL を自動解決。")
    ap.add_argument("--horizon", type=int, default=labmod.DEFAULT_HORIZON,
                    help="特徴量/ラベルのホライズン（既定=20。load_dataset の引数に渡すのみで採点はラベル不要）")
    args = ap.parse_args()

    model_path = args.model or _resolve_latest_model()
    t = pd.Timestamp(args.date) if args.date else _resolve_target_date(args.db)
    print(f"採点対象日 t={t:%Y-%m-%d}, モデル={os.path.basename(model_path)}")

    fcols = featmod.feature_columns()
    # load_dataset は全期間を組み立てる（--date フィルタなし）が、正規化は同一 Date 内に閉じるため
    # 当日抽出後も skew ゼロ。全期間ロードは日次バッチで許容するコスト（段階2 train と同規模）。
    df = load_dataset(args.db, args.horizon)
    day = _slice(df, t, t)
    if day.empty:
        raise SystemExit(
            f"{t:%Y-%m-%d} の Passed 行がありません。analyze（run-today step3）実行済みか、"
            "採点対象日が正しいかを確認してください。"
        )

    booster = lgb.Booster(model_file=model_path)
    # ラベル不要・特徴量のみで採点（train.py の OOS スコアリングと同じ呼出）。
    scored = day[["Date", "Code"]].copy()
    scored["MlScore"] = booster.predict(day[fcols].to_numpy())

    # 回帰ガード: 全 Passed 行にスコアが付いたこと（NaN は C# null 検査で run-today を止める）。
    n_null = int(np.isnan(scored["MlScore"].to_numpy()).sum())
    if n_null:
        raise SystemExit(
            f"採点に NaN が {n_null} 件＝特徴量欠損の疑い。C# null 検査で run-today が停止します。"
        )

    with dbmod.connect(args.db) as conn:
        # expect=len(scored): day の (Date,Code) は一意（load_dataset の回帰ガードが signals 重複を弾く）。
        # 不一致なら write_mlscores が rollback して ValueError（書戻し漏れを原子的に検出）。
        updated = dbmod.write_mlscores(conn, scored, expect=len(scored))
    print(f"Signal.MlScore 書き戻し: {updated} 行（{t:%Y-%m-%d} の Passed）")


if __name__ == "__main__":
    main()
