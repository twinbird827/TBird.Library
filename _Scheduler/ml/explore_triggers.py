"""triggers.py の全トリガーを「t の翌営業日の始値で建て、決めた営業日数だけ持つ」前提で評価し、事前に固定した基準で選ぶ。

買いのトリガーは買い、売りのトリガーは (a) 貸借銘柄の空売りとして評価する。比べる相手は、t に母集団の条件を満たす全銘柄の
同じ区間の等金額平均。往復コスト 0.3% を引き、空売りは貸株料（年 1.15%、逆日歩は入れない）も引く。
【トレンド】のトリガーは建値から 10% 逆行したら手仕舞う。保有中の同じ銘柄への再トリガーは数えない。
トリガー日の月は、DailyBars の最古月から 13 か月後〜最長の保有期間の売却日が DB に収まる最後の月。月数で 3 等分して P1・P2・P3。
通過の基準: 件数 200 以上・30 か月以上・月平均の超過リターンが P1・P2・P3 のすべてで正・全期間の Newey-West の t 値 2.5 以上
（空売りは超過リターンの符号を反転して当てる）。各トリガーは、通過した保有期間のうち P1〜P3 の最小値が最大のものを採る。
通過した売りのトリガーを、通過した買いのトリガーの (b) 見送り条件と (c) 保有中の早めの売り条件として評価し、元との差の
月次系列の t 値 2.5 以上・P1〜P3 のすべてで正なら採用する。最後に、通過した買いのトリガーの OR 戦略を日次で組んで要約する。
"""
from __future__ import annotations

import argparse
import math
import sqlite3
import warnings
from pathlib import Path
from typing import NamedTuple

import numpy as np
import pandas as pd

import explore
import triggers

COST = 0.003
LENDING_FEE = 0.0115
DAYS_PER_YEAR = 245
STOP = 0.10
MIN_TRADES = 200
MIN_MONTHS = 30
MIN_T = 2.5
SELL_LOOKBACK = 20
PERIODS = explore.PERIODS


def nw_t(x: pd.Series, lag: int) -> float:
    """Newey-West の t 値（自己共分散は n−1 で割る。lag=0 で explore.t_stat と一致する）。"""
    n = len(x)
    if n <= 2:
        return np.nan
    d = x.to_numpy(float) - x.mean()
    var = d @ d / (n - 1)
    for k in range(1, min(lag, n - 1) + 1):
        var += 2 * (1 - k / (lag + 1)) * (d[k:] @ d[:-k]) / (n - 1)
    return x.mean() / np.sqrt(var / n) if var > 0 else np.nan


def nw_lag(hold: int) -> int:
    return math.ceil(hold / 21)


class Market:
    """約定と比較に使う配列（行は営業日、列は銘柄）。"""

    def __init__(self, p):
        self.dates = p.dates
        self.n = len(p.dates)
        self.O, self.H, self.L = p.open.to_numpy(), p.high.to_numpy(), p.low.to_numpy()
        # 上場廃止は最終値で清算する（値が無くても 25 営業日まで最終値を持ち越す）。
        self.C = p.px.ffill(limit=25).to_numpy()
        self.elig = p.eligible.to_numpy(bool)
        self.marg = p.marginable.to_numpy(bool)
        self.sec = p.sec.to_numpy()
        hmax = max(triggers.HOLDING_DAYS)
        # bench[t, h]: t の母集団の、t+1 の始値 → t+h の終値の等金額平均リターン。
        self.bench = np.full((self.n, hmax + 1), np.nan)
        with warnings.catch_warnings(), np.errstate(all="ignore"):
            warnings.simplefilter("ignore", RuntimeWarning)
            for t in range(self.n - 1):
                js = np.flatnonzero(self.elig[t] & ~np.isnan(self.O[t + 1]))
                e = min(t + hmax, self.n - 1)
                if len(js):
                    self.bench[t, 1:e - t + 1] = np.nanmean(self.C[t + 1:e + 1, js] / self.O[t + 1, js] - 1, axis=1)
            # 前営業日の母集団の、その日の終値→終値（b_cc）と始値→終値（b_oc）の等金額平均。
            prev = self.elig[:-1]
            self.b_cc = np.r_[np.nan, np.nanmean(np.where(prev, self.C[1:] / self.C[:-1] - 1, np.nan), axis=1)]
            self.b_oc = np.r_[np.nan, np.nanmean(np.where(prev, self.C[1:] / self.O[1:] - 1, np.nan), axis=1)]


