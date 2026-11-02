"""フォワードテスト。run-today が毎晩、採点対象日 t に立ったトリガーを時刻つきで ForwardSignals に記録し、手動の --evaluate で実績を測る。
"""
from __future__ import annotations

import argparse
import sqlite3
import warnings
from datetime import datetime, timezone

import numpy as np
import pandas as pd

import db as dbmod
import explore_triggers as et
import triggers

NAMES = ["C3 割安×上方修正", "C6 業種内割安×上方修正"]
TARGETS = {n: (triggers.TRIGGERS | triggers.COMBO_TRIGGERS)[n] for n in NAMES}
HOLD = 5
# 指数連動 ETF の DailyBars の 5 桁コード。
INDEXES = {"1321": "13210", "1306": "13060"}
JST = "Asia/Tokyo"
# EF Core が SQLite の DateTime を TEXT で持つ形式に合わせる。
TIME_FORMAT = "%Y-%m-%d %H:%M:%S.%f"


def hits(p: triggers.DailyPanel, t: pd.Timestamp) -> dict[str, list[str]]:
    """t に立ち、t に母集団の条件を満たす銘柄コード（トリガーごと）。"""
    elig = p.eligible.loc[t].to_numpy(bool)
    return {n: p.codes[f(p).loc[t].to_numpy(bool) & elig].tolist() for n, f in TARGETS.items()}


def record(conn: sqlite3.Connection, t: pd.Timestamp, found: dict[str, list[str]], now: datetime) -> int:
    """found を ForwardSignals に既存の行を上書きせずに挿入し、挿入した行数を返す。now は UTC。"""
    rows = [(f"{t:%Y-%m-%d}", c, n, now.strftime(TIME_FORMAT)) for n, cs in found.items() for c in cs]
    before = conn.total_changes
    with conn:
        conn.executemany("INSERT OR IGNORE INTO ForwardSignals (Date, Code, Trigger, RecordedAt) VALUES (?, ?, ?, ?)", rows)
    return conn.total_changes - before


def usable(rows: pd.DataFrame, dates: pd.DatetimeIndex) -> pd.DataFrame:
    """記録時刻（UTC）が t の翌営業日の 9:00 JST より前の行。翌営業日が DB に無い行は落とす。"""
    i = dates.searchsorted(rows["Date"].to_numpy(), "right")
    ok = i < len(dates)
    nxt = pd.DatetimeIndex(dates[np.minimum(i, len(dates) - 1)])
    cutoff = (nxt + pd.Timedelta(hours=9)).tz_localize(JST).tz_convert("UTC").tz_localize(None)
    return rows[ok & (rows["RecordedAt"].to_numpy() < cutoff.to_numpy())]


def index_excess(tr: pd.DataFrame, m: et.Market, o: np.ndarray, c: np.ndarray) -> pd.DataFrame:
    """取引の ex を、同じ取引の t+1 の始値 → 手仕舞った日の終値で指数（始値 o・終値 c）を持った場合に対する超過（コスト込み）に差し替える。"""
    t, x = tr["t"].to_numpy(), tr["exit"].to_numpy()
    return tr.assign(ex=tr["ex"].to_numpy() + m.bench[t, x - t] - (c[x] / o[t + 1] - 1))


def read_indexes(conn: sqlite3.Connection, dates: pd.DatetimeIndex) -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """INDEXES の調整済みの始値・終値（dates に揃える）。DailyPanel は ETF を読まないので別に読む。"""
    q = pd.read_sql(f"SELECT Code, Date, AdjOpen, AdjClose FROM DailyBars WHERE Code IN ({','.join('?' * len(INDEXES))})",
                    conn, params=list(INDEXES.values()), parse_dates={"Date": "%Y-%m-%d"})
    out = {}
    for name, code in INDEXES.items():
        d = q[q["Code"] == code].set_index("Date").reindex(dates)
        out[name] = (d["AdjOpen"].to_numpy(float), d["AdjClose"].to_numpy(float))
    return out


def summary(tr: pd.DataFrame, m: et.Market, idx: dict[str, tuple[np.ndarray, np.ndarray]]) -> dict:
    """件数・月数・月平均の超過・Newey-West の t 値と、指数に対する超過の月平均と t 値。"""
    none, lag = pd.Series(dtype=object), et.nw_lag(HOLD)
    row = et.summarize(tr, m, none, lag)[0]
    out = {k: row[k] for k in ("件数", "月数", "超過", "t")}
    for name, (o, c) in idx.items():
        r = et.summarize(index_excess(tr, m, o, c), m, none, lag)[0]
        out |= {f"超過_{name}": r["超過"], f"t_{name}": r["t"]}
    return out


def evaluate(conn: sqlite3.Connection) -> pd.DataFrame:
    p = triggers.DailyPanel(conn)
    rows = pd.read_sql("SELECT Date, Code, Trigger, RecordedAt FROM ForwardSignals", conn,
                       parse_dates={"Date": "%Y-%m-%d", "RecordedAt": "ISO8601"})
    rows = usable(rows, p.dates)
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        m = et.Market(p)
    idx = read_indexes(conn, m.dates)
    sig = {}
    for n in NAMES:
        r = rows[rows["Trigger"] == n]
        i, j = p.dates.get_indexer(r["Date"]), p.codes.get_indexer(r["Code"])
        ok = (i >= 0) & (j >= 0)
        s = np.zeros((m.n, len(p.codes)), bool)
        s[i[ok], j[ok]] = True
        sig[n] = s
    sig["OR"] = np.logical_or.reduce(list(sig.values()))
    out = []
    for n, s in sig.items():
        tr = et.simulate(m, [et.Leg(s, HOLD, n not in triggers.REVERSAL)])
        out.append({"trigger": n, "hold": HOLD} | summary(tr, m, idx))
    return pd.DataFrame(out)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    ap.add_argument("--date", help="採点対象日 t（YYYY-MM-DD）。記録のときだけ必須")
    ap.add_argument("--evaluate", action="store_true", help="書かずに評価する")
    args = ap.parse_args()
    if args.evaluate == (args.date is not None):
        ap.error("--date は記録のときだけ必須で、--evaluate とは併用しない")

    with dbmod.connect(args.db) as conn:
        if args.evaluate:
            res = evaluate(conn)
            pd.set_option("display.width", 300)
            pd.set_option("display.unicode.east_asian_width", True)
            pct = "{:+.2%}".format
            print(res.to_string(index=False, formatters={c: pct for c in res if c.startswith("超過")}
                                | {c: "{:.2f}".format for c in res if c == "t" or c.startswith("t_")}))
            return
        t = pd.to_datetime(args.date, format="%Y-%m-%d")
        p = triggers.DailyPanel(conn, end=t)
        if t not in p.dates:
            raise SystemExit(f"{t:%Y-%m-%d} の DailyBars がありません。")
        found = hits(p, t)
        inserted = record(conn, t, found, datetime.now(timezone.utc))
    for n, cs in found.items():
        print(f"フォワードテスト {t:%Y-%m-%d} {n}: 判定 {len(cs)} 件")
    print(f"フォワードテスト {t:%Y-%m-%d}: ForwardSignals に {inserted} 行挿入")


if __name__ == "__main__":
    main()
