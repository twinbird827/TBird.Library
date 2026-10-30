"""探索（explore_triggers.py）とフォワードテストが共有する日次パネルと、買い・売りのトリガー。

トリガーはトリガー日 t の引け後に分かる情報だけで判定し、日付 × 銘柄の真偽表を返す（母集団の条件はかけない）。
財務は開示日が t 以前、信用取引残高は基準日 + 7 暦日が t 以前、業種・貸借区分は t 以前で最新の Stocks スナップショット。
価格・出来高は調整後の値を使い、生の出来高は信用買い残の比率（X8）の分母にだけ使う。
"""
from __future__ import annotations

import sqlite3
from typing import Callable

import numpy as np
import pandas as pd

import db as dbmod
import rules

HOLDING_DAYS = [5, 60, 120, 240]
MARGINABLE = "2"


def _daily_asof(p: DailyPanel, frame: pd.DataFrame, col: str, datecol: str = "DiscloseDate") -> pd.DataFrame:
    """各営業日 t について、datecol が t 以前の行のうち Code ごとの最新の col。"""
    f = frame.dropna(subset=[col]).sort_values(datecol, kind="stable")
    f = f.assign(i=p.dates.searchsorted(f[datecol].values, "left"))
    f = f[(f["i"] < len(p.dates)) & f["Code"].isin(p.cidx)]
    m = f.groupby(["i", "Code"])[col].last().unstack()
    return m.reindex(index=range(len(p.dates)), columns=p.codes).ffill().set_axis(p.dates)


def _events(p: DailyPanel, frame: pd.DataFrame, datecol: str = "DiscloseDate") -> pd.DataFrame:
    """frame の各行を、datecol 以後で最初の営業日に立てた真偽表。"""
    i = p.dates.searchsorted(frame[datecol].values, "left")
    j = p.codes.get_indexer(frame["Code"])
    ok = (i < len(p.dates)) & (j >= 0)
    out = np.zeros((len(p.dates), len(p.codes)), bool)
    out[i[ok], j[ok]] = True
    return pd.DataFrame(out, index=p.dates, columns=p.codes)


class DailyPanel:
    """日次パネル。行は営業日、列は銘柄コード。"""

    def __init__(self, conn: sqlite3.Connection, end: pd.Timestamp | None = None):
        stocks = pd.read_sql("SELECT AsOfDate, Code, Sector33, MarginCode FROM Stocks", conn,
                             parse_dates={"AsOfDate": "%Y-%m-%d"})
        etf = set(stocks.loc[stocks["Sector33"] == rules.ETF_SECTOR, "Code"])
        bars = dbmod.read_daily_bars(conn, end=end)
        bars = bars[bars["AdjClose"].notna() & bars["Code"].isin(set(stocks["Code"]) - etf)]
        P = {x: bars.pivot(index="Date", columns="Code", values=x)
             for x in ["AdjOpen", "AdjHigh", "AdjLow", "AdjClose", "AdjVolume", "Close", "Volume", "TurnoverValue"]}
        del bars
        self.px = px = P["AdjClose"]
        self.open, self.high, self.low = P["AdjOpen"], P["AdjHigh"], P["AdjLow"]
        self.vol, self.rawvol = P["AdjVolume"], P["Volume"]
        self.dates = px.index
        self.codes = px.columns
        self.cidx = {c: j for j, c in enumerate(self.codes)}
        ret = px.pct_change(fill_method=None).clip(-0.5, 1)
        self.ewi = np.cumprod(1 + np.nan_to_num(np.nanmean(ret.values, axis=1)))
        self.sec = _daily_asof(self, stocks, "Sector33", "AsOfDate")
        self.marginable = _daily_asof(self, stocks, "MarginCode", "AsOfDate") == MARGINABLE
        liq = P["TurnoverValue"].rolling(20, min_periods=15).mean()
        self.eligible = (px.notna() & (liq >= rules.MIN_TURNOVER_YEN)
                         & (P["Close"].ffill(limit=5) >= rules.MIN_PRICE_YEN) & self.sec.notna())
        self.allfin, self.fin = rules.build_fin(conn, self.codes)
        self.margin = rules._read_optional(conn, "MarginInterests",
                                           "SELECT Date, Code, LongVolume, ShortVolume FROM MarginInterests")

    def excess(self, n: int) -> pd.DataFrame:
        """n 営業日の対全銘柄超過リターン。"""
        e = pd.Series(self.ewi, index=self.dates)
        return (self.px / self.px.shift(n) - 1).sub(e / e.shift(n) - 1, axis=0)