class Leg(NamedTuple):
    """1 本のトリガーの建て方。skip が真の日は建てず、保有中に sell が真になったら翌営業日の始値で手仕舞う。"""
    sig: np.ndarray
    hold: int
    stop: bool
    skip: np.ndarray | None = None
    sell: np.ndarray | None = None


def simulate(m: Market, legs: list[Leg], short: bool = False) -> pd.DataFrame:
    """legs のトリガーを t+1 の始値で建て、t+hold の終値で手仕舞った取引（列 t, j, exit, px, ex, leg）。

    同じ銘柄を保有中（建てた日〜手仕舞った日）の再トリガーは、どの leg でも数えない。同じ日の重なりは先の leg を採る。
    stop の leg は建値から 10% 逆行したら、日中の接触は逆指値の価格、寄付きで越えていたら始値で手仕舞う（最終日も損切りを優先）。
    short=True は貸借銘柄だけを空売りし、ex は超過リターンの符号を反転して貸株料を引いた値。
    """
    ev = []
    for k, g in enumerate(legs):
        ok = g.sig & m.elig
        if short:
            ok &= m.marg
        if g.skip is not None:
            ok &= ~g.skip
        ok[max(m.n - g.hold, 0):] = False
        t, j = np.nonzero(ok)
        ev.append(np.c_[j, t, np.full(len(t), k)])
    ev = np.concatenate(ev)
    ev = ev[np.lexsort((ev[:, 2], ev[:, 1], ev[:, 0]))]
    rows, last_j, last_x = [], -1, -1
    for j, t, k in ev:
        if j == last_j and t <= last_x:
            continue
        g = legs[k]
        a, e = t + 1, t + g.hold
        entry = m.O[a, j]
        if np.isnan(entry):
            continue
        x, px = e, m.C[e, j]
        if g.stop:
            lvl = entry * (1 + STOP) if short else entry * (1 - STOP)
            o = m.O[a:e + 1, j]
            hit = np.flatnonzero(m.H[a:e + 1, j] >= lvl if short else m.L[a:e + 1, j] <= lvl)
            if len(hit):
                h = hit[0]
                x = a + h
                px = o[h] if (o[h] >= lvl if short else o[h] <= lvl) else lvl
        if g.sell is not None:
            s = np.flatnonzero(g.sell[a:e, j])
            # 寄付きの売りは、同じ日の日中の損切りより先に約定する。
            if len(s) and a + s[0] + 1 <= x and not np.isnan(m.O[a + s[0] + 1, j]):
                x = a + s[0] + 1
                px = m.O[x, j]
        if np.isnan(px):
            continue
        last_j, last_x = j, x
        r = px / entry - 1 - m.bench[t, x - t]
        ex = -r - COST - LENDING_FEE * (x - t) / DAYS_PER_YEAR if short else r - COST
        rows.append((t, j, x, px, ex, k))
    return pd.DataFrame(rows, columns=["t", "j", "exit", "px", "ex", "leg"]).astype(
        {"t": int, "j": int, "exit": int, "px": float, "ex": float, "leg": int})


def lookback(sell: np.ndarray) -> np.ndarray:
    """各日を含む直前 20 営業日のどこかで sell が真だったか（(b) の見送り条件）。"""
    return pd.DataFrame(sell, dtype=float).rolling(SELL_LOOKBACK, min_periods=1).max().to_numpy() > 0


