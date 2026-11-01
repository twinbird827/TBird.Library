"""封印期間（約定日が dataset.SEAL_END より後）を開け、#414 で固定した判定方法どおりに preseal.py の和集合を 1 回だけ確かめる。

シミュレーションは探索期間の最初の週から DB の最後の日まで続けて回し、約定・持ち分・要約・(5) は port.py と同じ。(5) の乱数は preseal.py の和集合と同じ種。
評価は約定日が SEAL_END より後の最初の週 k0 から最後までで、区間は始まりの約定日の月に入れる（最後の途中の月も含める）。
合否（和集合・片道 hold.COST）: (1) に対する月平均の超過が正で、(5) の 50 回の月平均の超過の 95% 点以上。D1・D2 には分けない。
片道 0.3% の要約・(5) の 95% 点、暦年ごとの超過、候補 3 本それぞれの要約は合否に使わない記述。
同じ実行の探索期間の部分（週 TRANCHES〜k0）の和集合の要約は、preseal.py の和集合と一致するかの確かめに出す。
封印期間を読むのはこのスクリプトだけで、探索の表のキャッシュ（dataset.load）は使わない。DB は読み取り専用で開く。
"""
from __future__ import annotations

import argparse
import sqlite3
import time
import warnings
from pathlib import Path

import numpy as np
import pandas as pd

import dataset
import explore_triggers as et
import hold
import port
import preseal
import triggers

COSTS = [hold.COST, 0.003]
UNION = preseal.UNION
NULL = f"(5) {UNION}"


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "seal"
    t0 = time.perf_counter()
    conn = sqlite3.connect(f"{db_path.as_uri()}?mode=ro", uri=True)
    try:
        df = dataset.build(conn, None)
        p = triggers.DailyPanel(conn, None)
    finally:
        conn.close()
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        m = et.Market(p)
    pns = {c: hold.build(df, m, p.codes, f) for c, f in preseal.CANDIDATES.items()}
    del df, m, p
    pn0 = pns[UNION]
    k0 = int(np.searchsorted(pn0.dates, dataset.SEAL_END, side="right"))
    ps, px = port.evaluated(pn0, k0), port.evaluated(pn0, port.TRANCHES, k0)
    bs, pers = hold.bench(ps), hold.halves(ps)
    bx, perx = hold.bench(px), hold.halves(px)
    t1 = time.perf_counter()

    buy = pn0.base & pn0.univ & ~np.isnan(pn0.O)
    ci = list(preseal.CANDIDATES).index(UNION)
    null_buys = [port.random_buy(pn0, buy.sum(1), np.random.default_rng((port.SEED, ci, i)))
                 for i in range(1, port.N_RANDOM + 1)]
    runs = {cost: port.simulate(pn0, buy, cost=cost) for cost in COSTS}
    nulls = {cost: [port.simulate(pn0, nb, cost=cost) for nb in null_buys] for cost in COSTS}
    cands = {c: port.simulate(pn, pn.base & pn.univ & ~np.isnan(pn.O)) for c, pn in pns.items() if c != UNION}
    t2 = time.perf_counter()

    def sealed(rs: list[tuple[hold.Run, np.ndarray]]) -> dict:
        return hold.summarize([port.trimmed(*r, k0) for r in rs], ps, bs, pers)

    explored = hold.summarize([port.trimmed(*runs[hold.COST], port.TRANCHES, k0)], px, bx, perx)
    rows = [{"期間": "探索期間の部分", "片道のコスト": hold.COST, "戦略": UNION} | explored]
    verdict = {}
    for cost in COSTS:
        body = sealed([runs[cost]])
        ex = [sealed([r])["超過"] for r in nulls[cost]]
        thr = float(np.quantile(ex, port.NULL_PCT))
        judged = {}
        if cost == hold.COST:
            judged = {"合否": "合格" if body["超過"] > 0 and body["超過"] >= thr else "不合格"}
        verdict[cost] = (thr, ex, body, judged)
        rows += [{"期間": "封印期間", "片道のコスト": cost, "戦略": UNION} | body | {"(5) の 95% 点": thr} | judged,
                 {"期間": "封印期間", "片道のコスト": cost, "戦略": NULL} | sealed(nulls[cost])]
    summ = pd.DataFrame(rows)

    year_rows = []
    for name, rs in [(UNION, [runs[hold.COST]]), (NULL, nulls[hold.COST])]:
        ex = preseal.excess([port.trimmed(*r, k0) for r in rs], ps, bs)
        year_rows += [{"戦略": name, "年": y, "超過": g.mean(), "月数": len(g)} for y, g in ex.groupby(ex.index.year)]
    yearly = pd.DataFrame(year_rows)
    cand_df = pd.DataFrame([{"戦略": c} | sealed([r]) for c, r in cands.items()])

    out_dir.mkdir(parents=True, exist_ok=True)
    summ.to_csv(out_dir / "summary.csv", index=False, encoding="utf-8-sig")
    yearly.to_csv(out_dir / "yearly.csv", index=False, encoding="utf-8-sig")
    cand_df.to_csv(out_dir / "candidates.csv", index=False, encoding="utf-8-sig")

    pct = "{:+.2%}".format
    fmt = ({c: pct for c in ["超過", "D1", "D2", "年率", "(5) の 95% 点"]}
           | {"t": "{:.2f}".format, "回転率": "{:.2f}".format, "保有期間の中央値": "{:.0f}".format,
              "片道のコスト": "{:.2%}".format})
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    print(f"シミュレーション: 約定日 {pn0.dates[0].date()}〜{pn0.dates[-1].date()}（{len(pn0.dates)} 週）")
    print(f"封印期間の評価: 約定日 {ps.dates[0].date()}〜{ps.dates[-1].date()}（{len(ps.dates)} 週・{len(pers)} か月）")
    print(f"探索期間の部分: 約定日 {px.dates[0].date()}〜{px.dates[-1].date()}（{len(px.dates)} 週・{len(perx)} か月）")
    print("要約（超過は (1) に対する月平均、(5) は 50 回の平均。回転率は年あたり、保有期間は営業日。D1・D2 は合否に使わない）:")
    print(summ.to_string(index=False, formatters=fmt, na_rep=""))
    for cost, (thr, ex, body, judged) in verdict.items():
        print(f"封印期間 片道 {cost:.2%} {UNION}: 超過 {body['超過']:+.2%}・(5) の 95% 点 {thr:+.2%}"
              f"（中央値 {np.median(ex):+.2%}・最大 {np.max(ex):+.2%}）"
              + (f" → {judged['合否']}" if judged else "（合否に使わない）"))
    print("合否に使わない内訳: 暦年ごとの超過（片道 0.15%。(1) に対する月平均、(5) は 50 回の平均の系列）:")
    print(yearly.pivot(index="戦略", columns="年", values="超過").reindex([UNION, NULL]).to_string(float_format=pct))
    print("合否に使わない内訳: 候補 3 本それぞれの封印期間の要約（片道 0.15%）:")
    print(cand_df.to_string(index=False, formatters=fmt))
    print(f"実行時間: データ {t1 - t0:.0f} 秒・シミュレーション {t2 - t1:.0f} 秒")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
