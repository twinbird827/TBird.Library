"""探索（explore.py）とフォワードテストが共有する月末パネルと、ルールのスコア関数。

形成日は各月の DailyBars 上の最終取引日。スコアは形成日の終値時点で分かる情報だけで作る:
財務は開示日が形成日より前、信用取引残高は基準日 + 7 暦日が形成日以前、業種は形成日以前で最新の Stocks スナップショット。
スコアは大きいほど買い。向きは事前に固定し、探索結果を見て反転させない。
"""
from __future__ import annotations

import sqlite3

import numpy as np
import pandas as pd

import db as dbmod

ETF_SECTOR = "9999"
MIN_TURNOVER_YEN = 1e8
MIN_PRICE_YEN = 100.0
# 公表日は 2026-09-25 より前が null で、週次の公表は基準日より後になるため、保守的に 1 週間ずらして先読みを防ぐ。
MARGIN_LAG = pd.Timedelta(days=7)
# 信用銘柄から外れた銘柄の古い残高を使い続けないため。週次（年末年始は 2 週空く）＋ MARGIN_LAG を収める。
MARGIN_MAX_AGE_DAYS = 31

# 業種単位でスコアが決まるルール。業種内順位にすると全銘柄が同順位になるので業種内順位版を評価しない。
SECTOR_LEVEL = {"14 業種6か月上昇率", "S4 業種空売り比率(低)"}

# フォワードテストで記録するルール名（#414 の探索結果を見て確定する）。
CANDIDATES: list[str] = []


def _asof(frame: pd.DataFrame, col: str, datecol: str, when: pd.DatetimeIndex, codes: pd.Index,
          strict: bool, maxage: int | None = None) -> pd.DataFrame:
    """when の各日について、datecol がその日より前（strict=False なら以前）の行のうち Code ごとの最新の col。"""
    f = frame.dropna(subset=[col]).sort_values(datecol)
    out = {}
    for d in when:
        sel = f[(f[datecol] < d) if strict else (f[datecol] <= d)]
        if maxage is not None:
            sel = sel[sel[datecol] >= d - pd.Timedelta(days=maxage)]
        out[d] = sel.groupby("Code")[col].last()
    return pd.DataFrame(out, columns=when).T.reindex(index=when, columns=codes)


def fin_asof(fin: pd.DataFrame, col: str, when: pd.DatetimeIndex, codes: pd.Index,
             datecol: str = "DiscloseDate", maxage: int | None = None) -> pd.DataFrame:
    """形成日より前（当日を含まない）に分かった最新値。当日の開示は引け後のことがあるため使わない。"""
    return _asof(fin, col, datecol, when, codes, strict=True, maxage=maxage)


def margin_asof(mi: pd.DataFrame, col: str, when: pd.DatetimeIndex, codes: pd.Index) -> pd.DataFrame:
    """基準日 + MARGIN_LAG が形成日以前の信用取引残高の最新値。"""
    m = mi.assign(Avail=mi["Date"] + MARGIN_LAG)
    return _asof(m, col, "Avail", when, codes, strict=False, maxage=MARGIN_MAX_AGE_DAYS)


def _read_optional(conn: sqlite3.Connection, table: str, sql: str) -> pd.DataFrame | None:
    """Standard 専用テーブルを読む。未マイグレーションの DB（バックフィル前の複製など）では None。"""
    if conn.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?", (table,)).fetchone() is None:
        return None
    df = pd.read_sql(sql, conn, parse_dates={"Date": "%Y-%m-%d"})
    return None if df.empty else df


