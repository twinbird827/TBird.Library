"""OOS 検証レポート（Rank-IC / NDCG@K）。

実行:
  uv run python evaluate.py --db ../TradeAnalyzer.Worker/trade.db --is 2024 --oos 2025

評価は OOS の Rank-IC（スコア vs 実 FwdReturn の Spearman 相関）と NDCG@K（設計§6）。
単一累積リターン曲線は信用しない。「ML スコア順 vs ルール RuleScore 順」を比較し、
ML がルール単純合算を順位付けで上回るかをレポートする。

評価指標は Python に集約（C# バックテストは勝率・平均リターンで A/B、二重実装を避ける）。
train.py が Signal.MlScore を書き戻した後に実行する（MlScore は DB から読む）。
"""
from __future__ import annotations

import argparse

from math import erf, sqrt

import numpy as np
import pandas as pd

import labels as labmod
from train import load_dataset, parse_window, _slice


def _spearman(a: np.ndarray, b: np.ndarray) -> float:
    """順位相関（順位化した a,b の Pearson）。scipy 非依存。"""
    ra = pd.Series(a).rank().to_numpy()
    rb = pd.Series(b).rank().to_numpy()
    if np.std(ra) == 0 or np.std(rb) == 0:
        return float("nan")
    return float(np.corrcoef(ra, rb)[0, 1])


def _ndcg_at_k(rel: np.ndarray, scores: np.ndarray, k: int) -> float:
    """スコア降順上位 K の DCG を理想 DCG で正規化。rel は非負 gain（LabelGain 0..4）。"""
    order = np.argsort(-scores, kind="stable")
    gains = rel[order][:k]
    discounts = 1.0 / np.log2(np.arange(2, len(gains) + 2))
    dcg = float(np.sum((2.0 ** gains - 1.0) * discounts))
    ideal = np.sort(rel)[::-1][:k]
    idcg = float(np.sum((2.0 ** ideal - 1.0) / np.log2(np.arange(2, len(ideal) + 2))))
    return dcg / idcg if idcg > 0 else 0.0


def _norm_two_sided_p(z: float) -> float:
    """標準正規での両側 p（=1-erf(|z|/√2)）。t 分布は大標本で正規に収束＝近似値。"""
    if not np.isfinite(z):
        return float("nan")
    return float(1.0 - erf(abs(z) / sqrt(2.0)))


def _significance(ics: np.ndarray) -> dict | None:
    """日次 Rank-IC 系列の有意性。t統計量・両側p（正規近似）・ICIR・IC>0率・符号検定p を返す。"""
    a = np.asarray(ics, float)
    a = a[np.isfinite(a)]
    n = a.size
    if n < 2:
        return None
    mean = float(a.mean())
    sd = float(a.std(ddof=1))
    t = mean / (sd / sqrt(n)) if sd > 0 else float("nan")
    pos = int((a > 0).sum())
    # 符号検定: H0 で IC>0 率=0.5。z=(pos - n/2)/(√n/2)（正規近似）。
    z_sign = (pos - n / 2.0) / (sqrt(n) / 2.0)
    return {
        "n": n, "mean": mean, "t": t, "p": _norm_two_sided_p(t),
        "icir": (mean / sd if sd > 0 else float("nan")),
        "hit": pos / n, "sign_p": _norm_two_sided_p(z_sign),
    }


def _daily_ic_table(df: pd.DataFrame) -> pd.DataFrame:
    """各 Date の ML/Rule クロスセクション Rank-IC を1表に（評価不能日は NaN）。

    ML/Rule を同じ Date 集合で並べることで、後段の「ML−Rule 対日差」検定の対応付けを可能にする。
    """
    def _ic(g: pd.DataFrame, col: str) -> float:
        gg = g.dropna(subset=[col, "FwdReturn"])
        if len(gg) < 3 or gg[col].nunique() < 2:
            return float("nan")
        return _spearman(gg[col].to_numpy(float), gg["FwdReturn"].to_numpy(float))

    rows = [(d, _ic(g, "MlScore"), _ic(g, "RuleScore")) for d, g in df.groupby("Date")]
    return pd.DataFrame(rows, columns=["Date", "ic_ml", "ic_rule"])


def _fmt_sig(s: dict | None) -> str:
    if s is None:
        return "(有意性: 日数不足)"
    return (f"({s['n']}日) t={s['t']:+.2f}, p≒{s['p']:.3f}(正規近似), "
            f"ICIR={s['icir']:+.3f}, IC>0={s['hit'] * 100:.0f}%(符号検定 p≒{s['sign_p']:.3f})")