# 業績予想の列（連結, 本決算の来期, 単体）。
OP_COLS = ("ForecastOperatingProfit", "NextForecastOperatingProfit", "ForecastNonConsolidatedOperatingProfit")
NP_COLS = ("ForecastNetProfit", "NextForecastNetProfit", "ForecastNonConsolidatedNetProfit")


def revisions(p: DailyPanel) -> pd.DataFrame:
    """業績予想の修正ごとの、会計年度末 fye・予想純利益 np・同じ会計年度の直前の予想からの変化率 chg（直前の予想が正の行だけ）。

    直前の予想は、開示日が修正より前の行のうち、会計年度末が同じ行の通期予想か、来期の会計年度末が同じ本決算の来期予想。
    営業利益で比べ、修正か直前のどちらかに無ければ純利益で比べる。
    """
    a = p.allfin
    st = a[a["DocType"].str.contains("FinancialStatements")]
    rv = a[a["DocType"] == "EarnForecastRevision"].sort_values("DiscloseDate")
    # 単体決算の会社（直前の決算短信が単体）の修正は、連結の列が空なら単体の列を使う。決算短信は連結の列に単体の値が入る。
    nc = (st[["Code", "DiscloseDate"]].assign(nc=st["DocType"].str.contains("NonConsolidated"))
          .rename(columns={"DiscloseDate": "sd"}).sort_values("sd"))
    rv = pd.merge_asof(rv, nc, left_on="DiscloseDate", right_on="sd", by="Code", allow_exact_matches=False)
    is_nc = rv["nc"].eq(True)
    for m, (cur, _, ncc) in {"op": OP_COLS, "np": NP_COLS}.items():
        rv[m] = rv[cur].where(~(is_nc & rv[cur].isna()), rv[ncc])

    key = ["Code", "DiscloseDate", "fye", "op", "np"]
    fy = st[st["DocType"].str.startswith("FY")]
    cand = pd.concat([
        rv.rename(columns={"CurrentFiscalYearEnd": "fye"})[key],
        st[["Code", "DiscloseDate", "CurrentFiscalYearEnd", OP_COLS[0], NP_COLS[0]]].set_axis(key, axis=1),
        fy[["Code", "DiscloseDate", "NextFiscalYearEnd", OP_COLS[1], NP_COLS[1]]].set_axis(key, axis=1),
    ]).rename(columns={"DiscloseDate": "pd"}).dropna(subset=["fye"])
    out = rv.rename(columns={"CurrentFiscalYearEnd": "fye"})[key].dropna(subset=["fye"])
    for m in ("op", "np"):
        x = (out[["Code", "DiscloseDate", "fye"]].reset_index()
             .merge(cand[["Code", "pd", "fye", m]].dropna(subset=[m]), on=["Code", "fye"]))
        x = x[x["pd"] < x["DiscloseDate"]].sort_values("pd", kind="stable")
        out["prev_" + m] = x.groupby("index")[m].last()
    use_op = out["op"].notna() & out["prev_op"].notna()
    new, prev = out["op"].where(use_op, out["np"]), out["prev_op"].where(use_op, out["prev_np"])
    out["chg"] = (new / prev - 1).where(prev > 0)
    return out.dropna(subset=["chg"])[["Code", "DiscloseDate", "fye", "np", "chg"]]


def _revision(p: DailyPanel, up: bool, th: float = 0.10) -> pd.DataFrame:
    r = revisions(p)
    return _events(p, r[r["chg"] >= th] if up else r[r["chg"] <= -th])


def _fy(p: DailyPanel) -> pd.DataFrame:
    return p.fin[p.fin["q"] == 4]


def _mcap(p: DailyPanel) -> pd.DataFrame:
    """時価総額（調整後終値 × 直近の本決算の株数）。"""
    return p.px * _daily_asof(p, _fy(p), "sh")


