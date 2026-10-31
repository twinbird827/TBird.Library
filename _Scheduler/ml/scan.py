"""dataset.py の表で、1 指標の 5 分位と 2 指標の分位の組み合わせを総当たりし、シャッフルの帰無分布で偶然の当たりを除いて候補を選ぶ。

分位は形成日ごとの母集団の中の順位（同じ値は平均順位）を 0.2 刻みで切り、欠損の銘柄はその指標の条件に入れない。
条件の値は、形成日ごとの「条件を満たす銘柄の ex_<h> の平均」を形成日の月で平均した系列の、月をまたいだ平均と Newey-West の t 値。
足切り: 件数 1,000 以上・30 か月以上。帰無分布: 形成日ごとに ex の行（3 つの h をまとめて）を銘柄の間で並べ替えて総当たりをやり直し、
足切りを通った全条件・全 h の最大の t 値と、コストを引く前（月の値 + COST）の最小の t 値を 200 回記録する。
候補: t 値が最大の t 値の 95% 点以上で D1・D2 の月平均がどちらも正。t 値の降順に、採った候補と行の重なり
（積集合 ÷ 小さいほうの件数）が 0.8 以上のものを飛ばして 5 本まで採る。
避ける条件: コストを引く前の t 値が最小の t 値の 5% 点以下（見送りは売買しないのでコストを引かない）。候補にはしない。
DB は読み取り専用で開き、封印期間の価格は読まない。
"""
from __future__ import annotations

import argparse
import time
import warnings
from pathlib import Path

import numpy as np
import pandas as pd

import dataset
import explore_triggers as et

N_Q = 5
MIN_ROWS = 1000
MIN_MONTHS = 30
N_SHUFFLE = 200
SEED = 414
NULL_PCT = 0.95
MAX_OVERLAP = 0.8
MAX_CANDIDATES = 5


def prepare(df: pd.DataFrame) -> tuple[pd.DataFrame, pd.Series]:
    """総当たりに使う形成月の行と、形成月 → D1/D2。"""
    per = dataset.months(df.attrs["dates"])
    return df[df["date"].dt.to_period("M").isin(per.index)].reset_index(drop=True), per


def quintiles(df: pd.DataFrame) -> np.ndarray:
    """行 × dataset.FEATURES の、形成日ごとの母集団の中の 5 分位（0〜4、欠損は −1）。"""
    r = df.groupby("date")[dataset.FEATURES].rank(method="average", pct=True).to_numpy()
    q = sum((r > b).astype(np.int8) for b in (0.2, 0.4, 0.6, 0.8))
    q[np.isnan(r)] = -1
    return q


def sweep(q: np.ndarray, ex: np.ndarray, day: np.ndarray, month: np.ndarray, n_month: int
          ) -> tuple[np.ndarray, np.ndarray]:
    """全条件の月ごとの値 M（月 × h × K × K）と件数 N（h × K × K）。K は指標 × 分位で、a = 指標 × 5 + 分位。

    条件 (a, b) は a と b の両方を満たす行（a == b は 1 指標の 1 分位）。行は day の順に並んでいること。
    """
    n_rows, n_feat = q.shape
    K, H = n_feat * N_Q, ex.shape[1]
    valid = ~np.isnan(ex)
    e0 = np.where(valid, ex, 0.0)
    msum = np.zeros((n_month, H, K, K))
    mcnt = np.zeros((n_month, H, K, K))
    N = np.zeros((H, K, K))
    col = q + N_Q * np.arange(n_feat)
    starts = np.r_[0, np.flatnonzero(np.diff(day)) + 1]
    for s, e in zip(starts, np.r_[starts[1:], n_rows]):
        has = q[s:e] >= 0
        A = np.zeros((e - s, K), np.float32)
        A[np.nonzero(has)[0], col[s:e][has]] = 1
        # 1 回の行列積で、全条件の (a, b) ごとの ex の和と件数を h ごとに出す。
        R = np.concatenate([A * e0[s:e, k:k + 1] for k in range(H)] + [A * valid[s:e, k:k + 1] for k in range(H)], 1)
        G = (A.T @ R).astype(float).reshape(K, 2, H, K).transpose(1, 2, 0, 3)
        S, C = G[0], np.rint(G[1])
        with np.errstate(all="ignore"):
            mean = S / C
        hit = C > 0
        msum[month[s]] += np.where(hit, mean, 0)
        mcnt[month[s]] += hit
        N += C
    with np.errstate(all="ignore"):
        return msum / np.where(mcnt > 0, mcnt, np.nan), N


