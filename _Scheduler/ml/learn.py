"""dataset.py の表で、指標の順位から LightGBM で ex_<h> を予測し、予測の上位 5 分位を 1 本の買い方として、シャッフルの帰無分布で偶然の当たりを除いて候補を選ぶ。

行は scan.prepare が残す総当たりの形成月の行。入力は dataset.FEATURES の、形成日ごとの母集団の中の順位（同じ値は平均順位、欠損は欠損のまま）。
h ごとに別のモデルを、TEST_YEARS の年ごとに、形成日がその年の最初の形成日より h 営業日以上前の行で学習し直す（ハイパーパラメータは PARAMS で固定し、調整しない）。
予測はテストの年の形成日ごとに 5 分位に分け、4 年分の上位 5 分位の行を scan.summarize で集計する（D1/D2 はテストの年の形成月を dataset.months と同じ規則で半分に分け直す）。
帰無分布: 形成日ごとに ex の行（3 つの h をまとめて）を銘柄の間で並べ替えて学習からやり直し、足切りを通った h の上位 5 分位の最大の t 値を 50 回記録する。
候補: t 値が最大の t 値の 95% 点以上・D1・D2 の月平均がどちらも正・足切り（件数 1,000 以上・30 か月以上）を通る h の上位 5 分位。
t 値の降順に、採った候補と行の重なり（scan.overlap）が 0.8 以上のものを飛ばす。総当たりの候補（explore/scan/candidates.csv）との重なりは飛ばさず記録だけする。
DB は読み取り専用で開き、封印期間の価格は読まない。
"""
from __future__ import annotations

import argparse
import time
from collections.abc import Iterator
from pathlib import Path

import lightgbm as lgb
import numpy as np
import pandas as pd

import dataset
import scan

TEST_YEARS = [2019, 2020, 2021, 2022]
N_SHUFFLE = 50
SEED = 414
N_TREES = 300
PARAMS = {"objective": "regression", "num_leaves": 31, "learning_rate": 0.05, "feature_fraction": 0.8,
          "deterministic": True, "force_row_wise": True, "seed": SEED, "verbosity": -1}
MIN_LEAF_FRAC = 0.01
EX = [f"ex_{h}" for h in dataset.HOLDS]


def ranks(df: pd.DataFrame) -> np.ndarray:
    """行 × dataset.FEATURES の、形成日ごとの母集団の中の順位（scan.quintiles と同じ。欠損は欠損のまま）。"""
    return df.groupby("date")[dataset.FEATURES].rank(method="average", pct=True).to_numpy()


def eval_months(df: pd.DataFrame) -> pd.Series:
    """テストの年の形成月 → D1/D2（dataset.months と同じく、端数は D2）。"""
    ms = dataset.months(df.attrs["dates"]).index
    ms = ms[ms.year.isin(TEST_YEARS)]
    b = len(ms) // 2
    return pd.Series(["D1"] * b + ["D2"] * (len(ms) - b), index=ms)


def splits(df: pd.DataFrame, h: int) -> Iterator[tuple[int, np.ndarray, np.ndarray]]:
    """テストの年ごとの (年, 学習の行, テストの行)。学習は、形成日がテストの最初の形成日より h 営業日以上前の行。"""
    pos = df.attrs["dates"].get_indexer(df["date"])
    year = df["date"].dt.year.to_numpy()
    for y in TEST_YEARS:
        test = year == y
        yield y, pos <= pos[test].min() - h, test


def walkforward(df: pd.DataFrame, X: np.ndarray, y: np.ndarray, h: int) -> tuple[np.ndarray, list[dict]]:
    """予測の形成日ごとの 5 分位（0〜4、テストでない行は −1）と、テストの年ごとの学習の範囲・モデル。"""
    q = np.full(len(df), -1, np.int8)
    info = []
    for year, train, test in splits(df, h):
        tr = train & ~np.isnan(y)
        params = PARAMS | {"min_data_in_leaf": max(1, round(tr.sum() * MIN_LEAF_FRAC))}
        model = lgb.train(params, lgb.Dataset(X[tr], label=y[tr], feature_name=dataset.FEATURES),
                          num_boost_round=N_TREES)
        r = pd.Series(model.predict(X[test])).groupby(df["date"].to_numpy()[test]).rank(
            method="average", pct=True).to_numpy()
        q[test] = sum((r > b).astype(np.int8) for b in (0.2, 0.4, 0.6, 0.8))
        info.append({"テストの年": year, "学習の最後の形成日": df["date"][tr].max(),
                     "テストの最初の形成日": df["date"][test].min(), "model": model})
    return q, info


