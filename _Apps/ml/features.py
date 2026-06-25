"""特徴量生成（点-in-time・クロスセクション正規化）。

段階1の RuleEngine/TechnicalIndicators と同じ先読み防止規約を pandas で踏襲する:
  - 価格は Date<=t、財務は DiscloseDate<=t のみ参照。
  - 各指標は後ろ向き rolling/shift のみ（t の行は Date<=t の情報だけで決まる）。

クロスセクション正規化（設計§3.2.2）: 同一リバランス日 Date の銘柄群で Rank（パーセンタイル 0..1）と
ZScore を計算し市場全体の上下を相殺する。モデル入力は原則 Rank/Z（市場水準非依存）。

**推論も学習も同じこの関数を通る**ため train-serve skew は定義上ゼロ。欠損補完も一箇所
（Rank=0.5 / Z=0）に固定し、学習・推論で同一の補完が走る。

特徴量は MVP として 12 系統に限定（低 SNR・限られた OOS 母数で列を増やすと過学習）。
"""
from __future__ import annotations

import numpy as np
import pandas as pd

# 流動性・出来高比の窓（営業日）。
LIQUIDITY_WINDOW = 20
VOLUME_WINDOW = 20

# クロスセクション正規化前の「生特徴量」列名。
BASE_FEATURES = [
    "mom5",
    "mom20",
    "mom60",
    "sma25_dev",
    "sma_ratio",
    "rsi14",
    "atr_pct",
    "liquidity",
    "vol_ratio",
    "per_inv",  # 1/PER（高いほど割安。PER の逆数で欠損/負を NaN 化しやすい）
    "pbr_inv",  # 1/PBR
    "equity_ratio",
]


def _rsi(adj_close: pd.Series, period: int = 14) -> pd.Series:
    """Wilder 平滑なしの単純移動平均版 RSI（RuleEngine の Rsi と同方針）。"""
    delta = adj_close.diff()
    gain = delta.clip(lower=0.0)
    loss = (-delta).clip(lower=0.0)
    avg_gain = gain.rolling(period).mean()
    avg_loss = loss.rolling(period).mean()
    rs = avg_gain / avg_loss.replace(0.0, np.nan)
    return 100.0 - 100.0 / (1.0 + rs)


def _atr_pct(high: pd.Series, low: pd.Series, close: pd.Series, period: int = 14) -> pd.Series:
    """ATR / 価格（ボラ）。True Range の単純移動平均を終値で正規化。"""
    prev_close = close.shift(1)
    tr = pd.concat(
        [(high - low), (high - prev_close).abs(), (low - prev_close).abs()], axis=1
    ).max(axis=1)
    atr = tr.rolling(period).mean()
    return atr / close


