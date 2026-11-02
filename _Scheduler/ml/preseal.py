"""封印期間を開ける前に、port.py の候補 3 本とその条件の和集合の合格が、時期の偏り・コスト・小型株の効果で説明できないかを探索期間で確かめる。
"""
from __future__ import annotations

import argparse
import time
from pathlib import Path

import numpy as np
import pandas as pd

import hold
import port

UNION = "和集合"
CANDIDATES = port.CANDIDATES | {UNION: lambda r: port.quintile(r, "ret250", 1) & (
    port.quintile(r, "roa_op", 4) | port.quintile(r, "psr", 2) | port.quintile(r, "per", 3))}
COSTS = [hold.COST, 0.003, 0.005]
CRASH = pd.period_range("2020-03", "2020-12", freq="M")
CRASH_LABEL = "2020-03〜2020-12 を除く"


def excess(runs: list[hold.Run], pn: hold.Panel, b: np.ndarray) -> pd.Series:
    """runs の月ごとの (1) に対する超過を平均した系列（hold.summarize の超過と同じ）。"""
    bm = hold.monthly(b, pn)
    return pd.concat([hold.monthly(r.nav[1:] / r.nav[:-1] - 1, pn) - bm for r in runs], axis=1).mean(axis=1)


def size_buy(pn: hold.Panel, buy: np.ndarray, size: list[np.ndarray], rng: np.random.Generator) -> np.ndarray:
    """(6): 週 k の buy を size の各層（時価総額の分位と欠損の、k × j の母集団のマスク）で数え、同じ層から同じ件数を乱数で選んだマスク。"""
    m = np.zeros_like(buy)
    for s in size:
        m |= port.random_buy(pn._replace(univ=pn.univ & s), (buy & s).sum(1), rng)
    return m


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "preseal"
    t0 = time.perf_counter()
    df, m, codes = hold.load(db_path)
    pns = {c: hold.build(df, m, codes, f) for c, f in CANDIDATES.items()}
    qs = [hold.build(df, m, codes, lambda r, n=n: port.quintile(r, "mcap", n)).base for n in range(1, 6)]
    del df, m
    pn0 = pns[UNION]
    size = qs + [pn0.univ & ~np.any(qs, axis=0)]
    pe = port.evaluated(pn0)
    b, per = hold.bench(pe), hold.halves(pe)
    t1 = time.perf_counter()

    runs, nulls, sized, buys = {}, {}, {}, {}
    for ci, (c, pn) in enumerate(pns.items()):
        buys[c] = buy = pn.base & pn.univ & ~np.isnan(pn.O)
        null_buys = [port.random_buy(pn, buy.sum(1), np.random.default_rng((port.SEED, ci, i)))
                     for i in range(1, port.N_RANDOM + 1)]
        for cost in COSTS:
            runs[c, cost] = port.simulate(pn, buy, cost=cost)
            nulls[c, cost] = [port.simulate(pn, nb, cost=cost) for nb in null_buys]
        sized[c] = [port.simulate(pn, size_buy(pn, buy, size, np.random.default_rng((port.SEED, 6, ci, i))))
                    for i in range(1, port.N_RANDOM + 1)]
    t2 = time.perf_counter()

    def summ(rs: list[tuple[hold.Run, np.ndarray]]) -> dict:
        return hold.summarize([port.trimmed(*r) for r in rs], pe, b, per)

    def null95(rs: list[tuple[hold.Run, np.ndarray]]) -> tuple[float, list[float]]:
        ex = [summ([r])["超過"] for r in rs]
        return float(np.quantile(ex, port.NULL_PCT)), ex

    year_rows = []
    for c in CANDIDATES:
        for name, rs in [(c, [runs[c, hold.COST]]), (f"(5) {c}", nulls[c, hold.COST])]:
            ex = excess([port.trimmed(*r) for r in rs], pe, b)
            year_rows += [{"戦略": name, "期間": str(y), "超過": g.mean(), "月数": len(g)}
                          for y, g in ex.groupby(ex.index.year)]
            kept = ex[~ex.index.isin(CRASH)]
            year_rows.append({"戦略": name, "期間": CRASH_LABEL, "超過": kept.mean(), "月数": len(kept)})
    yearly = pd.DataFrame(year_rows)

    cost_rows, verdict = [], {}
    for cost in COSTS:
        for c in CANDIDATES:
            body = summ([runs[c, cost]])
            thr, ex = null95(nulls[c, cost])
            passed = body["D1"] > 0 and body["D2"] > 0 and body["超過"] >= thr
            verdict[c, cost] = (thr, ex, passed, body)
            cost_rows += [{"片道のコスト": cost, "戦略": c} | body
                          | {"(5) の 95% 点": thr, "合否": "合格" if passed else "不合格"},
                          {"片道のコスト": cost, "戦略": f"(5) {c}"} | summ(nulls[c, cost])]
    cost_df = pd.DataFrame(cost_rows)

    labels = [f"Q{n}" for n in range(1, 6)] + ["欠損"]
    size_rows = []
    for c in CANDIDATES:
        thr, _ = null95(sized[c])
        bw = buys[c][port.TRANCHES:]
        size_rows += [{"戦略": c} | summ([runs[c, hold.COST]]) | {"(6) の 95% 点": thr}
                      | {f"時価総額 {lb}": (bw & s[port.TRANCHES:]).sum() / bw.sum() for lb, s in zip(labels, size)},
                      {"戦略": f"(6) {c}"} | summ(sized[c])]
    size_df = pd.DataFrame(size_rows)

    out_dir.mkdir(parents=True, exist_ok=True)
    yearly.to_csv(out_dir / "yearly.csv", index=False, encoding="utf-8-sig")
    cost_df.to_csv(out_dir / "cost.csv", index=False, encoding="utf-8-sig")
    size_df.to_csv(out_dir / "size.csv", index=False, encoding="utf-8-sig")

    pct = "{:+.2%}".format
    fmt = ({c: pct for c in ["超過", "D1", "D2", "年率", "(5) の 95% 点", "(6) の 95% 点"]}
           | {f"時価総額 {lb}": "{:.0%}".format for lb in labels}
           | {"t": "{:.2f}".format, "回転率": "{:.2f}".format, "保有期間の中央値": "{:.0f}".format,
              "片道のコスト": "{:.2%}".format})
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    print(f"評価期間: 約定日 {pe.dates[0].date()}〜{pe.dates[-1].date()}（{len(pe.dates)} 週・{len(per)} か月）")
    for k in ["D1", "D2"]:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）")
    print("時期の偏り（片道 0.15%。(1) に対する月平均の超過、(5) は 50 回の平均の系列）:")
    print(yearly.pivot(index="戦略", columns="期間", values="超過").reindex(yearly["戦略"].unique())
          .to_string(float_format=pct))
    print("コスト（超過は (1) に対する月平均、(5) は 50 回の平均。回転率は年あたり、保有期間は営業日）:")
    print(cost_df.to_string(index=False, formatters=fmt, na_rep=""))
    for (c, cost), (thr, ex, passed, body) in verdict.items():
        print(f"片道 {cost:.2%} {c}: (5) の 95% 点 {thr:+.2%}（中央値 {np.median(ex):+.2%}・最大 {np.max(ex):+.2%}）"
              f" → {'合格' if passed else '不合格'}（D1 {body['D1']:+.2%}・D2 {body['D2']:+.2%}・超過 {body['超過']:+.2%}）")
    print("小型株の効果（片道 0.15%。(6) は時価総額の分位を揃えた無作為な選択の 50 回の平均、時価総額の分位は評価期間の候補の銘柄の割合）:")
    print(size_df.to_string(index=False, formatters=fmt, na_rep=""))
    print(f"実行時間: データ {t1 - t0:.0f} 秒・シミュレーション {t2 - t1:.0f} 秒")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