def build_fin(conn: sqlite3.Connection, codes) -> tuple[pd.DataFrame, pd.DataFrame]:
    """codes の FinSummaries 全行（allfin）と、決算短信の行に SUE・ROE 等の派生指標を足したもの（fin）。"""
    allfin = dbmod.read_fin_summaries(conn).rename(
        columns={"OperatingProfit": "OP", "NetProfit": "NP", "TotalAssets": "TA"})
    allfin = allfin[allfin["Code"].isin(codes)]
    fin = allfin[allfin["DocType"].str.contains("FinancialStatements")].copy()
    fin["q"] = fin["DocType"].str[:2].map({"1Q": 1, "2Q": 2, "3Q": 3, "FY": 4})
    fin["cons"] = ~fin["DocType"].str.contains("NonConsolidated")
    fin = (fin.dropna(subset=["q"]).sort_values(["Code", "DiscloseDate", "cons"])
           .drop_duplicates(["Code", "DiscloseDate"], keep="last"))
    g = fin.groupby("Code")
    pq, pdt, pnp = g["q"].shift(), g["DiscloseDate"].shift(), g["NP"].shift()
    # 四半期単独の純利益: 1Q はそのまま、それ以外は直前四半期（150 日以内）の累計との差。
    fin["sNP"] = np.where(fin["q"] == 1, fin["NP"],
                          np.where((pq == fin["q"] - 1) & ((fin["DiscloseDate"] - pdt).dt.days < 150),
                                   fin["NP"] - pnp, np.nan))
    fin["key"] = fin["DiscloseDate"] - pd.Timedelta(days=365)
    py = fin[["Code", "q", "DiscloseDate", "NP", "sNP", "Sales", "OP", "TA"]].rename(
        columns=lambda x: x if x in ("Code", "q") else x + "_py")
    fin = pd.merge_asof(fin.sort_values("key"), py.sort_values("DiscloseDate_py"),
                        left_on="key", right_on="DiscloseDate_py", by=["Code", "q"],
                        direction="nearest", tolerance=pd.Timedelta(days=45))
    fy = fin[fin["q"] == 4][["Code", "DiscloseDate", "Sales", "OP", "NP"]].rename(
        columns={"DiscloseDate": "fyd", "Sales": "Sales_fy", "OP": "OP_fy", "NP": "NP_fy"})
    fin = pd.merge_asof(fin.sort_values("DiscloseDate"), fy.sort_values("fyd"),
                        left_on="DiscloseDate", right_on="fyd", by="Code", direction="backward",
                        allow_exact_matches=False, tolerance=pd.Timedelta(days=400))
    for x in ["Sales", "OP", "NP"]:
        fin[x + "_ttm"] = np.where(fin["q"] == 4, fin[x], fin[x] + fin[x + "_fy"] - fin[x + "_py"])
    fin = fin.sort_values(["Code", "DiscloseDate"])
    fin["dNP"] = fin["sNP"] - fin["sNP_py"]
    fin["sue"] = fin["dNP"] / fin.groupby("Code")["dNP"].transform(
        lambda s: s.shift().rolling(8, min_periods=4).std())
    fin["roa_op"] = fin["OP_ttm"] / fin["TA"]
    fin["roe"] = fin["NP_ttm"] / fin["Equity"].where(fin["Equity"] > 0)
    fin["agr"] = fin["TA"] / fin["TA_py"] - 1
    fin["bk"] = fin["Bps"].where(fin["Bps"] > 0)
    fin["sh"] = fin["Equity"] / fin["bk"]
    return allfin, fin


def ear(ev: pd.DataFrame, dates: pd.DatetimeIndex, px: pd.DataFrame, ewi: np.ndarray,
        cidx: dict[str, int]) -> pd.DataFrame:
    """発表前営業日終値→発表翌営業日終値の対全銘柄超過リターンと、それが判明する日。"""
    t0 = dates.searchsorted(ev["DiscloseDate"].values, "left") - 1
    t1 = dates.searchsorted(ev["DiscloseDate"].values, "right")
    okr = (t0 >= 0) & (t1 < len(dates))
    ev = ev[okr].copy()
    t0, t1 = t0[okr], t1[okr]
    j = ev["Code"].map(cidx).values
    ev["v"] = px.values[t1, j] / px.values[t0, j] - 1 - (ewi[t1] / ewi[t0] - 1)
    ev["avail"] = dates[t1]
    return ev


