"""イベント（開示・修正）で立つ買いのトリガーについて、イベントの後の値動きを記述する（トリガーの選抜には使わない）。

イベントは、トリガーの真偽表が真で、トリガー日 t に母集団の条件を満たし、t の月が explore_triggers.periods の月に入る (日, 銘柄)。
保有中の再トリガーも数え、コストと損切りは入れない。値動きは調整後の価格で、比べる相手は t の母集団（t+1 の始値のある銘柄）の
同じ区間の等金額平均。取れない部分は t の前日の終値 → t の終値（当日）と t の終値 → t+1 の始値（窓開け）、取れる部分は
t+1 の始値 → t+h の終値。押し目は t+1 の始値 × (1 − n) の指値に t+1〜t+k の安値が触れたら約定する。
t+1 の始値が無いイベントは件数だけ数えて他の集計から外す。集計は、トリガー日の月ごとの平均の、月をまたいだ平均と
Newey-West の t 値と、P1〜P3 ごとの月平均（出力は explore/events/）。
"""
from __future__ import annotations

import argparse
import sqlite3
import warnings
from pathlib import Path

import numpy as np
import pandas as pd

import explore_triggers as et
import triggers

CAP_HOLDS = [5, 20, 60, 120, 240]
# 5 日は押し目を待つ k 日と重なるので出さない。
DIP_HOLDS = [20, 60, 120, 240]
GAP_HOLDS = [5, 20, 60]
DIPS = [0.03, 0.05, 0.10]
DIP_DAYS = 5
PATH_DAYS = 60
# 寄らなかったイベントが、t+2〜t+1+NO_OPEN_DAYS に寄ったかを数える。
NO_OPEN_DAYS = 5
GAP_BINS = 5
_CODES = {"E1", "E2", "E3", "E4", "C2", "C3", "C6", *(f"C{i}" for i in range(9, 17)), *(f"F{i}" for i in range(1, 6))}
# 窓開けが起きるのは開示の翌日なので、価格のトリガーは取れない部分の定義が成り立たず対象にしない。
EVENT_TRIGGERS = [n for n in triggers.TRIGGERS | triggers.COMBO_TRIGGERS if n.split()[0] in _CODES]
PATH_LABELS = ["t の終値", "t+1 の始値", *(f"t+{d}" for d in range(1, PATH_DAYS + 1))]


def bench_paths(m: et.Market) -> tuple[np.ndarray, np.ndarray]:
    """t の母集団の、t の前日の終値からの累積リターン bp[t, i]（列は PATH_LABELS）と、窓開けのリターン bgap[t]。"""
    bp = np.full((m.n, len(PATH_LABELS)), np.nan)
    bgap = np.full(m.n, np.nan)
    with warnings.catch_warnings(), np.errstate(all="ignore"):
        warnings.simplefilter("ignore", RuntimeWarning)
        for t in range(1, m.n - 1):
            js = np.flatnonzero(m.elig[t] & ~np.isnan(m.O[t + 1]))
            if not len(js):
                continue
            e = min(t + PATH_DAYS, m.n - 1)
            base = m.C[t - 1, js]
            bp[t, 0] = np.nanmean(m.C[t, js] / base - 1)
            bp[t, 1] = np.nanmean(m.O[t + 1, js] / base - 1)
            bp[t, 2:2 + e - t] = np.nanmean(m.C[t + 1:e + 1, js] / base - 1, axis=1)
            bgap[t] = np.nanmean(m.O[t + 1, js] / m.C[t, js] - 1)
    return bp, bgap


def segments(m: et.Market, bp: np.ndarray, bgap: np.ndarray, t: np.ndarray, j: np.ndarray
             ) -> tuple[dict, np.ndarray]:
    """イベント (t, j) ごとの超過リターン（当日・窓開け・取れる部分の h 日）と、累積の超過リターンの経路（列は PATH_LABELS）。"""
    cp, c0, o1 = m.C[t - 1, j], m.C[t, j], m.O[t + 1, j]
    seg = {"当日": c0 / cp - 1 - bp[t, 0], "窓開け": o1 / c0 - 1 - bgap[t]}
    for h in CAP_HOLDS:
        seg[h] = m.C[t + h, j] / o1 - 1 - m.bench[t, h]
    close = m.C[t[:, None] + np.arange(1, PATH_DAYS + 1), j[:, None]]
    path = np.c_[c0, o1, close] / cp[:, None] - 1 - bp[t]
    return seg, path


def dip_fill(o: np.ndarray, l: np.ndarray, t: np.ndarray, j: np.ndarray, n: float, k: int = DIP_DAYS
             ) -> tuple[np.ndarray, np.ndarray]:
    """t+1 の始値 × (1 − n) の指値が t+1〜t+k に約定した日（t+1 が 0、約定しなければ −1）と価格。o・l は行が営業日の始値・安値。

    その日の始値がすでに指値以下なら始値で約定する。基準が t+1 の始値なので、t+1 の安値は必ず始値より後に付き、当日も約定に数える。
    """
    rows, cols = t[:, None] + np.arange(1, k + 1), j[:, None]
    ow, lw = o[rows, cols], l[rows, cols]
    lim = ow[:, 0] * (1 - n)
    hit = lw <= lim[:, None]
    day = np.where(hit.any(axis=1), hit.argmax(axis=1), -1)
    px = np.where(day >= 0, np.fmin(ow[np.arange(len(t)), day], lim), np.nan)
    return day, px