def periods(dates: pd.DatetimeIndex) -> pd.Series:
    """トリガー日の月 → P1/P2/P3（端数は P3）。"""
    last = pd.Series(np.arange(len(dates)), index=dates.to_period("M")).groupby(level=0).max()
    months = last.index[explore.FIRST_FORMATION:]
    months = months[last.loc[months].to_numpy() + max(triggers.HOLDING_DAYS) <= len(dates) - 1]
    b = len(months) // 3
    return pd.Series(["P1"] * b + ["P2"] * b + ["P3"] * (len(months) - 2 * b), index=months)


def by_period(s: pd.Series, per: pd.Series) -> dict[str, float]:
    return {k: s[per.reindex(s.index).to_numpy() == k].mean() for k in PERIODS}


def summarize(tr: pd.DataFrame, m: Market, per: pd.Series, lag: int) -> tuple[dict, pd.Series]:
    """取引の一覧の要約と、トリガー日の月ごとの平均超過リターン。"""
    tr = tr.dropna(subset=["ex"])
    s = tr["ex"].groupby(m.dates[tr["t"].to_numpy()].to_period("M")).mean()
    sec = pd.Series(m.sec[tr["t"].to_numpy(), tr["j"].to_numpy()])
    row = {"件数": len(tr), "月数": len(s), "超過": s.mean(), "t": nw_t(s, lag), **by_period(s, per),
           "上位業種比率": sec.value_counts(normalize=True).max() if len(sec) else np.nan}
    return row, s


def passes(row: dict) -> bool:
    return (row["件数"] >= MIN_TRADES and row["月数"] >= MIN_MONTHS
            and all(row[k] > 0 for k in PERIODS) and row["t"] >= MIN_T)


