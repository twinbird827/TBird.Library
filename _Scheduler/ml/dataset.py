"""総当たり（scan.py）と LightGBM が共有する、銘柄 × 形成日ごとの指標と先の超過リターンの表。

形成日は毎週の最終営業日、行はその日に母集団の条件（triggers.DailyPanel.eligible）を満たす銘柄。指標は t の引け後に分かる情報だけで作る。
ex_<h> は t の翌営業日の始値 → t+h の終値の、t の母集団の等金額平均に対する超過リターンから往復コストを引いた値。
封印期間（SEAL_END より後）の価格は読まない。境界は引数で変えられない定数にして、開けるのは封印期間を確かめるプランだけにする。
"""
from __future__ import annotations

import hashlib
import pickle
import sqlite3
import warnings
from pathlib import Path

import numpy as np
import pandas as pd

import explore
import explore_triggers as et
import rules
import triggers

SEAL_END = pd.Timestamp("2022-12-30")
HOLDS = [5, 20, 60]
COST = et.COST
PRICE = ["ret1", "ret5", "ret20", "ret60", "ret120", "ret250", "hi250", "lo250", "ma25", "ma75",
         "sd20", "sd60", "mx20", "vs", "tv20", "ami20"]
FIN = ["pbr", "per", "psr", "roe", "roa_op", "eq_ratio", "sales_yoy", "op_yoy", "margin_chg", "sue", "agr",
       "progress_gap", "days_since", "mcap"]
FEATURES = PRICE + FIN + ["rev_chg", "credit", "long_turn", "long_turn_chg"]
# 作り直しの要否を判定するため、表の中身を決めるモジュール。
_SOURCES = ["dataset.py", "triggers.py", "rules.py", "db.py", "explore.py", "explore_triggers.py"]


def formation_dates(dates: pd.DatetimeIndex) -> pd.DatetimeIndex:
    """毎週の最終営業日。"""
    return pd.DatetimeIndex(dates.to_series().groupby(dates.to_period("W")).max().to_numpy())


def months(dates: pd.DatetimeIndex) -> pd.Series:
    """総当たりに使う形成月 → D1/D2（端数は D2）。dates は DailyPanel の営業日。

    最古月から explore.FIRST_FORMATION か月後の月から、月の最終営業日 + max(HOLDS) 営業日が dates に収まる最後の月まで。
    """
    last = pd.Series(np.arange(len(dates)), index=dates.to_period("M")).groupby(level=0).max()
    ms = last.index[explore.FIRST_FORMATION:]
    ms = ms[last.loc[ms].to_numpy() + max(HOLDS) <= len(dates) - 1]
    b = len(ms) // 2
    return pd.Series(["D1"] * b + ["D2"] * (len(ms) - b), index=ms)


def forward_excess(m: et.Market, t: np.ndarray, j: np.ndarray) -> dict[int, np.ndarray]:
    """行 (t, j) の h ごとの ex。t+1 の始値か t+h の終値が無い行、t+h が最終日を越える行は欠損。"""
    with np.errstate(all="ignore"):
        entry = np.where(t + 1 < m.n, m.O[np.minimum(t + 1, m.n - 1), j], np.nan)
        out = {}
        for h in HOLDS:
            e = t + h
            px = np.where(e < m.n, m.C[np.minimum(e, m.n - 1), j], np.nan)
            out[h] = px / entry - 1 - m.bench[t, h] - COST
    return out


def _price(p: triggers.DailyPanel, fd: pd.DatetimeIndex) -> dict[str, pd.DataFrame]:
    px, tv = p.px, p.turnover
    ret = px.pct_change(fill_method=None).clip(-0.5, 1)
    w = px.rolling(250, min_periods=200)
    v60 = p.vol.rolling(60, min_periods=50).mean()
    f = {
        **{f"ret{n}": lambda n=n: px / px.shift(n) - 1 for n in (1, 5, 20, 60, 120, 250)},
        "hi250": lambda: px / w.max(),
        "lo250": lambda: px / w.min(),
        "ma25": lambda: px / px.rolling(25, min_periods=20).mean(),
        "ma75": lambda: px / px.rolling(75, min_periods=60).mean(),
        "sd20": lambda: ret.rolling(20, min_periods=15).std(),
        "sd60": lambda: ret.rolling(60, min_periods=50).std(),
        "mx20": lambda: ret.rolling(20, min_periods=15).max(),
        "vs": lambda: p.vol.rolling(5, min_periods=4).mean() / v60.where(v60 > 0),
        "tv20": lambda: tv.rolling(20, min_periods=15).mean(),
        "ami20": lambda: (ret.abs() / tv.where(tv > 0)).rolling(20, min_periods=15).mean(),
    }
    # 全営業日の表は 1 本ずつ作って形成日だけ残す（全部を同時に持つとメモリが足りない）。
    return {k: g().loc[fd] for k, g in f.items()}