def _cheap(p: DailyPanel) -> pd.DataFrame:
    """PBR 1 倍未満（直近の本決算の BPS）。"""
    return p.px / _daily_asof(p, _fy(p), "bk") < 1


def _c9(p: DailyPanel) -> pd.DataFrame:
    r = revisions(p)
    r = r[(r["chg"] >= 0.10) & r["np"].notna()]
    return _events(p, r) & (_daily_asof(p, r, "np") / _mcap(p) >= 0.10)


def _c10(p: DailyPanel) -> pd.DataFrame:
    r = revisions(p)
    first_up = r[r["chg"] > 0].groupby(["Code", "fye"])["DiscloseDate"].min().rename("first_up")
    r = r.join(first_up, on=["Code", "fye"])
    return _events(p, r[(r["chg"] >= 0.10) & (r["DiscloseDate"] > r["first_up"])]) & _cheap(p)


def _c11(p: DailyPanel) -> pd.DataFrame:
    # 同じ日の修正は「後の実績」で裏付けられたことにならないので、窓は t−60〜t−1。
    e1 = _revision(p, True).astype(float).shift(1).rolling(60, min_periods=1).max() > 0
    return _sue(p, True) & e1 & _cheap(p)


def _mcap_tier(p: DailyPanel, lo: float, hi: float) -> pd.DataFrame:
    """母集団の中の時価総額の順位が lo 超・hi 以下。"""
    r = _mcap(p).where(p.eligible).rank(axis=1, pct=True)
    return (r > lo) & (r <= hi)


def _market_up(p: DailyPanel, up: bool) -> np.ndarray:
    """全銘柄等金額指数が 200 営業日平均より上（up=False なら以下）。平均が出ない日はどちらも偽。"""
    e = pd.Series(p.ewi, index=p.dates)
    ma = e.rolling(200, min_periods=200).mean()
    return ((e > ma) if up else (e <= ma)).to_numpy()[:, None]


def _sector_pbr_rank(p: DailyPanel) -> pd.DataFrame:
    """母集団の銘柄の PBR の業種内パーセンタイル順位。業種内の銘柄が 5 未満なら NaN。

    BPS は直近の本決算の値で、0 以下なら NaN（"bk" は 0 以下の期を落として前の期の正の値を持ち越すので使わない）。
    """
    bps = _daily_asof(p, _fy(p), "Bps")
    s = (p.px / bps.where(bps > 0)).where(p.eligible).stack().dropna()
    g = s.groupby([s.index.get_level_values(0), p.sec.stack().reindex(s.index)])
    return g.rank(pct=True).where(g.transform("size") >= 5).unstack().reindex(index=p.dates, columns=p.codes)


def _ear(p: DailyPanel, up: bool) -> pd.DataFrame:
    ev = rules.ear(p.fin[["Code", "DiscloseDate"]].copy(), p.dates, p.px, p.ewi, p.cidx)
    return _events(p, ev[ev["v"] >= 0.05] if up else ev[ev["v"] <= -0.05], "avail")


def _e4(p: DailyPanel) -> pd.DataFrame:
    fy = _fy(p)
    return _events(p, fy[(fy["OP"] > 0) & (fy[OP_COLS[1]] >= 1.2 * fy["OP"])])


def _first_extreme(p: DailyPanel, high: bool) -> pd.DataFrame:
    """終値が過去 250 営業日の最高値（high=False なら最安値）を超え、直前 20 営業日には超えていない。"""
    w = p.px.shift(1).rolling(250, min_periods=200)
    new = p.px > w.max() if high else p.px < w.min()
    return new & ~(new.astype(float).shift(1).rolling(20, min_periods=1).max() > 0)


def _volume_spike(p: DailyPanel, up: bool) -> pd.DataFrame:
    """出来高が過去 60 営業日平均の 5 倍以上で、前日比 +5% 以上（up=False なら −5% 以下）。"""
    avg = p.vol.shift(1).rolling(60, min_periods=50).mean()
    r = p.px.pct_change(fill_method=None)
    return (avg > 0) & (p.vol >= 5 * avg) & ((r >= 0.05) if up else (r <= -0.05))


