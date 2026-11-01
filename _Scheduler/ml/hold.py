"""dataset.py の表で、ベースの買いルールで買ってよい銘柄を決め、その中で売る理由の無い銘柄を買い、売る理由が出るまで持ち続ける戦略を週ごとにシミュレーションする。

判定は毎週の形成日 t の引け後に、t の母集団の中の順位（learn.ranks）で行い、売りも買いも t の翌営業日の始値で約定する。欠損の条件は偽。
ベース: roa_op の順位 0.5 以上かつ pbr の順位 0.5 以下。売る理由（REASONS）のどれかが真なら売り、買う時点でどれかが真の銘柄は買わない。
母集団から外れた銘柄は売る（始値が無ければ Market.C）。枠は 50 銘柄で、候補が空きより多いときは乱数で選び、そのときの現金を新しく買う銘柄で等分する。比率は直さない。
コストは片道 COST を売買した金額にだけ掛ける。資産は約定日の始値（無ければ Market.C）で評価し、期間の終わりに持っている銘柄は清算しない。
比べる相手: (1) 母集団の等金額（始値 → 次の約定日の始値、コストなし）、(2) 売る理由では売らない、(3) 13 週ごとに全体を入れ替える、
(4) 本体の売る理由による売却の件数だけ無作為に売る（50 回）。本体・(2)・(3)・売る理由を 1 つ外した版は、買う順の種 20 本の平均で要約する。
月ごとの超過は、約定日から次の約定日までの区間を始まりの約定日の月に入れ、月ごとに複利でつないだ (1) との差。期間の月を半分に分けて D1/D2（端数は D2）。
合否: 本体の月平均の超過が D1・D2 ともに正で、(4) の 50 回の月平均の超過の 95% 点以上。
DB は読み取り専用で開き、封印期間の価格は読まない。
"""
from __future__ import annotations

import argparse
import sqlite3
import time
import warnings
from pathlib import Path
from typing import NamedTuple

import numpy as np
import pandas as pd

import dataset
import explore_triggers as et
import learn
import triggers

SLOTS = 50
COST = dataset.COST / 2
N_ORDER = 20
N_RANDOM = 50
ROTATE = 13
LAG = 3
NULL_PCT = 0.95
SEED = 414


def base(r: pd.DataFrame) -> pd.Series:
    """ベースの買いルール（r は順位）。"""
    return (r["roa_op"] >= 0.5) & (r["pbr"] <= 0.5)


# 売る理由（r は順位、x は値そのもの）。欠損との比較は偽になるので、欠損を理由に売らない。
REASONS = {
    "割高化": lambda r, x: (r["per"] >= 0.8) | (r["pbr"] >= 0.8),
    "財務の悪化": lambda r, x: r["roa_op"] <= 0.2,
    "決算が予想を下回った": lambda r, x: r["sue"] <= 0.2,
    "下方修正": lambda r, x: x["rev_chg"] < 0,
    "過熱": lambda r, x: r["ret250"] >= 0.8,
    "値動きが荒い": lambda r, x: r["sd60"] >= 0.8,
    "利益の急伸": lambda r, x: r["op_yoy"] >= 0.8,
}


class Panel(NamedTuple):
    """形成日 k × 銘柄 j の判定と約定の価格。行は、翌営業日がパネルにある期間内の形成日。"""
    fd: pd.DatetimeIndex  # 形成日
    dates: pd.DatetimeIndex  # 約定日
    a: np.ndarray  # 約定日の営業日の番号
    codes: pd.Index
    univ: np.ndarray  # 母集団
    base: np.ndarray
    reasons: np.ndarray  # 売る理由 × k × j
    O: np.ndarray  # 約定日の始値
    P: np.ndarray  # 約定日の評価の価格（始値、無ければ Market.C）


class Run(NamedTuple):
    """1 回のシミュレーションの約定日ごとの記録。"""
    nav: np.ndarray  # 売買の前の資産（最初は 1）
    cash: np.ndarray  # 売買の後の現金の比率
    held: np.ndarray  # 売買の後に持っている銘柄数
    traded: np.ndarray  # 売買した金額
    n_reason: np.ndarray  # 売る理由による売却の件数
    holds: list[int]  # 売った銘柄の保有期間（営業日）
    open_holds: list[int]  # 期間の終わりに持っている銘柄の保有期間（打ち切り）
    trades: list[dict]