def _fin(p: triggers.DailyPanel, fd: pd.DatetimeIndex) -> dict[str, pd.DataFrame]:
    f = p.fin.reset_index(drop=True)
    pos = triggers._pos
    # 最新の決算短信の行番号を引き、その行の値を使う（_daily_asof を値に直接使うと、欠損のとき古い開示の値を持ち越す）。
    rid = triggers._daily_asof(p, f.assign(rid=np.arange(len(f), dtype=float)), "rid").loc[fd].to_numpy()
    ok = ~np.isnan(rid)
    ri = np.where(ok, rid, 0).astype(int)

    def latest(v: pd.Series) -> np.ndarray:
        return np.where(ok, v.to_numpy(float)[ri], np.nan)

    mcap = triggers._mcap(p).loc[fd].to_numpy()
    bps = triggers._daily_asof(p, triggers._fy(p), "Bps").loc[fd]
    di = p.dates.searchsorted(f["DiscloseDate"].to_numpy(), "left")
    with np.errstate(all="ignore"):
        out = {
            "pbr": (p.px.loc[fd] / bps.where(bps > 0)).to_numpy(),
            "per": mcap / latest(pos(f["NP_ttm"])),
            "psr": mcap / latest(pos(f["Sales_ttm"])),
            "roe": latest(f["roe"]),
            "roa_op": latest(f["roa_op"]),
            "eq_ratio": latest(f["Equity"] / pos(f["TA"])),
            "sales_yoy": latest(f["Sales"] / pos(f["Sales_py"]) - 1),
            "op_yoy": latest(f["OP"] / pos(f["OP_py"]) - 1),
            "margin_chg": latest(f["OP"] / pos(f["Sales"]) - f["OP_py"] / pos(f["Sales_py"])),
            "sue": latest(f["sue"]),
            "agr": latest(f["agr"]),
            "progress_gap": latest(triggers._progress_gap(f)),
            "days_since": np.where(ok, p.dates.get_indexer(fd)[:, None] - di[ri], np.nan),
            "mcap": mcap,
        }
    return {k: pd.DataFrame(v, index=fd, columns=p.codes) for k, v in out.items()}


def _revision(p: triggers.DailyPanel, fd: pd.DatetimeIndex) -> dict[str, pd.DataFrame]:
    r = triggers.revisions(p)
    r = r.assign(di=p.dates.searchsorted(r["DiscloseDate"].to_numpy(), "left").astype(float))
    chg = triggers._daily_asof(p, r, "chg").loc[fd]
    age = p.dates.get_indexer(fd)[:, None] - triggers._daily_asof(p, r, "di").loc[fd].to_numpy()
    return {"rev_chg": chg.where(age <= 59)}


def _margin(p: triggers.DailyPanel, fd: pd.DatetimeIndex) -> dict[str, pd.DataFrame]:
    names = ["credit", "long_turn", "long_turn_chg"]
    if p.margin is None:
        return {k: pd.DataFrame(np.nan, index=fd, columns=p.codes) for k in names}
    mi = triggers._margin(p)
    mi["credit"] = mi["LongVolume"] / mi["ShortVolume"].where(mi["ShortVolume"] > 0)
    lt = rules.margin_asof(mi, "lt", fd, p.codes)
    prev = rules.margin_asof(mi, "lt", fd - pd.Timedelta(days=28), p.codes)
    return dict(zip(names, [rules.margin_asof(mi, "credit", fd, p.codes), lt, lt - prev.to_numpy()]))


def build(conn: sqlite3.Connection, end: pd.Timestamp) -> pd.DataFrame:
    """行が (形成日 date, 銘柄 code)（date・code の順に並ぶ）、列が業種 sector・FEATURES・ex_<h> の表。

    attrs["dates"] に DailyPanel の営業日を持つ（months に渡す）。
    """
    p = triggers.DailyPanel(conn, end)
    fd = formation_dates(p.dates)
    feats = {**_price(p, fd), **_fin(p, fd), **_revision(p, fd), **_margin(p, fd)}
    assert list(feats) == FEATURES
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        m = et.Market(p)
    t, j = np.nonzero(p.eligible.loc[fd].to_numpy(bool))
    out = {"date": fd[t], "code": p.codes[j], "sector": p.sec.loc[fd].to_numpy()[t, j]}
    out |= {k: feats[k].to_numpy(float)[t, j] for k in FEATURES}
    out |= {f"ex_{h}": v for h, v in forward_excess(m, p.dates.get_indexer(fd)[t], j).items()}
    # 分母が 0 に近い比（SUE の標準偏差 0 等）の ±inf は、最上位・最下位の分位に集まって偽の候補を作るので欠損にする。
    df = pd.DataFrame(out).replace([np.inf, -np.inf], np.nan)
    df.attrs["dates"] = p.dates
    return df


def _stamp(db_path: Path, end: pd.Timestamp) -> dict:
    files = {}
    for suffix in ("", "-wal"):
        f = Path(f"{db_path}{suffix}")
        files[suffix] = (f.stat().st_size, f.stat().st_mtime_ns) if f.exists() else None
    here = Path(__file__).parent
    src = hashlib.sha256(b"".join((here / s).read_bytes() for s in _SOURCES)).hexdigest()
    return {"db": str(db_path), "files": files, "end": str(end), "src": src}


def load(db_path: str | Path, end: pd.Timestamp) -> pd.DataFrame:
    """explore/scan/dataset.pkl が同じ DB・end・ソースで作ったものなら読み込み、そうでなければ build して保存する。"""
    db_path = Path(db_path).resolve()
    cache = db_path.parent / "explore" / "scan" / "dataset.pkl"
    stamp = _stamp(db_path, end)
    if cache.exists():
        with cache.open("rb") as f:
            saved = pickle.load(f)
        if saved["stamp"] == stamp:
            return saved["df"]
    conn = sqlite3.connect(f"{db_path.as_uri()}?mode=ro", uri=True)
    try:
        df = build(conn, end)
    finally:
        conn.close()
    cache.parent.mkdir(parents=True, exist_ok=True)
    with cache.open("wb") as f:
        pickle.dump({"stamp": stamp, "df": df}, f)
    return df
