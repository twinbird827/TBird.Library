"""predict/train 共通の採点→NaN ガード→書戻しヘルパ。

predict.py（当日 t）と train.py（OOS 区間）が同一の「scored 生成→NaN SystemExit→write_mlscores」を
複製していたのを1箇所に集約する（train-serve skew をコードレベルでも一致させる）。db.py（C#↔Python の
DB 境界責務）に推論を混ぜず、predict↔train の循環 import も避けるため独立モジュールに置く。
"""
from __future__ import annotations

import sqlite3

import lightgbm as lgb
import numpy as np
import pandas as pd

import db as dbmod


def score_and_write(
    booster: lgb.Booster,
    df: pd.DataFrame,
    fcols: list[str],
    conn: sqlite3.Connection | None,
    *,
    expect: int | None = None,
    write: bool = True,
) -> int:
    """df の各行を booster で採点し、NaN ガードの後に Signals.MlScore へ書き戻す。

    呼出側契約: df は非空（母集団非空は predict.py の day.empty・train.py の signals.empty/oos_df.empty ガードが
    保証）、write=True のとき conn は非 None（ヘルパは防御せず、None なら write_mlscores→None.cursor() で
    AttributeError として fail-loud）。

    手順（write の値に依らず NaN ガードは常に走る）:
      1. scored = df[["Date","Code"]] に MlScore（booster.predict）を付ける。
      2. NaN が1件でもあれば SystemExit（特徴量欠損＝後続 C# null 検査で run-today/backtest が停止する原因を
         Python 側で原因明示。silent fallback 禁止）。
      3. write=True のときのみ write_mlscores(conn, scored, expect=expect or len(scored)) で UPDATE。

    expect: 書戻し期待行数。None なら len(scored)（df の (Date,Code) は load_dataset/load_one_day の
            回帰ガードが一意性を保証するため行数＝1行1 UPDATE）。不一致なら write_mlscores が rollback して
            ValueError（書戻し漏れを原子的に検出）。
    戻り値: write=True なら更新行数、write=False なら採点行数（len(scored)）。
    """
    scored = df[["Date", "Code"]].copy()
    scored["MlScore"] = booster.predict(df[fcols].to_numpy())

    # 回帰ガード: 全行にスコアが付いたこと。NaN は C# null 検査で run-today/backtest を止める。
    n_null = int(np.isnan(scored["MlScore"].to_numpy()).sum())
    if n_null:
        raise SystemExit(
            f"採点に NaN が {n_null} 件＝特徴量欠損の疑い。C# null 検査で停止します。"
        )

    if not write:
        return len(scored)

    return dbmod.write_mlscores(
        conn, scored, expect=expect if expect is not None else len(scored)
    )