def _ndcg_report(df: pd.DataFrame, score_col: str, k: int) -> tuple[float, int]:
    vals = []
    for _, g in df.groupby("Date"):
        g = g.dropna(subset=[score_col, "LabelGain"])
        g = g[g["LabelGain"] >= 0]
        if len(g) < 2:
            continue
        vals.append(_ndcg_at_k(g["LabelGain"].to_numpy(float), g[score_col].to_numpy(float), k))
    return (float(np.mean(vals)) if vals else float("nan"), len(vals))


def main() -> None:
    ap = argparse.ArgumentParser(description="段階2 OOS 検証レポート（Rank-IC/NDCG）")
    ap.add_argument("--db", required=True)
    ap.add_argument("--is", dest="is_window", required=True)
    ap.add_argument("--oos", dest="oos_window", required=True)
    ap.add_argument("--horizon", type=int, default=labmod.DEFAULT_HORIZON)
    ap.add_argument("--topk", type=int, default=10)
    args = ap.parse_args()

    oos_start, oos_end = parse_window(args.oos_window)

    df, _ = load_dataset(args.db, args.horizon)  # PS1: load_dataset は (df, bars) を返す（bars は評価では未使用）。
    oos = _slice(df, oos_start, oos_end)
    # Rank-IC/NDCG は確定ラベル行で評価（FwdReturn が無い末尾行は除外）。
    eval_df = oos[oos["LabelConfirmed"]].copy()

    print("=== OOS 検証レポート ===")
    print(f"OOS 期間: {oos_start:%Y-%m-%d}..{oos_end:%Y-%m-%d}")
    print(f"OOS Passed 行={len(oos)}, 確定ラベル行={len(eval_df)}")
    if eval_df.empty:
        print("確定ラベル行が 0 のため評価できません（履歴不足）。")
        return

    ml_missing = oos["MlScore"].isna().sum()
    if ml_missing:
        print(f"⚠ OOS Passed 行のうち MlScore=null が {ml_missing} 件あります（train.py 未実行/期間不一致の可能性）。")

    tbl = _daily_ic_table(eval_df)
    ic_ml = float(np.nanmean(tbl["ic_ml"])) if tbl["ic_ml"].notna().any() else float("nan")
    ic_rule = float(np.nanmean(tbl["ic_rule"])) if tbl["ic_rule"].notna().any() else float("nan")
    sig_ml = _significance(tbl["ic_ml"].to_numpy())
    sig_rule = _significance(tbl["ic_rule"].to_numpy())
    ndcg_ml, _ = _ndcg_report(eval_df, "MlScore", args.topk)
    ndcg_rule, _ = _ndcg_report(eval_df, "RuleScore", args.topk)

    print(f"\nRank-IC (日次平均, Spearman):")
    print(f"  ML   : {ic_ml:+.4f}  {_fmt_sig(sig_ml)}")
    print(f"  Rule : {ic_rule:+.4f}  {_fmt_sig(sig_rule)}")

    # ML−Rule の対日差（同一 Date で両方評価できた日）で「ML の上乗せが有意か」を検定する。
    paired = tbl.dropna(subset=["ic_ml", "ic_rule"])
    sig_diff = _significance((paired["ic_ml"] - paired["ic_rule"]).to_numpy())
    if sig_diff is not None:
        print(f"  ML−Rule 差: {sig_diff['mean']:+.4f}  "
              f"t={sig_diff['t']:+.2f}, p≒{sig_diff['p']:.3f}(正規近似, {sig_diff['n']} 日対)")
    print(f"NDCG@{args.topk} (日次平均):")
    print(f"  ML   : {ndcg_ml:.4f}")
    print(f"  Rule : {ndcg_rule:.4f}")

    verdict = "上回る" if (np.isfinite(ic_ml) and np.isfinite(ic_rule) and ic_ml > ic_rule) else "上回らない/不明"
    print(f"\n判定: ML は Rank-IC でルール単純合算を {verdict}。")
    print(
        "\n【前提】OOS 区間の母数が限られると Rank-IC の統計的有意性に限界がある"
        "（株式の Rank-IC は本来 0.02〜0.05、薄い母数では符号すら不安定）。また master は単一スナップショットのため"
        "survivorship バイアスが残る（上場廃止/新規上場が母集団に反映されず結果は楽観方向に偏りうる）。"
        "なお t統計量・p値・符号検定は正規近似で、標本日数が小さいほど近似は粗い。"
        "段階2の目的はパイプライン疎通＋ルール単純合算を超えうるかの一次確認。"
        "実運用精度の作り込みはメタラベル/ボラ調整/履歴拡張を行う段階3以降。"
    )


if __name__ == "__main__":
    main()