def stats(t: np.ndarray, v: np.ndarray, m: et.Market, per: pd.Series, lag: int) -> dict:
    """イベントごとの値 v を、トリガー日の月ごとに平均した系列の要約。"""
    s = pd.Series(v, index=m.dates[t].to_period("M")).dropna()
    mo = s.groupby(level=0).mean()
    return {"件数": len(s), "月数": len(mo), "超過": mo.mean(), "t": et.nw_t(mo, lag), **et.by_period(mo, per)}


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "events"
    out_dir.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(f"{db_path.as_uri()}?mode=ro", uri=True)
    try:
        p = triggers.DailyPanel(conn)
    finally:
        conn.close()
    trig = triggers.TRIGGERS | triggers.COMBO_TRIGGERS
    raw = {n: trig[n](p).to_numpy(bool) for n in EVENT_TRIGGERS}
    m = et.Market(p)
    del p
    per = et.periods(m.dates)
    inper = m.dates.to_period("M").isin(per.index)
    bp, bgap = bench_paths(m)
    # 約定の日の始値 → t+h の終値の比べる相手は、b_oc と b_cc の連結で近似する。
    gcc = np.cumprod(1 + np.nan_to_num(m.b_cc))

    dec, path, dip, gap, top = [], [], [], [], []
    for name, r in raw.items():
        t, j = np.nonzero(r & m.elig & inper[:, None])
        opened = ~np.isnan(m.O[t + 1, j])
        tn, jn = t[~opened], j[~opened]
        later = np.isfinite(m.O[tn[:, None] + np.arange(2, 2 + NO_OPEN_DAYS), jn[:, None]]).any(axis=1)
        t, j = t[opened], j[opened]
        seg, pth = segments(m, bp, bgap, t, j)
        seg["取れない部分"] = seg["当日"] + seg["窓開け"]

        row = {"trigger": name, "イベント": len(t) + len(tn), "寄らず": len(tn), "うち後で寄った": int(later.sum())}
        for key in ["当日", "窓開け", "取れない部分", *CAP_HOLDS]:
            hold = key if isinstance(key, int) else np.nan
            label = f"取れる部分 {key}日" if isinstance(key, int) else key
            st = stats(t, seg[key], m, per, et.nw_lag(key if isinstance(key, int) else 1))
            dec.append({"trigger": name, "区間": label, "hold": hold, **st})
            row[label] = st["超過"]
        top.append(row)

        for i, label in enumerate(PATH_LABELS):
            path.append({"trigger": name, "点": label, "i": i, **stats(t, pth[:, i], m, per, et.nw_lag(max(i - 1, 1)))})

        for n in DIPS:
            day, px = dip_fill(m.O, m.L, t, j, n)
            filled = day >= 0
            d = t + 1 + np.maximum(day, 0)
            for h in DIP_HOLDS:
                e = t + h
                ex = m.C[e, j] / px - 1 - ((1 + m.b_oc[d]) * gcc[e] / gcc[d] - 1)
                for status, mask, v in [("約定", filled, ex), ("不約定", ~filled, seg[h]),
                                        ("全件", np.ones(len(t), bool), seg[h])]:
                    dip.append({"trigger": name, "n": n, "hold": h, "約定": status, "イベント数": int(mask.sum()),
                                "約定率": filled.mean(), **stats(t[mask], v[mask], m, per, et.nw_lag(h))})

        q = pd.qcut(seg["窓開け"], GAP_BINS, labels=False, duplicates="drop")
        for b in range(GAP_BINS):
            mask = q == b
            for h in GAP_HOLDS:
                gap.append({"trigger": name, "分位": b + 1, "窓開けの平均": np.nanmean(seg["窓開け"][mask]),
                            "hold": h, **stats(t[mask], seg[h][mask], m, per, et.nw_lag(h))})

    for fname, rows in [("decompose", dec), ("path", path), ("dip", dip), ("gap", gap)]:
        pd.DataFrame(rows).to_csv(out_dir / f"{fname}.csv", index=False, encoding="utf-8-sig")

    summary = pd.DataFrame(top)
    pct = [c for c in summary if c not in ("trigger", "イベント", "寄らず", "うち後で寄った")]
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    print(summary.to_string(index=False, formatters={c: "{:+.2%}".format for c in pct}))
    for k in et.PERIODS:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）" if len(mk) else f"{k}: なし")
    print(f"試行数: 押し目 {len(DIPS)} 通り × {len(DIP_HOLDS)} 保有期間 × {len(raw)} 本 = "
          f"{len(DIPS) * len(DIP_HOLDS) * len(raw)}（記述で、選抜に使わない）")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
