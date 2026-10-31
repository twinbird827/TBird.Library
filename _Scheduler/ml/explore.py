"""rules.py の全ルールを月末形成・翌月末保有で評価し、事前に固定した選抜基準で候補を選ぶ。

形成月は DailyBars の最古月から 13 か月後〜最新の前月。月数で 3 等分して P1・P2・P3（端数は P3）。
選抜基準: (1) TX（上位 5 分位 − 全銘柄平均）の月平均が P1・P2・P3 のすべてで正、(2) LS（上位 − 下位 5 分位）も同様、
(3) 全期間の TX の t 値 2.0 以上、(4) 業種内順位版でも TX が P1・P2・P3 のすべてで正（業種単位のルールは対象外）。
通過したルールを 3 期間の TX の最小値の降順に並べ、月次 TX の相関が 0.7 を超える組は下位を落とし、上位 5 本を候補にする。
"""
from __future__ import annotations

import argparse
import sqlite3
from pathlib import Path

import numpy as np
import pandas as pd

import rules

FIRST_FORMATION = 13
MIN_STOCKS = 100
MIN_T = 2.0
MAX_CORR = 0.7
MAX_CANDIDATES = 5
PERIODS = ["P1", "P2", "P3"]


def t_stat(x: pd.Series) -> float:
    return x.mean() / x.std() * np.sqrt(len(x)) if len(x) > 2 else np.nan


def periods(me: pd.DatetimeIndex) -> pd.Series:
    """形成月 → P1/P2/P3。"""
    months = me[FIRST_FORMATION:len(me) - 1]
    b = len(months) // 3
    return pd.Series(["P1"] * b + ["P2"] * b + ["P3"] * (len(months) - 2 * b), index=months)


def monthly(p: rules.Panel, name: str, sector_ranked: bool) -> pd.DataFrame:
    rec = []
    for i in range(FIRST_FORMATION, len(p.me) - 1):
        fwd = p.pm.iloc[i + 1] / p.pm.iloc[i] - 1
        ok = p.eligible(i) & fwd.notna()
        s = p.score(name, i, ok)[ok].dropna()
        if sector_ranked:
            s = p.sector_rank(s, i).dropna()
        if len(s) < MIN_STOCKS:
            continue
        q = s.rank(pct=True)
        f = fwd[s.index]
        top_idx = q.index[q > 0.8]
        top, bot = f[top_idx].mean(), f[q <= 0.2].mean()
        max_sec = p.sec.iloc[i][top_idx].value_counts(normalize=True).max()
        rec.append((p.me[i], top - fwd[ok].mean(), top - bot, len(s), max_sec))
    return pd.DataFrame(rec, columns=["d", "tx", "ls", "n", "max_sec"]).set_index("d")


def summarize(r: pd.DataFrame, per: pd.Series) -> dict:
    row = {"月数": len(r), "銘柄数": r["n"].median() if len(r) else np.nan,
           "TX": r["tx"].mean(), "tTX": t_stat(r["tx"]), "LS": r["ls"].mean(),
           "上位業種比率": r["max_sec"].mean()}
    g = r.groupby(per.reindex(r.index))
    for k in PERIODS:
        x = g.get_group(k) if k in g.groups else r.iloc[:0]
        row[f"TX_{k}"] = x["tx"].mean()
        row[f"LS_{k}"] = x["ls"].mean()
    return row


def select(summary: pd.DataFrame, series: dict[str, pd.Series]) -> tuple[pd.DataFrame, list[str]]:
    base = summary[summary["variant"] == "raw"].set_index("rule")
    sr = summary[summary["variant"] == "sector_rank"].set_index("rule")
    tx = base[[f"TX_{k}" for k in PERIODS]]
    ls = base[[f"LS_{k}" for k in PERIODS]]
    srx = sr.reindex(base.index)[[f"TX_{k}" for k in PERIODS]]
    crit = pd.DataFrame({
        "c1": (tx > 0).all(axis=1),
        "c2": (ls > 0).all(axis=1),
        "c3": base["tTX"] >= MIN_T,
        "c4": pd.Series(base.index.isin(rules.SECTOR_LEVEL), index=base.index) | (srx > 0).all(axis=1),
    })
    crit["minTX"] = tx.min(axis=1)
    passed = crit[crit[["c1", "c2", "c3", "c4"]].all(axis=1)].sort_values("minTX", ascending=False)
    chosen: list[str] = []
    for name in passed.index:
        if all(series[name].corr(series[c]) <= MAX_CORR for c in chosen):
            chosen.append(name)
        if len(chosen) == MAX_CANDIDATES:
            break
    return crit, chosen


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore"
    out_dir.mkdir(exist_ok=True)
    conn = sqlite3.connect(f"{db_path.as_uri()}?mode=ro", uri=True)
    try:
        p = rules.Panel(conn)
    finally:
        conn.close()
    per = periods(p.me)

    rows, months, series = [], [], {}
    for name in rules.RULES:
        variants = [("raw", False)] if name in rules.SECTOR_LEVEL else [("raw", False), ("sector_rank", True)]
        for variant, ranked in variants:
            r = monthly(p, name, ranked)
            rows.append({"rule": name, "variant": variant, **summarize(r, per)})
            months.append(r.reset_index().assign(rule=name, variant=variant))
            if variant == "raw":
                series[name] = r["tx"]
    summary = pd.DataFrame(rows)
    crit, chosen = select(summary, series)
    summary = summary.merge(crit.reset_index().assign(variant="raw"), on=["rule", "variant"], how="left")

    pd.concat(months).to_csv(out_dir / "monthly.csv", index=False, encoding="utf-8-sig")
    summary.to_csv(out_dir / "summary.csv", index=False, encoding="utf-8-sig")

    pct = ["TX", "LS", "上位業種比率", "minTX"] + [f"{m}_{k}" for m in ("TX", "LS") for k in PERIODS]
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    print(summary.to_string(index=False, formatters={
        **{c: "{:+.2%}".format for c in pct}, "tTX": "{:.2f}".format, "銘柄数": "{:.0f}".format}))
    for k in PERIODS:
        m = per[per == k].index
        print(f"{k}: {m.min():%Y-%m}〜{m.max():%Y-%m}（{len(m)} か月）" if len(m) else f"{k}: なし")
    print("候補:", ", ".join(chosen) if chosen else "なし")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