def _per_code_raw(g: pd.DataFrame) -> pd.DataFrame:
    """1銘柄の生特徴量を計算（すべて後ろ向き＝点-in-time）。"""
    # shift/rolling は Date 昇順前提。呼出側（build_features）はソートしないため、ここで防御的に並べ替える
    # （冪等・微小コスト・自己完結。read_daily_bars のソート依存に頼らず未ソート入力でも正特徴量）。
    g = g.sort_values("Date")
    c = g["AdjClose"]
    # 欠損出来高は NaN 保持（fillna(0.0) は rolling 平均を 0 で希釈し vol_ratio 過大化/liquidity 過小化を招く）。
    v = g["AdjVolume"]

    out = pd.DataFrame(index=g.index)
    out["Code"] = g["Code"].values
    out["Date"] = g["Date"].values
    # as-of 結合で PER/PBR に使う「その bar 日の調整後終値」を持ち回る。
    out["adjclose_asof"] = c.values

    out["mom5"] = c / c.shift(5) - 1.0
    out["mom20"] = c / c.shift(20) - 1.0
    out["mom60"] = c / c.shift(60) - 1.0

    sma25 = c.rolling(25).mean()
    sma75 = c.rolling(75).mean()
    out["sma25_dev"] = c / sma25 - 1.0
    out["sma_ratio"] = sma25 / sma75 - 1.0

    out["rsi14"] = _rsi(c, 14)
    out["atr_pct"] = _atr_pct(g["AdjHigh"], g["AdjLow"], c, 14)

    # 流動性: RuleEngine の流動性フィルタと同じ AdjClose×AdjVolume 近似に揃える
    # （実列 TurnoverValue=Va は分割調整前で連続性が無いため使わない＝ルール層との skew 回避）。
    # turnover も NaN 伝播（欠損出来高日を 0 として平均に混ぜない）。min_periods で欠損混入時も
    # 過小サンプルでの平均算出を防ぐ。欠損結果 NaN は _cross_section_normalize の補完（Rank=0.5/Z=0）に乗る。
    turnover = c * v
    out["liquidity"] = np.log1p(turnover.rolling(LIQUIDITY_WINDOW, min_periods=LIQUIDITY_WINDOW // 2).mean())
    # 出来高比: 当日出来高 / 直近平均（当日は分母から除外＝RuleEngine と同方針）。
    vol_avg = v.shift(1).rolling(VOLUME_WINDOW, min_periods=VOLUME_WINDOW // 2).mean()
    out["vol_ratio"] = v / vol_avg.replace(0.0, np.nan)
    return out


def _attach_financials(raw: pd.DataFrame, fin: pd.DataFrame) -> pd.DataFrame:
    """財務を as-of（DiscloseDate<=Date）でマージし PER/PBR/自己資本比率を付ける。

    raw は build_features の as-of 結合済みフレーム（Date=リバランス日、adjclose_asof=直近終値）。
    """
    raw = raw.sort_values(["Date", "Code"]).reset_index(drop=True)
    if fin.empty:
        raw["per_inv"] = np.nan
        raw["pbr_inv"] = np.nan
        raw["equity_ratio"] = np.nan
        return raw

    # F10: FinSummary は複合PK (Code,DiscloseDate,DocType) で (Code,DiscloseDate) は非一意。同一開示日に
    # 複数 DocType 行があると merge_asof(backward) が入力順最後の行を引き PER/PBR/自己資本比率が非決定的に
    # なりうる。as-of 前に (Code,DiscloseDate) を決定的に1行へ縮約する（DocType 辞書順最大＝入力順非依存）。
    # DocType の意味的優先（通期優先等）は実 DocType 値確認後＝段階3送り（keep="last" は決定性確保の暫定規則）。
    fin = fin.sort_values(["Code", "DiscloseDate", "DocType"]).drop_duplicates(
        ["Code", "DiscloseDate"], keep="last"
    )

    # merge_asof は by グループごとに left.Date >= right.DiscloseDate の直近を引く（点-in-time）。
    fin_sorted = fin.sort_values("DiscloseDate")
    raw_sorted = raw.sort_values("Date")
    merged = pd.merge_asof(
        raw_sorted,
        fin_sorted,
        left_on="Date",
        right_on="DiscloseDate",
        by="Code",
        direction="backward",
    )

    eps = merged["Eps"]
    bps = merged["Bps"]
    eq = merged["Equity"]
    ta = merged["TotalAssets"]
    px = merged["adjclose_asof"]  # as-of 結合で持ち回った直近終値（リバランス日 t 時点）

    # PER≈Px/Eps、PBR≈Px/Bps。Eps<=0/Bps<=0/欠損は NaN（割安度として無意味なため）。
    per = px / eps.where(eps > 0)
    pbr = px / bps.where(bps > 0)
    merged["per_inv"] = 1.0 / per
    merged["pbr_inv"] = 1.0 / pbr
    merged["equity_ratio"] = eq / ta.where(ta > 0)
    return merged.sort_values(["Date", "Code"]).reset_index(drop=True)


def _cross_section_normalize(df: pd.DataFrame) -> pd.DataFrame:
    """同一 Date 内で各生特徴量を Rank(0..1) と ZScore に変換し、欠損を補完する。

    Rank 欠損→0.5（中央）、Z 欠損→0（平均）。学習・推論で同一補完が走る。
    返り値の特徴量列は `{base}_rank` / `{base}_z`。
    """
    out = df[["Date", "Code"]].copy()
    grp = df.groupby("Date", sort=True)
    for f in BASE_FEATURES:
        # Rank: パーセンタイル（同一 Date 内）。NaN は rank から除外され後で 0.5 補完。
        rank = grp[f].rank(pct=True, method="average")
        out[f"{f}_rank"] = rank.fillna(0.5)
        # Z: (x - mean) / std（同一 Date 内）。std=0/NaN は 0。
        mean = grp[f].transform("mean")
        std = grp[f].transform("std")
        z = (df[f] - mean) / std.replace(0.0, np.nan)
        out[f"{f}_z"] = z.fillna(0.0)
    return out


def feature_columns() -> list[str]:
    """モデル入力列（Rank/Z）の一覧。"""
    cols: list[str] = []
    for f in BASE_FEATURES:
        cols.append(f"{f}_rank")
        cols.append(f"{f}_z")
    return cols


def build_features(bars: pd.DataFrame, fin: pd.DataFrame, universe: pd.DataFrame) -> pd.DataFrame:
    """点-in-time 特徴量を生成し、リバランス母集団（universe=Passed の (Date,Code)）に絞って返す。

    返り値: 列 Date, Code, {base}_rank, {base}_z。クロスセクション正規化は universe 内で行う
    （足切り後の銘柄群で市場水準を相殺する＝設計§3.2.2）。
    """
    # 1銘柄ごとに生特徴量（全 bar 日ぶん。後ろ向きなので点-in-time）。
    raw = (
        bars.groupby("Code", group_keys=False)[bars.columns.tolist()]
        .apply(_per_code_raw)
        .reset_index(drop=True)
    )

    # universe（Passed の (Date,Code)）の各リバランス日 t に対し、bar.Date<=t の最新 bar を
    # **as-of 結合**で引く（停止等で t ちょうどの bar が無い銘柄も必ず1行になる）。
    # 完全一致 inner join だと t に bar が無い Passed 行が落ち、その行の MlScore が null のままになって
    # C# の ML backtest（Passed かつ MlScore=null で throw）を止めるため、as-of で全 universe 行をカバーする。
    uni = universe[["Date", "Code"]].drop_duplicates().sort_values("Date")
    raw_sorted = raw.sort_values("Date")
    raw_u = pd.merge_asof(uni, raw_sorted, on="Date", by="Code", direction="backward")

    with_fin = _attach_financials(raw_u, fin)

    # クロスセクション正規化（同一リバランス日 Date 内）。
    normalized = _cross_section_normalize(with_fin)

    # 回帰ガード: as-of 結合は universe の全 (Date,Code) を必ず1行で被覆する。
    # 完全一致 inner join に戻すと t に bar が無い Passed 行が落ち、その行の MlScore が
    # null のままになって C# の ML backtest（Passed かつ MlScore=null で throw）を止める。
    if len(normalized) != len(uni):
        raise ValueError(
            f"特徴量行数 {len(normalized)} が universe {len(uni)} と不一致＝as-of 被覆の回帰。"
            "build_features の merge_asof（direction='backward'）を確認してください。"
        )
    return normalized