def _cross(p: DailyPanel, up: bool) -> pd.DataFrame:
    """25 日移動平均が 75 日移動平均を下から上に（up=False なら上から下に）抜けた。"""
    d = p.px.rolling(25).mean() - p.px.rolling(75).mean()
    return (d > 0) & (d.shift(1) <= 0) if up else (d < 0) & (d.shift(1) >= 0)


def _c1(p: DailyPanel) -> pd.DataFrame:
    return ((_daily_asof(p, p.fin, "roe") >= 0.10) & (_daily_asof(p, p.fin, "roa_op") >= 0.05)
            & (p.excess(20) <= -0.20))


def _sue(p: DailyPanel, up: bool) -> pd.DataFrame:
    return _events(p, p.fin[p.fin["sue"] >= 2] if up else p.fin[p.fin["sue"] <= -2])


def _margin(p: DailyPanel) -> pd.DataFrame:
    """信用取引残高に、使える日 Avail・信用倍率 credit・買い残÷20 日平均出来高 lt を足したもの。"""
    mi = p.margin.copy()
    # 信用残の株数は遡及調整されないので、分母も生の Volume にする。
    vol20 = p.rawvol.rolling(20, min_periods=15).mean()
    ri, ci = p.dates.get_indexer(mi["Date"]), p.codes.get_indexer(mi["Code"])
    hit = (ri >= 0) & (ci >= 0)
    v = np.full(len(mi), np.nan)
    v[hit] = vol20.values[ri[hit], ci[hit]]
    mi["lt"] = mi["LongVolume"] / np.where(v > 0, v, np.nan)
    long, short = mi["LongVolume"], mi["ShortVolume"]
    mi["credit"] = np.where(short > 0, long / short.where(short > 0), np.where(long > 0, np.inf, np.nan))
    mi["Avail"] = mi["Date"] + rules.MARGIN_LAG
    return mi.sort_values(["Code", "Date"])


def _none(p: DailyPanel) -> pd.DataFrame:
    return pd.DataFrame(False, index=p.dates, columns=p.codes)


def _s1(p: DailyPanel) -> pd.DataFrame:
    if p.margin is None:
        return _none(p)
    mi = _margin(p).dropna(subset=["credit"])
    g = mi.groupby("Code")
    prev, pdate = g["credit"].shift(), g["Date"].shift()
    hit = (mi["credit"] < 1) & (prev >= 1) & ((mi["Date"] - pdate).dt.days <= rules.MARGIN_MAX_AGE_DAYS)
    return _events(p, mi[hit], "Avail")


def _x8(p: DailyPanel) -> pd.DataFrame:
    if p.margin is None:
        return _none(p)
    mi = _margin(p).dropna(subset=["lt"])
    # 使える日の 4 週前に使えた値: 基準日が 28〜59 日前の最新。
    left = mi.assign(key=mi["Date"] - pd.Timedelta(days=28)).sort_values("key")
    right = mi[["Code", "Date", "lt"]].rename(columns={"Date": "pdate", "lt": "lt4"}).sort_values("pdate")
    m = pd.merge_asof(left, right, left_on="key", right_on="pdate", by="Code", direction="backward",
                      tolerance=pd.Timedelta(days=rules.MARGIN_MAX_AGE_DAYS))
    return _events(p, m[m["lt"] >= 2 * m["lt4"].where(m["lt4"] > 0)], "Avail")