def build(df: pd.DataFrame, m: et.Market, codes: pd.Index) -> Panel:
    """dataset.months の最初の月の最初の形成日から、翌営業日がパネルにある最後の形成日まで。"""
    fd = dataset.formation_dates(m.dates)
    t = m.dates.get_indexer(fd)
    keep = (fd.to_period("M") >= dataset.months(m.dates).index[0]) & (t + 1 < m.n)
    fd, a = fd[keep], t[keep] + 1
    x = df[df["date"].isin(fd)].reset_index(drop=True)
    r = pd.DataFrame(learn.ranks(x), columns=dataset.FEATURES)
    k, j = fd.get_indexer(x["date"]), codes.get_indexer(x["code"])
    assert (k >= 0).all() and (j >= 0).all()

    def grid(v) -> np.ndarray:
        g = np.zeros((len(fd), len(codes)), bool)
        g[k, j] = v
        return g

    O = m.O[a]
    return Panel(fd, m.dates[a], a, codes, grid(True), grid(base(r).to_numpy()),
                 np.stack([grid(f(r, x).to_numpy()) for f in REASONS.values()]), O, np.where(np.isnan(O), m.C[a], O))


def bench(pn: Panel) -> np.ndarray:
    """(1) の約定日 k → k+1 のリターン（k の母集団で始値のある銘柄の等金額平均）。"""
    with np.errstate(all="ignore"):
        g = np.where(pn.univ[:-1] & ~np.isnan(pn.O[:-1]), pn.P[1:] / pn.O[:-1] - 1, np.nan)
    return np.nanmean(g, axis=1)


def pick(mask: np.ndarray, n: int, rng: np.random.Generator) -> np.ndarray:
    """mask の銘柄から n 件（多ければ乱数で選ぶ）。"""
    idx = np.flatnonzero(mask)
    return rng.choice(idx, n, replace=False) if len(idx) > n else idx


def simulate(pn: Panel, sell: np.ndarray, mode: str, rng: np.random.Generator,
             n_sell: np.ndarray | None = None, sell_rng: np.random.Generator | None = None,
             record: bool = False) -> Run:
    """mode は "hold"（本体・売る理由を外した版）、"keep"（(2)）、"rotate"（(3)）、"random"（(4)。約定日 k に n_sell[k] 件を sell_rng で売る）。

    sell は k × j の売る理由（どれかが真）。買う候補は、母集団・ベース・売る理由なし・約定日の始値ありで、その日に売った銘柄を除く。
    """
    K, J = pn.univ.shape
    q, bought, cash = np.zeros(J), np.zeros(J, int), 1.0
    nav, cash_ratio, traded = np.zeros(K), np.zeros(K), np.zeros(K)
    held_n, n_reason = np.zeros(K, int), np.zeros(K, int)
    holds, trades, names = [], [], list(REASONS)
    for k in range(K):
        px, h = pn.P[k], q > 0
        assert np.isfinite(px[h]).all()
        nav[k] = cash + q[h] @ px[h]
        ok = pn.univ[k] & pn.base[k] & ~sell[k] & ~np.isnan(pn.O[k])
        out = h & ~pn.univ[k]
        why = np.zeros(J, bool)
        if mode == "hold":
            why = h & sell[k]
        elif mode == "random":
            why[pick(h & ~out, n_sell[k], sell_rng)] = True
        elif mode == "rotate" and k % ROTATE == 0:
            chosen = np.zeros(J, bool)
            chosen[pick(ok, SLOTS, rng)] = True
            out |= h & ~chosen
        s = out | why
        n_reason[k] = (why & ~out).sum()
        v = q[s] * px[s]
        cash += v.sum() * (1 - COST)
        traded[k] += v.sum()
        holds += (pn.a[k] - bought[s]).tolist()
        if record:
            for jj, vv in zip(np.flatnonzero(s), v):
                trades.append({"形成日": pn.fd[k], "約定日": pn.dates[k], "銘柄": pn.codes[jj], "売買": "売り", "金額": vv,
                               "売る理由": "・".join(n for n, r in zip(names, pn.reasons[:, k, jj]) if r)
                               if pn.univ[k, jj] else "母集団外"})
        q[s] = 0
        h = q > 0
        if mode == "rotate":
            new = np.flatnonzero(chosen & ~h) if k % ROTATE == 0 else np.array([], int)
        else:
            new = pick(ok & ~h & ~s, SLOTS - h.sum(), rng)
        if len(new) and cash > 0:
            amt = cash / len(new) / (1 + COST)
            q[new] = amt / pn.O[k, new]
            bought[new] = pn.a[k]
            traded[k] += amt * len(new)
            cash = 0.0
            if record:
                trades += [{"形成日": pn.fd[k], "約定日": pn.dates[k], "銘柄": pn.codes[jj], "売買": "買い", "金額": amt,
                            "売る理由": ""} for jj in new]
        h = q > 0
        held_n[k] = h.sum()
        cash_ratio[k] = cash / (cash + q[h] @ px[h])
    return Run(nav, cash_ratio, held_n, traded, n_reason, holds, (pn.a[-1] - bought[q > 0]).tolist(), trades)


