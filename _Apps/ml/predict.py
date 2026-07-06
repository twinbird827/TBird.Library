"""段階3a 当日 EOD 推論。最新 FULL モデルで指定日（既定=DB 最新営業日）の Passed universe を採点し
Signal.MlScore へ書き戻す。

実行:
  uv run python predict.py --db ../../_Tools/TradeAnalyzer/trade.db --date 2025-06-27
  uv run python predict.py --db ../../_Tools/TradeAnalyzer/trade.db          # --date 省略=DB 最新営業日

設計の要点:
  - train.py の load_one_day/features をそのまま通すため train-serve skew は定義上ゼロ（load_one_day は
    当日 t に絞るが、正規化は同一 Date 内に閉じ各 code は Date<=t 全履歴を読むためスコアはビット等価）。
  - 先読みなし: モデルは train-end<=採点日前日 の学習、特徴量は Date<=t の点-in-time（features.py 規約）。
  - 書戻し契約: 当日 Passed 全行の MlScore を score_and_write(expect=件数) で UPDATE。NaN/件数不一致は
    SystemExit（silent fallback 禁止）。これにより C# run-today の null 検査が当日経路でも止まらない。
"""
from __future__ import annotations

import argparse
import glob
import os

import lightgbm as lgb
import pandas as pd

import db as dbmod
import features as featmod
import labels as labmod
from ml_common import score_and_write
from train import MODELS_DIR, load_one_day


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
    # <train-end> は固定長 8 桁（%Y%m%d）なので辞書順=時系列順＝最大値が最新（PS3: sorted(reverse)[0]→max）。
    return max(paths)


def _parse_target_date(s: str) -> pd.Timestamp:
    """--date（YYYY-MM-DD）を厳格に解釈する。不正値は silent fallback せず SystemExit。

    errors="raise" は ValueError（不正文字列の pandas.errors.ParserError も ValueError 派生）/ TypeError を
    投げ NaT を返さないため NaT チェックは併用しない（_resolve_target_date 群の SystemExit ガードと作法を揃える）。
    """
    try:
        return pd.to_datetime(s, format="%Y-%m-%d", errors="raise")
    except (ValueError, TypeError) as e:
        raise SystemExit(f"--date が不正: {s}（YYYY-MM-DD 形式）") from e


def _resolve_target_date(db_path: str) -> pd.Timestamp:
    """DB 内 DailyBar.Date の最大値（＝最新 EOD が入っている営業日）を採点対象日に解決する。

    DailyBar.Date は TEXT(yyyy-MM-dd) 格納で MAX は辞書順=時系列順＝正しい最新日。取得文字列は
    pd.Timestamp に変換して load_one_day（Date==t で signals を絞る）へ渡す。データ組み立てと独立に
    軽量クエリで先に採点対象日を解決する。
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
                    help="特徴量/ラベルのホライズン（既定=20。load_one_day へ署名整合で渡すのみ＝採点はラベル不要）")
    args = ap.parse_args()

    model_path = args.model or _resolve_latest_model()
    t = _parse_target_date(args.date) if args.date else _resolve_target_date(args.db)
    print(f"採点対象日 t={t:%Y-%m-%d}, モデル={os.path.basename(model_path)}")

    fcols = featmod.feature_columns()
    # 当日 t の Passed universe だけを軽量ロード（load_one_day）。全期間ロードとビット等価（正規化は
    # 同一 Date 内に閉じ、各 code は Date<=t 全履歴で as-of bar の後ろ向き特徴量を計算するため）。
    day = load_one_day(args.db, t, args.horizon)
    if day.empty:
        raise SystemExit(
            f"{t:%Y-%m-%d} の Passed 行がありません。analyze（run-today step3）実行済みか、"
            "採点対象日が正しいかを確認してください。"
        )

    booster = lgb.Booster(model_file=model_path)
    # 採点（ラベル不要・特徴量のみ）→ NaN ガード → 書戻しは train と共通の score_and_write に集約。
    # expect=len(day): day の (Date,Code) は一意（load_one_day の回帰ガードが signals 重複を弾く）。
    with dbmod.connect(args.db) as conn:
        updated = score_and_write(booster, day, fcols, conn, expect=len(day))
    print(f"Signal.MlScore 書き戻し: {updated} 行（{t:%Y-%m-%d} の Passed）")


if __name__ == "__main__":
    main()