def nw_t_rows(X: np.ndarray, lag: int) -> np.ndarray:
    """X の各行（月の系列。欠損の月は飛ばして詰める）の Newey-West の t 値（explore_triggers.nw_t と同じ）。"""
    miss = np.isnan(X)
    Y = np.take_along_axis(X, np.argsort(miss, axis=1, kind="stable"), 1)
    n = (~miss).sum(1)
    with np.errstate(all="ignore"), warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        mu = np.nanmean(Y, 1)
        D = np.nan_to_num(Y - mu[:, None])
        var = (D * D).sum(1) / (n - 1)
        for k in range(1, lag + 1):
            var += 2 * (1 - k / (lag + 1)) * (D[:, k:] * D[:, :-k]).sum(1) / (n - 1)
        return np.where((n > 2) & (var > 0), mu / np.sqrt(var / n), np.nan)


def conditions(n_feat: int) -> tuple[np.ndarray, np.ndarray]:
    """1 指標の 1 分位（a == b）と、異なる 2 指標の分位の組み合わせ（a の指標 < b の指標）。"""
    a, b = np.triu_indices(n_feat * N_Q)
    keep = (a == b) | (a // N_Q < b // N_Q)
    return a[keep], b[keep]


def stats(M: np.ndarray, N: np.ndarray, per: pd.Series) -> pd.DataFrame:
    """sweep の結果を、条件 × h ごとの件数・月数・月平均・t 値・コスト前の t 値・D1/D2 の月平均・足切りの可否にする。"""
    a, b = conditions(M.shape[-1] // N_Q)
    d1 = (per == "D1").to_numpy()
    out = []
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", RuntimeWarning)
        for k, h in enumerate(dataset.HOLDS):
            X = M[:, k, a, b].T
            out.append(pd.DataFrame({
                "a": a, "b": b, "h": h, "件数": N[k, a, b].astype(int), "月数": (~np.isnan(X)).sum(1),
                "超過": np.nanmean(X, 1), "t": nw_t_rows(X, et.nw_lag(h)),
                "tコスト前": nw_t_rows(X + dataset.COST, et.nw_lag(h)),
                "D1": np.nanmean(X[:, d1], 1), "D2": np.nanmean(X[:, ~d1], 1)}))
    r = pd.concat(out, ignore_index=True)
    r["通過"] = (r["件数"] >= MIN_ROWS) & (r["月数"] >= MIN_MONTHS)
    return r


def summarize(df: pd.DataFrame, mask: np.ndarray, h: int) -> dict:
    """(形成日, 銘柄) の行のマスクを、保有期間 h の総当たりと同じ集計で要約する（総当たりに使う形成月の行だけを数える）。"""
    per = dataset.months(df.attrs["dates"])
    x = df.loc[np.asarray(mask, bool) & df["date"].dt.to_period("M").isin(per.index) & df[f"ex_{h}"].notna()]
    s = x.groupby("date")[f"ex_{h}"].mean()
    s = s.groupby(s.index.to_period("M")).mean()
    d = per.reindex(s.index).to_numpy()
    row = {"件数": len(x), "月数": len(s), "超過": s.mean(), "t": et.nw_t(s, et.nw_lag(h)),
           "D1": s[d == "D1"].mean(), "D2": s[d == "D2"].mean()}
    row["通過"] = row["件数"] >= MIN_ROWS and row["月数"] >= MIN_MONTHS
    return row


def rows_of(q: np.ndarray, ex: np.ndarray, a: int, b: int, h: int) -> np.ndarray:
    """条件 (a, b) を満たし、ex_<h> のある行。"""
    return ((q[:, a // N_Q] == a % N_Q) & (q[:, b // N_Q] == b % N_Q)
            & ~np.isnan(ex[:, dataset.HOLDS.index(h)]))


def select(res: pd.DataFrame, thr: float, thr_lo: float, q: np.ndarray, ex: np.ndarray
           ) -> tuple[pd.DataFrame, pd.DataFrame]:
    """候補（最大 5 本）と避ける条件。thr は最大の t 値の 95% 点、thr_lo はコスト前の最小の t 値の 5% 点。"""
    ok = res["通過"]
    c = res[ok & (res["t"] >= thr) & (res["D1"] > 0) & (res["D2"] > 0)].sort_values("t", ascending=False, kind="stable")
    chosen, masks = [], []
    for i, r in c.iterrows():
        m = rows_of(q, ex, int(r["a"]), int(r["b"]), int(r["h"]))
        if any((m & x).sum() / min(m.sum(), x.sum()) >= MAX_OVERLAP for x in masks):
            continue
        chosen.append(i)
        masks.append(m)
        if len(chosen) == MAX_CANDIDATES:
            break
    return c.loc[chosen], res[ok & (res["tコスト前"] <= thr_lo)].sort_values("tコスト前")


def named(r: pd.DataFrame) -> pd.DataFrame:
    """条件の a・b を指標名と分位（Q1 が最下位）にする。1 指標の条件は 指標2 が空。"""
    f = np.array(dataset.FEATURES)
    single = r["a"] == r["b"]
    out = pd.DataFrame({"指標1": f[r["a"] // N_Q], "分位1": r["a"] % N_Q + 1,
                        "指標2": np.where(single, "", f[r["b"] // N_Q]),
                        "分位2": np.where(single, 0, r["b"] % N_Q + 1)}, index=r.index)
    return pd.concat([out, r.drop(columns=["a", "b"])], axis=1)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "scan"
    t0 = time.perf_counter()
    df, per = prepare(dataset.load(db_path, dataset.SEAL_END))
    t1 = time.perf_counter()
    q = quintiles(df)
    ex = df[[f"ex_{h}" for h in dataset.HOLDS]].to_numpy()
    day = pd.factorize(df["date"])[0]
    month = per.index.get_indexer(df["date"].dt.to_period("M"))
    res = stats(*sweep(q, ex, day, month, len(per)), per)
    rng = np.random.default_rng(SEED)
    null, null_lo = [], []
    for _ in range(N_SHUFFLE):
        perm = np.lexsort((rng.random(len(day)), day))
        r = stats(*sweep(q, ex[perm], day, month, len(per)), per)
        null.append(r.loc[r["通過"], "t"].max())
        null_lo.append(r.loc[r["通過"], "tコスト前"].min())
    t2 = time.perf_counter()
    thr = float(np.quantile(null, NULL_PCT))
    thr_lo = float(np.quantile(null_lo, 1 - NULL_PCT))
    cand, avoid = select(res, thr, thr_lo, q, ex)

    out_dir.mkdir(parents=True, exist_ok=True)
    single = res["a"] == res["b"]
    named(res[single]).to_csv(out_dir / "singles.csv", index=False, encoding="utf-8-sig")
    named(res[~single & res["通過"]]).to_csv(out_dir / "pairs.csv", index=False, encoding="utf-8-sig")
    pd.DataFrame({"回": np.arange(1, N_SHUFFLE + 1), "最大t": null, "コスト前の最小t": null_lo}).to_csv(
        out_dir / "null.csv", index=False, encoding="utf-8-sig")
    both = pd.concat([cand.assign(区分="候補"), avoid.assign(区分="避ける")])
    named(both).to_csv(out_dir / "candidates.csv", index=False, encoding="utf-8-sig")

    s = named(res[single])
    tilt = (s[s["分位1"] == N_Q].set_index(["指標1", "h"])["超過"]
            - s[s["分位1"] == 1].set_index(["指標1", "h"])["超過"]).rename("上位−下位").reset_index()
    tilt = tilt.reindex(tilt["上位−下位"].abs().sort_values(ascending=False).index)

    fmt = {c: "{:+.2%}".format for c in ["超過", "D1", "D2", "上位−下位"]} | {"t": "{:.2f}".format, "tコスト前": "{:.2f}".format}
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    for k in ["D1", "D2"]:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）")
    print("試行数:", int(res["通過"].sum()), "（h ごと:",
          ", ".join(f"{h}日 単独 {int((g['通過'] & (g['a'] == g['b'])).sum())}・組み合わせ "
                    f"{int((g['通過'] & (g['a'] != g['b'])).sum())}" for h, g in res.groupby("h")) + "）")
    print(f"帰無分布の最大 t 値の 95% 点: {thr:.2f}（中央値 {np.median(null):.2f}・最大 {np.max(null):.2f}）")
    print(f"帰無分布のコスト前の最小 t 値の 5% 点: {thr_lo:.2f}（中央値 {np.median(null_lo):.2f}・最小 {np.min(null_lo):.2f}）")
    print("候補:")
    print(named(cand).to_string(index=False, formatters=fmt) if len(cand) else "なし")
    print(f"避ける条件（{len(avoid)} 件、コスト前の t の小さい順に上位 20）:")
    print(named(avoid.head(20)).to_string(index=False, formatters=fmt) if len(avoid) else "なし")
    print("単独の分位の傾き（Q5 − Q1 の月平均、絶対値の大きい順に上位 20）:")
    print(tilt.head(20).to_string(index=False, formatters=fmt))
    print(f"実行時間: データセット {t1 - t0:.0f} 秒・総当たりとシャッフル {t2 - t1:.0f} 秒")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