def halves(pn: Panel) -> pd.Series:
    """区間の始まりの約定日の月 → D1/D2（dataset.months と同じく、端数は D2）。"""
    ms = pn.dates[:-1].to_period("M").unique()
    b = len(ms) // 2
    return pd.Series(["D1"] * b + ["D2"] * (len(ms) - b), index=ms)


def years(pn: Panel) -> float:
    return (pn.dates[-1] - pn.dates[0]).days / 365.25


def monthly(r: np.ndarray, pn: Panel) -> pd.Series:
    """約定日 k → k+1 のリターンを、始まりの約定日の月ごとに複利でつないだ値。"""
    return pd.Series(1 + r).groupby(pn.dates[:-1].to_period("M")).prod() - 1


def summarize(runs: list[Run], pn: Panel, b: np.ndarray, per: pd.Series) -> dict:
    """runs の月ごとの (1) に対する超過を平均した系列の月平均・t 値・D1/D2 と、年率・回転率・保有期間の中央値の runs の平均。"""
    bm = monthly(b, pn)
    ex = pd.concat([monthly(r.nav[1:] / r.nav[:-1] - 1, pn) - bm for r in runs], axis=1).mean(axis=1)
    d = per.reindex(ex.index).to_numpy()
    y = years(pn)
    return {"超過": ex.mean(), "t": et.nw_t(ex, LAG), "D1": ex[d == "D1"].mean(), "D2": ex[d == "D2"].mean(),
            "年率": np.mean([r.nav[-1] ** (1 / y) - 1 for r in runs]),
            "回転率": np.mean([r.traded.sum() / 2 / r.nav.mean() / y for r in runs]),
            "保有期間の中央値": np.mean([np.median(r.holds) if r.holds else np.nan for r in runs])}


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "hold"
    t0 = time.perf_counter()
    df = dataset.load(db_path, dataset.SEAL_END)
    conn = sqlite3.connect(f"{db_path.as_uri()}?mode=ro", uri=True)
    try:
        p = triggers.DailyPanel(conn, dataset.SEAL_END)
    finally:
        conn.close()
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        m = et.Market(p)
    pn = build(df, m, p.codes)
    del df, p, m
    b, per = bench(pn), halves(pn)
    t1 = time.perf_counter()

    sell = pn.reasons.any(0)

    def order(s: int) -> np.random.Generator:
        return np.random.default_rng((SEED, 0, s))

    runs = {"本体": [simulate(pn, sell, "hold", order(s), record=True) for s in range(N_ORDER)],
            "(2) 売らない": [simulate(pn, sell, "keep", order(s)) for s in range(N_ORDER)],
            "(3) 定期の入れ替え": [simulate(pn, sell, "rotate", order(s)) for s in range(N_ORDER)]}
    runs["(4) 無作為な売り"] = [
        simulate(pn, sell, "random", order(i % N_ORDER), n_sell=runs["本体"][i % N_ORDER].n_reason,
                 sell_rng=np.random.default_rng((SEED, 1, i))) for i in range(1, N_RANDOM + 1)]
    ablation = {n: [simulate(pn, np.delete(pn.reasons, i, 0).any(0), "hold", order(s)) for s in range(N_ORDER)]
                for i, n in enumerate(REASONS)}
    t2 = time.perf_counter()

    summ = pd.DataFrame([{"戦略": k} | summarize(v, pn, b, per) for k, v in runs.items()]
                        + [{"戦略": "(1) 全銘柄の等金額", "年率": np.prod(1 + b) ** (1 / years(pn)) - 1}])
    null = [summarize([r], pn, b, per)["超過"] for r in runs["(4) 無作為な売り"]]
    thr = float(np.quantile(null, NULL_PCT))
    body = summ.iloc[0]
    passed = body["D1"] > 0 and body["D2"] > 0 and body["超過"] >= thr
    abl = pd.DataFrame([{"外した理由": n} | summarize(v, pn, b, per) for n, v in ablation.items()])

    ids = {k: range(1, N_RANDOM + 1) if k.startswith("(4)") else range(N_ORDER) for k in runs}
    nav = pd.concat([pd.DataFrame({"形成日": pn.fd, "約定日": pn.dates, "戦略": k, "種": s, "資産": r.nav,
                                   "現金の比率": r.cash, "銘柄数": r.held})
                     for k, v in runs.items() for s, r in zip(ids[k], v)])
    trades = pd.DataFrame([{"種": s} | t for s, r in enumerate(runs["本体"]) for t in r.trades])
    out_dir.mkdir(parents=True, exist_ok=True)
    nav.to_csv(out_dir / "nav.csv", index=False, encoding="utf-8-sig")
    trades.to_csv(out_dir / "trades.csv", index=False, encoding="utf-8-sig")
    summ.to_csv(out_dir / "summary.csv", index=False, encoding="utf-8-sig")
    pd.DataFrame({"回": ids["(4) 無作為な売り"], "買う順の種": [i % N_ORDER for i in ids["(4) 無作為な売り"]],
                  "超過": null}).to_csv(out_dir / "null.csv", index=False, encoding="utf-8-sig")
    abl.to_csv(out_dir / "ablation.csv", index=False, encoding="utf-8-sig")

    pct = "{:+.2%}".format
    fmt = {c: pct for c in ["超過", "D1", "D2", "年率"]} | {"t": "{:.2f}".format, "回転率": "{:.2f}".format,
                                                            "保有期間の中央値": "{:.0f}".format}
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    print(f"期間: 約定日 {pn.dates[0].date()}〜{pn.dates[-1].date()}（{len(pn.dates)} 週）")
    for k in ["D1", "D2"]:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）")
    print("要約（超過は (1) に対する月平均。本体・(2)・(3) は買う順の種 20 本、(4) は 50 回の平均。回転率は年あたり、保有期間は営業日）:")
    print(summ.to_string(index=False, formatters=fmt))
    print(f"(4) の月平均の超過の 95% 点: {thr:+.2%}（中央値 {np.median(null):+.2%}・最大 {np.max(null):+.2%}）")
    print(f"合否: {'合格' if passed else '不合格'}（D1 {body['D1']:+.2%}・D2 {body['D2']:+.2%}・超過 {body['超過']:+.2%}）")
    done = pd.Series([x for r in runs["本体"] for x in r.holds], dtype=float)
    cens = pd.Series([x for r in runs["本体"] for x in r.open_holds], dtype=float)
    q = done.quantile([0.1, 0.25, 0.5, 0.75, 0.9])
    print(f"本体の保有期間（営業日、種 20 本の合計）: 売った {len(done)} 件 "
          + "・".join(f"{int(k * 100)}% {v:.0f}" for k, v in q.items())
          + f" / 打ち切り {len(cens)} 件 中央値 {cens.median():.0f}")
    print("売る理由を 1 つずつ外した版（合否に使わない）:")
    print(abl.to_string(index=False, formatters=fmt))
    print(f"実行時間: データ {t1 - t0:.0f} 秒・シミュレーション {t2 - t1:.0f} 秒")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
