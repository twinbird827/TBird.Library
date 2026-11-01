"""総当たりの ret250 Q1 系の候補 3 本を、12 本の持ち分を毎週 1 本ずつ入れ替える（保有 12 週）ポートフォリオにしてシミュレーションする。

資産を 12 本の持ち分に分け、最初は各 1/12 の現金にする。形成日の通し番号 k の週は、持ち分 k mod 12 を、週 k の条件を満たし約定日の始値がある銘柄の等金額に組み替える。
条件から外れた銘柄は全部売り（満期）、引き続き条件を満たす銘柄は目標の金額との差額だけを売買し（調整）、持っていない銘柄は目標の金額まで買う（新規）。
目標の金額は、組み替えの売買とコストの後に持ち分の現金がちょうど 0 になる等金額。候補が 0 件の週は、その持ち分を次の番まで現金で置く。
母集団から外れた銘柄はその週に売り、現金はその持ち分に残す。分位は scan.quintiles と同じで、欠損の銘柄は条件を満たさない。
約定・資産の評価・コスト・(1) 全銘柄の等金額・月ごとの超過・D1/D2 は hold.py と同じ。要約・合否は全持ち分が埋まった週 12 以降の約定日だけで出す。
比べる相手 (5): 週 k に母集団で約定日の始値がある銘柄から、その週の候補の数と同じ件数を乱数で選び、同じ持ち分・保有の形で 50 回回す。
合否（候補ごと）: 月平均の超過が D1・D2 ともに正で、その候補の (5) の 50 回の月平均の超過の 95% 点以上。
DB は読み取り専用で開き、封印期間の価格は読まない。
"""
from __future__ import annotations

import argparse
import time
from pathlib import Path

import numpy as np
import pandas as pd

import hold

TRANCHES = 12
N_RANDOM = 50
NULL_PCT = 0.95
SEED = 414


def quintile(r: pd.DataFrame, f: str, n: int) -> pd.Series:
    """順位 r の指標 f が第 n 5 分位（1〜5）か。切り方は scan.quintiles と同じで、欠損は偽。"""
    return (sum((r[f] > b).astype(int) for b in (0.2, 0.4, 0.6, 0.8)) == n - 1) & r[f].notna()


CANDIDATES = {
    "ret250 Q1 × roa_op Q4": lambda r: quintile(r, "ret250", 1) & quintile(r, "roa_op", 4),
    "ret250 Q1 × psr Q2": lambda r: quintile(r, "ret250", 1) & quintile(r, "psr", 2),
    "ret250 Q1 × per Q3": lambda r: quintile(r, "ret250", 1) & quintile(r, "per", 3),
}


def target(v: np.ndarray, w: float, cost: float = hold.COST) -> float:
    """Σ spend(T − v) = w となる等金額 T（v は組み替えの前の各銘柄の金額、w は持ち分の現金。spend は買いに 1 + cost、売りに 1 − cost を掛けた現金の出）。

    spend は T について単調な区分線形で、折れ点は v なので、w を挟む折れ点の区間で直線を解く。
    """
    def spend(t):
        d = t - v
        return np.where(d > 0, d * (1 + cost), d * (1 - cost)).sum(-1)

    b = np.sort(v)
    i = int(np.searchsorted(spend(b[:, None]), w))
    x0 = b[max(i - 1, 0)]
    return x0 + (w - spend(x0)) / ((1 + cost) * i + (1 - cost) * (len(b) - i))