class Panel:
    """月末パネル。行は形成日 me、列は銘柄コード。"""

    def __init__(self, conn: sqlite3.Connection, end: pd.Timestamp | None = None):
        stocks = pd.read_sql("SELECT AsOfDate, Code, Sector33 FROM Stocks", conn,
                             parse_dates={"AsOfDate": "%Y-%m-%d"})
        etf = set(stocks.loc[stocks["Sector33"] == ETF_SECTOR, "Code"])
        bars = dbmod.read_daily_bars(conn, end=end)
        bars = bars[bars["AdjClose"].notna() & bars["Code"].isin(set(stocks["Code"]) - etf)]
        P = {x: bars.pivot(index="Date", columns="Code", values=x)
             for x in ["AdjClose", "Close", "AdjVolume", "Volume", "TurnoverValue"]}
        del bars
        px = P["AdjClose"]
        self.dates = dates = px.index
        self.codes = codes = px.columns
        cidx = {c: j for j, c in enumerate(codes)}
        ret = px.pct_change(fill_method=None).clip(-0.5, 1)
        ewr = np.nan_to_num(np.nanmean(ret.values, axis=1))
        ewi = np.cumprod(1 + ewr)
        self.me = me = pd.DatetimeIndex(dates.to_series().groupby(dates.to_period("M")).max().values)
        mpos = dates.get_indexer(me)
        # 上場廃止は最終値で清算する（翌月末に値が無くても 25 営業日まで最終値を持ち越す）。
        self.pm = pm = px.ffill(limit=25).loc[me]
        self.alive = px.loc[me].notna()
        self.liq = P["TurnoverValue"].rolling(20, min_periods=15).mean().loc[me]
        self.rawm = P["Close"].ffill(limit=5).loc[me]
        self.sec = _asof(stocks, "Sector33", "AsOfDate", me, codes, strict=False)

        # ---- 財務（Eps/Bps は read_fin_summaries が AdjClose の株数基準へ換算済み）----
        allfin, fin = build_fin(conn, codes)
        rev = ear(allfin.loc[allfin["DocType"] == "EarnForecastRevision", ["Code", "DiscloseDate"]].copy(),
                  dates, px, ewi, cidx)
        fe = ear(fin[["Code", "DiscloseDate"]].copy(), dates, px, ewi, cidx)
        self.F = F = {n: fin_asof(fin, n, me, codes) for n in ["sue", "roa_op", "roe", "agr", "Sales_ttm"]}
        # 株価反応は発表翌営業日の引けで判明するので、その日を含めて使う。
        F["ear"] = _asof(fe, "v", "avail", me, codes, strict=False, maxage=92)
        F["rev"] = _asof(rev, "v", "avail", me, codes, strict=False, maxage=92)
        fyb = fin[fin["q"] == 4]
        bk, sh = fin_asof(fyb, "bk", me, codes), fin_asof(fyb, "sh", me, codes)
        self.bp = bk / pm
        self.ep = fin_asof(fyb, "Eps", me, codes) / pm
        self.mcap = pm * sh

        # ---- 価格系 ----
        self.vol60 = px.pct_change(fill_method=None).rolling(60, min_periods=50).std().loc[me]
        self.hi52 = px.rolling(250, min_periods=200).max().loc[me]
        self.mx = ret.rolling(21, min_periods=18).max().loc[me]
        tv = P["TurnoverValue"]
        self.ami = (ret.abs() / tv.where(tv > 0)).rolling(20, min_periods=15).mean().loc[me]
        self.vs = (P["AdjVolume"].rolling(5).mean() / P["AdjVolume"].rolling(60, min_periods=50).mean()).loc[me]
        iv = {}
        for d, a in zip(me, mpos):
            if a < 60:
                continue
            R = ret.values[a - 59:a + 1]
            m = ewr[a - 59:a + 1][:, None]
            M = ~np.isnan(R)
            n = M.sum(0)
            with np.errstate(all="ignore"):
                Rz = np.where(M, R, 0)
                mxr = Rz.sum(0) / n
                mm = (m * M).sum(0) / n
                dm = (m - mm) * M
                cov = (dm * (Rz - mxr)).sum(0) / n
                vm = (dm ** 2).sum(0) / n
                res = (Rz - mxr - cov / vm * (m - mm)) * M
                iv[d] = np.where(n >= 50, np.sqrt((res ** 2).sum(0) / n), np.nan)
        self.ivol = pd.DataFrame(iv, index=codes).T.reindex(me)

        # ---- Standard 専用 ----
        nan = pd.DataFrame(np.nan, index=me, columns=codes)
        self.credit = self.long_turn = self.long_turn_chg = self.ssr = nan
        mi = _read_optional(conn, "MarginInterests",
                            "SELECT Date, Code, LongVolume, ShortVolume FROM MarginInterests")
        if mi is not None:
            # 信用残の株数は遡及調整されないので、分母も生の Volume にする（AdjVolume だと将来の分割が比率に混ざる）。
            vol20 = P["Volume"].rolling(20, min_periods=15).mean()
            ri, ci = dates.get_indexer(mi["Date"]), codes.get_indexer(mi["Code"])
            hit = (ri >= 0) & (ci >= 0)
            v = np.full(len(mi), np.nan)
            v[hit] = vol20.values[ri[hit], ci[hit]]
            mi["lt"] = mi["LongVolume"] / np.where(v > 0, v, np.nan)
            mi["credit"] = mi["LongVolume"] / mi["ShortVolume"].where(mi["ShortVolume"] > 0)
            self.credit = margin_asof(mi, "credit", me, codes)
            self.long_turn = margin_asof(mi, "lt", me, codes)
            prev = margin_asof(mi, "lt", me - pd.Timedelta(days=28), codes)
            self.long_turn_chg = self.long_turn - prev.values
        ss = _read_optional(conn, "SectorShortRatios",
                            "SELECT Date, Sector33, SellExShortValue, ShortWithRestrictionValue, "
                            "ShortNoRestrictionValue FROM SectorShortRatios")
        if ss is not None:
            short = ss["ShortWithRestrictionValue"] + ss["ShortNoRestrictionValue"]
            ss["ratio"] = short / (ss["SellExShortValue"] + short)
            r20 = (ss.pivot(index="Date", columns="Sector33", values="ratio")
                   .rolling(20, min_periods=15).mean())
            self.ssr = r20.reindex(r20.index.union(me)).ffill().loc[me]

    def eligible(self, i: int) -> pd.Series:
        """形成日 me[i] の母集団: 売買あり・20 日平均売買代金 1 億円以上・株価 100 円以上・業種が分かる（ETF は読込み時に除外済み）。"""
        return (self.alive.iloc[i] & (self.liq.iloc[i] >= MIN_TURNOVER_YEN)
                & (self.rawm.iloc[i] >= MIN_PRICE_YEN) & self.sec.iloc[i].notna())

    def score(self, name: str, i: int, ok: pd.Series) -> pd.Series:
        """ルール name の形成日 me[i] のスコア（銘柄コード順。ok は業種集計・業種内順位の母集団）。"""
        return RULES[name](self, i, ok).reindex(self.codes)

    def sector_rank(self, s: pd.Series, i: int) -> pd.Series:
        """業種内のパーセンタイル順位。業種内の銘柄が 5 未満なら NaN。"""
        sec = self.sec.iloc[i].reindex(s.index)
        return s.groupby(sec).transform(lambda x: x.rank(pct=True) if len(x) >= 5 else np.nan)

    def mom(self, i: int, a: int, b: int = 0) -> pd.Series:
        return self.pm.iloc[i - b] / self.pm.iloc[i - a] - 1


