"""trade.db 読み書き（C#↔Python の唯一の境界）。

実テーブル名は EF Core 規約で複数形（Signals/DailyBars/FinSummaries）。
エンティティ名は単数だが SQLite テーブルは複数形なので ``FROM Signal`` は no such table で失敗する。
列名は C# プロパティ名そのまま（PascalCase）。DateOnly は TEXT 形式 yyyy-MM-dd、bool は INTEGER(0/1)。

書き戻し（Signals.MlScore）は **C# プロセス完全終了後** に手動バッチで実行する前提
（段階2は逐次手動運用なので SQLite 書き込みロックは衝突しない）。万一の locked は busy_timeout で吸収。
"""
from __future__ import annotations

import sqlite3
from contextlib import contextmanager

import pandas as pd


@contextmanager
def connect(db_path: str):
    """busy_timeout を設定した接続をコンテキストで返す。"""
    conn = sqlite3.connect(db_path, timeout=10.0)
    try:
        conn.execute("PRAGMA busy_timeout=5000")
        yield conn
    finally:
        conn.close()


def _parse_dates(df: pd.DataFrame, cols: list[str]) -> pd.DataFrame:
    """TEXT(yyyy-MM-dd) 列を datetime64 に変換する（点-in-time 比較用）。"""
    for c in cols:
        if c in df.columns:
            df[c] = pd.to_datetime(df[c], format="%Y-%m-%d")
    return df


def read_signals(conn: sqlite3.Connection, passed_only: bool = True) -> pd.DataFrame:
    """Signals を読む。passed_only=True で Passed=1（スコアリング対象母集団）に絞る。"""
    where = "WHERE Passed = 1" if passed_only else ""
    df = pd.read_sql(
        f"SELECT Date, Code, Passed, RuleScore, MlScore FROM Signals {where}", conn
    )
    return _parse_dates(df, ["Date"])


def read_daily_bars(conn: sqlite3.Connection) -> pd.DataFrame:
    """DailyBars 全件（調整後 Adj* を特徴量・ラベルに使う）。"""
    df = pd.read_sql(
        "SELECT Code, Date, AdjOpen, AdjHigh, AdjLow, AdjClose, AdjVolume "
        "FROM DailyBars",
        conn,
    )
    df = _parse_dates(df, ["Date"])
    return df.sort_values(["Code", "Date"]).reset_index(drop=True)


def read_fin_summaries(conn: sqlite3.Connection) -> pd.DataFrame:
    """FinSummaries 全件（DiscloseDate で as-of マージ）。"""
    df = pd.read_sql(
        "SELECT Code, DiscloseDate, DocType, Eps, Bps, Equity, TotalAssets "
        "FROM FinSummaries",
        conn,
    )
    df = _parse_dates(df, ["DiscloseDate"])
    return df.sort_values(["Code", "DiscloseDate"]).reset_index(drop=True)


def write_mlscores(conn: sqlite3.Connection, scores: pd.DataFrame) -> int:
    """Signals.MlScore を (Date, Code) で UPDATE。全件を単一トランザクションで一括反映。

    scores: 列 Date(datetime/date), Code(str), MlScore(float)。
    戻り値: 更新行数。
    """
    rows = [
        (float(r.MlScore), pd.Timestamp(r.Date).strftime("%Y-%m-%d"), str(r.Code))
        for r in scores.itertuples(index=False)
    ]
    cur = conn.cursor()
    cur.execute("BEGIN")
    try:
        cur.executemany(
            "UPDATE Signals SET MlScore = ? WHERE Date = ? AND Code = ?", rows
        )
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    return cur.rowcount