def simulate(pn: hold.Panel, buy: np.ndarray, record: bool = False,
             cost: float = hold.COST) -> tuple[hold.Run, np.ndarray]:
    """buy[k] の銘柄で週 k に持ち分 k mod 12 を組み替える（片道のコストは cost）。2 つ目は Run.holds の各件を売った週 k。"""
    K, J = pn.univ.shape
    q, bought = np.zeros((TRANCHES, J)), np.zeros((TRANCHES, J), int)
    cash = np.full(TRANCHES, 1 / TRANCHES)
    nav, cash_ratio, traded, held_n = np.zeros(K), np.zeros(K), np.zeros(K), np.zeros(K, int)
    holds, hold_k, trades = [], [], []

    def sell(k: int, i: int, s: np.ndarray, why: str) -> None:
        v = q[i, s] * pn.P[k, s]
        days = pn.a[k] - bought[i, s]
        cash[i] += v.sum() * (1 - cost)
        traded[k] += v.sum()
        holds.extend(days.tolist())
        hold_k.extend([k] * len(days))
        if record:
            trades.extend({"形成日": pn.fd[k], "約定日": pn.dates[k], "持ち分": i, "銘柄": pn.codes[j], "売買": "売り",
                           "金額": vv, "理由": why, "保有期間": dd} for j, vv, dd in zip(np.flatnonzero(s), v, days))
        q[i, s] = 0

    for k in range(K):
        px, hj = pn.P[k], (q > 0).any(0)
        assert np.isfinite(px[hj]).all()
        nav[k] = cash.sum() + q[:, hj].sum(0) @ px[hj]
        for i in range(TRANCHES):
            sell(k, i, (q[i] > 0) & ~pn.univ[k], "母集団外")
        i = k % TRANCHES
        s = buy[k] & pn.univ[k] & ~np.isnan(pn.O[k])
        sell(k, i, (q[i] > 0) & ~s, "満期")
        if s.any():
            js = np.flatnonzero(s)
            v = q[i, js] * pn.O[k, js]
            t = target(v, cash[i], cost)
            new = v == 0
            traded[k] += np.abs(t - v).sum()
            bought[i, js[new]] = pn.a[k]
            q[i, js] = t / pn.O[k, js]
            cash[i] = 0.0
            if record:
                trades.extend({"形成日": pn.fd[k], "約定日": pn.dates[k], "持ち分": i, "銘柄": pn.codes[j],
                               "売買": "買い" if d > 0 else "売り", "金額": abs(d), "理由": "新規" if nw else "調整",
                               "保有期間": np.nan} for j, d, nw in zip(js, t - v, new) if d != 0)
        hj = (q > 0).any(0)
        held_n[k] = hj.sum()
        cash_ratio[k] = cash.sum() / (cash.sum() + q[:, hj].sum(0) @ px[hj])
    run = hold.Run(nav, cash_ratio, held_n, traded, np.zeros(K, int), holds, (pn.a[-1] - bought[q > 0]).tolist(), trades)
    return run, np.array(hold_k, int)


def random_buy(pn: hold.Panel, n: np.ndarray, rng: np.random.Generator) -> np.ndarray:
    """週 k に、母集団で約定日の始値がある銘柄から n[k] 件を乱数で選んだマスク。"""
    pool = pn.univ & ~np.isnan(pn.O)
    m = np.zeros_like(pool)
    for k, nk in enumerate(n):
        m[k, rng.choice(np.flatnonzero(pool[k]), nk, replace=False)] = True
    return m


def evaluated(pn: hold.Panel, w: int = TRANCHES, e: int | None = None) -> hold.Panel:
    """週 w 以上 e 未満の Panel。"""
    return pn._replace(fd=pn.fd[w:e], dates=pn.dates[w:e], a=pn.a[w:e], univ=pn.univ[w:e], base=pn.base[w:e],
                       reasons=pn.reasons[:, w:e], O=pn.O[w:e], P=pn.P[w:e])