def run(df: pd.DataFrame, X: np.ndarray, per: pd.Series) -> dict[int, tuple[np.ndarray, list[dict], dict]]:
    """h ごとの、予測の分位・テストの年ごとの学習の範囲とモデル・4 年分の上位 5 分位の集計。"""
    out = {}
    for h in dataset.HOLDS:
        q, info = walkforward(df, X, df[f"ex_{h}"].to_numpy(), h)
        out[h] = q, info, scan.summarize(df, q == scan.N_Q - 1, h, per)
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--db", required=True)
    args = ap.parse_args()

    db_path = Path(args.db).resolve()
    out_dir = db_path.parent / "explore" / "learn"
    t0 = time.perf_counter()
    df, _ = scan.prepare(dataset.load(db_path, dataset.SEAL_END))
    X = ranks(df)
    per = eval_months(df)
    t1 = time.perf_counter()
    res = run(df, X, per)
    t2 = time.perf_counter()
    rng = np.random.default_rng(SEED)
    day = pd.factorize(df["date"])[0]
    null = []
    for _ in range(N_SHUFFLE):
        d = df.copy()
        d[EX] = df[EX].to_numpy()[np.lexsort((rng.random(len(day)), day))]
        null.append(max((s["t"] for *_, s in run(d, X, per).values() if s["通過"]), default=np.nan))
    t3 = time.perf_counter()
    thr = float(np.quantile(null, scan.NULL_PCT))

    ex = df[EX].to_numpy()
    summ = pd.DataFrame([{"h": h} | s for h, (*_, s) in res.items()])
    masks = {h: (q == scan.N_Q - 1) & ~np.isnan(ex[:, k]) for k, (h, (q, *_)) in enumerate(res.items())}
    ok = summ["通過"] & (summ["t"] >= thr) & (summ["D1"] > 0) & (summ["D2"] > 0)
    chosen = []
    for i in summ[ok].sort_values("t", ascending=False, kind="stable").index:
        if all(scan.overlap(masks[summ.at[i, "h"]], masks[summ.at[j, "h"]]) < scan.MAX_OVERLAP for j in chosen):
            chosen.append(i)
    summ["候補"] = summ.index.isin(chosen)

    sc = pd.read_csv(db_path.parent / "explore" / "scan" / "candidates.csv", encoding="utf-8-sig")
    fq = scan.quintiles(df)
    test_rows = df["date"].dt.year.isin(TEST_YEARS).to_numpy()
    for _, r in sc[sc["区分"] == "候補"].iterrows():
        single = r["分位2"] == 0
        a = dataset.FEATURES.index(r["指標1"]) * scan.N_Q + int(r["分位1"]) - 1
        b = a if single else dataset.FEATURES.index(r["指標2"]) * scan.N_Q + int(r["分位2"]) - 1
        x = scan.rows_of(fq, ex, a, b, int(r["h"])) & test_rows
        name = f"{r['指標1']} Q{r['分位1']}" + ("" if single else f" × {r['指標2']} Q{r['分位2']}") + f" {r['h']}日"
        summ[f"重なり: {name}"] = [scan.overlap(masks[h], x) for h in summ["h"]]

    year = df["date"].dt.year.to_numpy()
    lgbm = pd.DataFrame([
        {"h": h, "テストの年": i["テストの年"], "分位": k + 1, "学習の最後の形成日": i["学習の最後の形成日"],
         "テストの最初の形成日": i["テストの最初の形成日"]} | scan.summarize(df, (q == k) & (year == i["テストの年"]), h, per)
        for h, (q, info, _) in res.items() for i in info for k in range(scan.N_Q)])
    imp = pd.DataFrame([
        {"h": h, "テストの年": i["テストの年"], "指標": f, "gain": g}
        for h, (_, info, _) in res.items() for i in info
        for f, g in zip(dataset.FEATURES, i["model"].feature_importance("gain"))])

    out_dir.mkdir(parents=True, exist_ok=True)
    lgbm.to_csv(out_dir / "lgbm.csv", index=False, encoding="utf-8-sig")
    imp.to_csv(out_dir / "importance.csv", index=False, encoding="utf-8-sig")
    pd.DataFrame({"回": np.arange(1, N_SHUFFLE + 1), "最大t": null}).to_csv(
        out_dir / "null.csv", index=False, encoding="utf-8-sig")
    summ.to_csv(out_dir / "candidates.csv", index=False, encoding="utf-8-sig")

    pct = "{:+.2%}".format
    fmt = {c: pct for c in ["超過", "D1", "D2", "Q5", "Q1", "Q5−Q1"]} | {"t": "{:.2f}".format}
    pd.set_option("display.width", 300)
    pd.set_option("display.max_columns", 50)
    pd.set_option("display.unicode.east_asian_width", True)
    for k in ["D1", "D2"]:
        mk = per[per == k].index
        print(f"{k}: {mk.min()}〜{mk.max()}（{len(mk)} か月）")
    print("試行数:", len(dataset.HOLDS), "（LightGBM の h の数）")
    print(f"帰無分布の最大 t 値の 95% 点: {thr:.2f}（中央値 {np.nanmedian(null):.2f}・最大 {np.nanmax(null):.2f}）")
    print("4 年分の上位 5 分位の成績と候補（重なりは総当たりの候補との積集合 ÷ 小さいほうの件数）:")
    print(summ.to_string(index=False, formatters=fmt | {c: "{:.2f}".format for c in summ if c.startswith("重なり")}))
    yq = lgbm.pivot_table(index=["h", "テストの年"], columns="分位", values="超過")
    print("テストの年ごとの上位・下位 5 分位の月平均:")
    print(pd.DataFrame({"Q5": yq[scan.N_Q], "Q1": yq[1], "Q5−Q1": yq[scan.N_Q] - yq[1]}).reset_index().to_string(
        index=False, formatters=fmt))
    print("重要度の上位 10（4 年の gain の平均の比率）:")
    for h, g in imp.groupby("h"):
        m = g.groupby("指標")["gain"].mean()
        print(f"  {h}日: " + "・".join(f"{f} {v:.1%}" for f, v in (m / m.sum()).nlargest(10).items()))
    print(f"実行時間: データセット {t1 - t0:.0f} 秒・学習 {t2 - t1:.0f} 秒・シャッフル {t3 - t2:.0f} 秒")
    print("出力:", out_dir)


if __name__ == "__main__":
    main()