def _positive_pct(s: pd.Series) -> pd.Series:
    return s.where(s > 0).rank(pct=True)


def _industry_mom(p: Panel, i: int, ok: pd.Series) -> pd.Series:
    m6 = p.mom(i, 6)[ok].dropna()
    sec = p.sec.iloc[i].reindex(m6.index)
    return sec.map(m6.groupby(sec).mean())


def _sector_short(p: Panel, i: int, ok: pd.Series) -> pd.Series:
    return -p.sec.iloc[i][ok].map(p.ssr.iloc[i])


RULES = {
    "12-1モメンタム(高)": lambda p, i, ok: p.mom(i, 12, 1),
    "1か月リバーサル(下落上位)": lambda p, i, ok: -p.mom(i, 1),
    "3か月リバーサル(下落上位)": lambda p, i, ok: -p.mom(i, 3),
    "低PBR(B/P高)": lambda p, i, ok: p.bp.iloc[i],
    "高益回り(E/P高)": lambda p, i, ok: p.ep.iloc[i].where(p.ep.iloc[i] > 0),
    "バリュー複合(E/P+B/P順位)": lambda p, i, ok: _positive_pct(p.ep.iloc[i]) + _positive_pct(p.bp.iloc[i]),
    "低ボラ(60日)": lambda p, i, ok: -p.vol60.iloc[i],
    "52週高値近接": lambda p, i, ok: p.pm.iloc[i] / p.hi52.iloc[i],
    "低PBR×1か月下落": lambda p, i, ok: _positive_pct(p.bp.iloc[i]) + (-p.mom(i, 1)).rank(pct=True),
    "1 増益サプライズ(SUE)": lambda p, i, ok: p.F["sue"].iloc[i],
    "2 決算発表時の株価反応": lambda p, i, ok: p.F["ear"].iloc[i],
    "3 予想修正時の株価反応": lambda p, i, ok: p.F["rev"].iloc[i],
    "4 営業利益/総資産": lambda p, i, ok: p.F["roa_op"].iloc[i],
    "5 ROE": lambda p, i, ok: p.F["roe"].iloc[i],
    "6 総資産の伸び(低)": lambda p, i, ok: -p.F["agr"].iloc[i],
    "7 業種内 低PBR": lambda p, i, ok: p.sector_rank(p.bp.iloc[i][ok].dropna(), i),
    "8 PSR(売上/時価総額 高)": lambda p, i, ok: (p.F["Sales_ttm"].iloc[i] / p.mcap.iloc[i]).where(p.F["Sales_ttm"].iloc[i] > 0),
    "9 小型株": lambda p, i, ok: -p.mcap.iloc[i].where(p.mcap.iloc[i] > 0),
    "10 流動性の低さ(Amihud)": lambda p, i, ok: p.ami.iloc[i],
    "11 最大日次上昇(低)": lambda p, i, ok: -p.mx.iloc[i],
    "12 固有ボラ(低)": lambda p, i, ok: -p.ivol.iloc[i],
    "13 出来高急増": lambda p, i, ok: p.vs.iloc[i],
    "14 業種6か月上昇率": _industry_mom,
    "S1 信用倍率(低)": lambda p, i, ok: -p.credit.iloc[i],
    "S2 信用買い残/出来高(低)": lambda p, i, ok: -p.long_turn.iloc[i],
    "S3 信用買い残/出来高 4週変化(低)": lambda p, i, ok: -p.long_turn_chg.iloc[i],
    "S4 業種空売り比率(低)": _sector_short,
}