def trimmed(run: hold.Run, hold_k: np.ndarray, w: int = TRANCHES, e: int | None = None) -> hold.Run:
    """週 w 以上 e 未満に切り詰め、資産と売買の金額を週 w の資産で割る（hold.summarize は nav[0] == 1 を前提にする）。"""
    n0, e_ = run.nav[w], len(run.nav) if e is None else e
    return run._replace(nav=run.nav[w:e] / n0, cash=run.cash[w:e], held=run.held[w:e], traded=run.traded[w:e] / n0,
                        n_reason=run.n_reason[w:e], holds=[h for h, k in zip(run.holds, hold_k) if w <= k < e_])


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "port"
    t0 = time.perf_counter()
    df, m, codes = hold.load(db_path)
    pns = {c: hold.build(df, m, codes, f) for c, f in CANDIDATES.items()}
    del df, m
    pn0 = next(iter(pns.values()))
    pe = evaluated(pn0)
    b, per = hold.bench(pe), hold.halves(pe)
    t1 = time.perf_counter()

    runs, nulls, counts = {}, {}, {}
    for ci, (c, pn) in enumerate(pns.items()):
        buy = pn.base & pn.univ & ~np.isnan(pn.O)
        counts[c] = buy.sum(1)
        runs[c] = simulate(pn, buy, record=True)
        nulls[c] = [simulate(pn, random_buy(pn, counts[c], np.random.default_rng((SEED, ci, i))))
                    for i in range(1, N_RANDOM + 1)]
    t2 = time.perf_counter()

    rows, null_rows, verdict = [], [], {}
    for c in CANDIDATES:
        body = hold.summarize([trimmed(*runs[c])], pe, b, per)
        ex = [hold.summarize([trimmed(*r)], pe, b, per)["超過"] for r in nulls[c]]
        thr = float(np.quantile(ex, NULL_PCT))
        passed = body["D1"] > 0 and body["D2"] > 0 and body["超過"] >= thr
        verdict[c] = (thr, ex, passed, body)
        rows += [{"戦略": c} | body | {"候補の数の中央値": np.median(counts[c][TRANCHES:]),
                                       "合否": "合格" if passed else "不合格"},
                 {"戦略": f"(5) {c}"} | hold.summarize([trimmed(*r) for r in nulls[c]], pe, b, per)]
        null_rows += [{"候補": c, "回": i, "超過": e} for i, e in enumerate(ex, 1)]
    summ = pd.DataFrame(rows + [{"戦略": "(1) 全銘柄の等金額", "年率": np.prod(1 + b) ** (1 / hold.years(pe)) - 1}])

    def frame(name: str, s: int, r: hold.Run, n: np.ndarray | float) -> pd.DataFrame:
        return pd.DataFrame({"形成日": pn0.fd, "約定日": pn0.dates, "戦略": name, "回": s, "資産": r.nav,
                             "現金の比率": r.cash, "銘柄数": r.held, "候補の数": n})

    nav = pd.concat([frame(c, 0, runs[c][0], counts[c]) for c in CANDIDATES]
                    + [frame(f"(5) {c}", i, r, np.nan) for c in CANDIDATES for i, (r, _) in enumerate(nulls[c], 1)])
    trades = pd.DataFrame([{"候補": c} | t for c in CANDIDATES for t in runs[c][0].trades])
    out_dir.mkdir(parents=True, exist_ok=True)
    nav.to_csv(out_dir / "nav.csv", index=False, encoding="utf-8-sig")
    trades.to_csv(out_dir / "trades.csv", index=False, encoding="utf-8-sig")
    summ.to_csv(out_dir / "summary.csv", index=False, encoding="utf-8-sig")
    pd.DataFrame(null_rows).to_csv(out_dir / "null.csv", index=False, encoding="utf-8-sig")

    pct = "{:+.2%}".format
    fmt = {c: pct for c in ["超過", "D1", "D2", "年率"]} | {"t": "{:.2f}".format, "回転率": "{:.2f}".format,
                                                            "保有期間の中央値": "{:.0f}".format,
                                                            "候補の数の中央値": "{:.0f}".format}
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    print(f"シミュレーション: 約定日 {pn0.dates[0].date()}〜{pn0.dates[-1].date()}（{len(pn0.dates)} 週）")
    print(f"評価期間: 約定日 {pe.dates[0].date()}〜{pe.dates[-1].date()}（{len(pe.dates)} 週）")
    for k in ["D1", "D2"]:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）")
    print("要約（評価期間。超過は (1) に対する月平均、(5) は 50 回の平均。回転率は年あたり、保有期間は営業日）:")
    print(summ.to_string(index=False, formatters=fmt))
    for c, (thr, ex, passed, body) in verdict.items():
        print(f"{c}: (5) の 95% 点 {thr:+.2%}（中央値 {np.median(ex):+.2%}・最大 {np.max(ex):+.2%}）"
              f" → {'合格' if passed else '不合格'}（D1 {body['D1']:+.2%}・D2 {body['D2']:+.2%}・超過 {body['超過']:+.2%}）")
    print("候補の数の分布（全週）:")
    for c, n in counts.items():
        qs = np.quantile(n, [0, 0.1, 0.5, 0.9, 1])
        print(f"  {c}: " + "・".join(f"{lb} {v:.0f}" for lb, v in zip(["最小", "10%", "50%", "90%", "最大"], qs))
              + f" / 0 件の週 {int((n == 0).sum())}（評価期間 {int((n[TRANCHES:] == 0).sum())}）")
    print(f"実行時間: データ {t1 - t0:.0f} 秒・シミュレーション {t2 - t1:.0f} 秒")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