def daily_excess(m: Market, tr: pd.DataFrame) -> tuple[pd.Series, np.ndarray]:
    """保有中の全ポジションの等金額平均の日次超過リターンと、日ごとの保有数。

    ポジションの日次リターンは、買った日は始値→終値、売った日は前日終値→売値（往復コストをこの日に引く）、それ以外は終値→終値。
    """
    tot, cnt = np.zeros(m.n), np.zeros(m.n)
    for t, j, x, px in tr[["t", "j", "exit", "px"]].itertuples(index=False):
        a = t + 1
        start = np.r_[m.O[a, j], m.C[a:x, j]]
        end = np.r_[m.C[a:x, j], px]
        ex = end / start - 1 - np.r_[m.b_oc[a], m.b_cc[a + 1:x + 1]]
        ex[-1] -= COST
        tot[a:x + 1] += np.nan_to_num(ex)
        cnt[a:x + 1] += 1
    held = cnt > 0
    return pd.Series(tot[held] / cnt[held], index=m.dates[held]), cnt


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "triggers"
    out_dir.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(f"{db_path.as_uri()}?mode=ro", uri=True)
    try:
        p = triggers.DailyPanel(conn)
    finally:
        conn.close()
    raw = {n: f(p).to_numpy(bool) for n, f in triggers.TRIGGERS.items()}
    m = Market(p)
    del p
    per = periods(m.dates)
    inper = m.dates.to_period("M").isin(per.index)
    sig = {n: r & inper[:, None] for n, r in raw.items()}

    rows, months, base = [], [], {}

    def record(name: str, variant: str, hold, row: dict, s: pd.Series) -> None:
        side = "空売り" if name in triggers.SELL_TRIGGERS else "買い"
        rows.append({"trigger": name, "side": side, "variant": variant, "hold": hold, **row})
        months.append(pd.DataFrame({"month": s.index.astype(str), "ex": s.to_numpy()})
                      .assign(trigger=name, variant=variant, hold=hold))

    for name, sg in sig.items():
        short = name in triggers.SELL_TRIGGERS
        for hold in triggers.HOLDING_DAYS:
            row, s = summarize(simulate(m, [Leg(sg, hold, name not in triggers.REVERSAL)], short), m, per, nw_lag(hold))
            row["通過"] = passes(row)
            record(name, "元", hold, row, s)
            base[name, hold] = (row, s)

    chosen = {}
    for name in sig:
        ok = [(min(base[name, h][0][k] for k in PERIODS), h) for h in triggers.HOLDING_DAYS if base[name, h][0]["通過"]]
        if ok:
            chosen[name] = max(ok)[1]
    buys = [n for n in chosen if n not in triggers.SELL_TRIGGERS]
    sells = [n for n in chosen if n in triggers.SELL_TRIGGERS]

    adopted: dict[str, dict] = {}
    if sells:
        sell_any = np.logical_or.reduce([raw[n] for n in sells])
        recent = lookback(sell_any)
        for name in buys:
            hold, stop, s0 = chosen[name], name not in triggers.REVERSAL, base[name, chosen[name]][1]
            for variant, kw in [("(b) 見送り", {"skip": recent}), ("(c) 早めの売り", {"sell": sell_any})]:
                row, s = summarize(simulate(m, [Leg(sig[name], hold, stop, **kw)]), m, per, nw_lag(hold))
                d = (s - s0).dropna()
                diff = by_period(d, per)
                row.update({"差": d.mean(), "差t": nw_t(d, nw_lag(hold)), **{f"差_{k}": v for k, v in diff.items()}})
                row["採用"] = row["差t"] >= MIN_T and all(v > 0 for v in diff.values())
                record(name, variant, hold, row, s)
                if row["採用"]:
                    adopted.setdefault(name, {}).update(kw)

    holding = {}
    if buys:
        lag = nw_lag(max(chosen[n] for n in buys))
        for variant, ad in [("元", {})] + ([("(b)(c) 採用込み", adopted)] if adopted else []):
            legs = [Leg(sig[n], chosen[n], n not in triggers.REVERSAL, **ad.get(n, {})) for n in buys]
            tr = simulate(m, legs).dropna(subset=["ex"])
            d, cnt = daily_excess(m, tr)
            s = (1 + d).groupby(d.index.to_period("M")).prod() - 1
            s = s[s.index.isin(per.index)]
            record("OR", variant, np.nan, {"件数": len(tr), "月数": len(s), "超過": s.mean(), "t": nw_t(s, lag),
                                           **by_period(s, per)}, s)
            holding[variant] = pd.Series(cnt[inper]).describe(percentiles=[0.5, 0.9])

    summary = pd.DataFrame(rows)
    summary.to_csv(out_dir / "summary.csv", index=False, encoding="utf-8-sig")
    pd.concat(months).to_csv(out_dir / "monthly.csv", index=False, encoding="utf-8-sig")

    pct = ["超過", "上位業種比率", "差"] + PERIODS + [f"差_{k}" for k in PERIODS]
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.max_rows", 500)
    pd.set_option("display.unicode.east_asian_width", True)
    print(summary.to_string(index=False, formatters={
        **{c: "{:+.2%}".format for c in pct if c in summary}, "t": "{:.2f}".format, "差t": "{:.2f}".format}))
    for k in PERIODS:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）" if len(mk) else f"{k}: なし")
    print("通過:", ", ".join(f"{n}({h}日)" for n, h in chosen.items()) if chosen else "なし")
    print("採用した (b)(c):", ", ".join(
        f"{n}({'+'.join('(b)' if k == 'skip' else '(c)' for k in kw)})" for n, kw in adopted.items()) if adopted else "なし")
    for variant, h in holding.items():
        print(f"OR {variant} の同時保有数: 平均 {h['mean']:.1f}・中央値 {h['50%']:.0f}・90% {h['90%']:.0f}・最大 {h['max']:.0f}")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