TRIGGERS: dict[str, Callable[[DailyPanel], pd.DataFrame]] = {
    "E1 上方修正": lambda p: _revision(p, True),
    "E2 増益サプライズ": lambda p: _sue(p, True),
    "E3 決算翌日の急騰": lambda p: _ear(p, True),
    "E4 来期大幅増益予想": _e4,
    "P1 52週高値の初更新": lambda p: _first_extreme(p, True),
    "P2 出来高急増の大陽線": lambda p: _volume_spike(p, True),
    "P3 ゴールデンクロス": lambda p: _cross(p, True),
    "P4 急落": lambda p: p.excess(5) <= -0.20,
    "C1 財務良×急落": _c1,
    "C2 赤字×上方修正": lambda p: _revision(p, True) & (_daily_asof(p, p.fin, "NP_ttm") < 0),
    "C3 割安×上方修正": lambda p: _revision(p, True) & _cheap(p),
    "C4 増益サプライズ×52週高値近接": lambda p: _sue(p, True) & (p.px >= 0.95 * p.px.rolling(250, min_periods=200).max()),
    "S1 信用倍率の売り長転換": _s1,
    "X1 下方修正": lambda p: _revision(p, False),
    "X2 減益サプライズ": lambda p: _sue(p, False),
    "X3 決算翌日の急落": lambda p: _ear(p, False),
    "X4 52週安値の初更新": lambda p: _first_extreme(p, False),
    "X5 出来高急増の大陰線": lambda p: _volume_spike(p, False),
    "X6 デッドクロス": lambda p: _cross(p, False),
    "X7 急騰": lambda p: p.excess(5) >= 0.20,
    "X8 信用買い残の急増": _x8,
}
# 単独で傾向のあった指標の組み合わせ（explore_triggers.py --combo だけが評価する）。
COMBO_TRIGGERS: dict[str, Callable[[DailyPanel], pd.DataFrame]] = {
    # 急落は t の大陽線で縮まないよう t−1 までで見る。
    "C5 急落後の出来高急増の大陽線": lambda p: _volume_spike(p, True) & (p.excess(5).shift(1) <= -0.20),
    "C6 業種内割安×上方修正": lambda p: _revision(p, True) & (_sector_pbr_rank(p) <= 0.2),
    "C7 業種内割安×ゴールデンクロス": lambda p: _cross(p, True) & (_sector_pbr_rank(p) <= 0.2),
    "C8 業種内割安×信用倍率の売り長転換": lambda p: _s1(p) & (_sector_pbr_rank(p) <= 0.2),
    "X9 赤字×急落": lambda p: (_daily_asof(p, p.fin, "NP_ttm") < 0) & (p.excess(5) <= -0.20),
    "X10 業種内割高×急落": lambda p: (_sector_pbr_rank(p) > 0.8) & (p.excess(5) <= -0.20),
    "C9 予想割安×上方修正": _c9,
    "C10 割安×2回目以降の上方修正": _c10,
    "C11 割安×上方修正後の増益サプライズ": _c11,
}
# C3 の頑健性グリッド（割安の定義 × 修正の幅）と層別。選抜には使わず、周りの設定でも効くかを見るだけ。
_CHEAP: dict[str, Callable[[DailyPanel], pd.DataFrame]] = {
    "PBR1倍未満": _cheap,
    **{f"業種内下位{q:.0%}": (lambda p, q=q: _sector_pbr_rank(p) <= q) for q in (0.1, 0.2, 0.3)},
}
_STRATA: dict[str, Callable[[DailyPanel], pd.DataFrame | np.ndarray]] = {
    "大型": lambda p: _mcap_tier(p, 2 / 3, 1),
    "中型": lambda p: _mcap_tier(p, 1 / 3, 2 / 3),
    "小型": lambda p: _mcap_tier(p, -np.inf, 1 / 3),
    "地合い上": lambda p: _market_up(p, True),
    "地合い下": lambda p: _market_up(p, False),
}
ROBUSTNESS: dict[str, Callable[[DailyPanel], pd.DataFrame]] = {
    **{f"G {c}×修正{w:.0%}": (lambda p, f=f, w=w: _revision(p, True, w) & f(p))
       for c, f in _CHEAP.items() for w in (0.05, 0.10, 0.20)},
    **{f"G C3×{s}": (lambda p, f=f: TRIGGERS["C3 割安×上方修正"](p) & f(p)) for s, f in _STRATA.items()},
}
SELL_TRIGGERS = {n for n in [*TRIGGERS, *COMBO_TRIGGERS] if n.startswith("X")}
# 【反発】のトリガー。行き過ぎの戻りを狙うので損切りを掛けない（それ以外は【トレンド】で損切りを掛ける）。
REVERSAL = {"P4 急落", "C1 財務良×急落", "X7 急騰", "C5 急落後の出来高急増の大陽線"}
