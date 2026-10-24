"""ラベル生成（フォワード相対リターン → gain。先読み防止最優先）。

フォワードリターンはインデックス基準で off-by-one を排除する（SimulateTradeAsync の forward と同一定義）:
  リバランス日 t の「翌営業日以降のバー列」を昇順に並べ fwd[0], fwd[1], … とし、
  - エントリ = fwd[0] の AdjOpen（無ければ AdjClose）
  - エグジット = fwd[H] の AdjClose（H=ラベルホライズン、既定 20 = BacktestOptions.MaxHoldDays と一致）
  → FwdReturn = exit/entry - 1

SimulateTradeAsync は Take(MaxHoldDays+1) で fwd[0..MaxHoldDays] を取り MaxHoldDays 到達時に
fwd[MaxHoldDays] の AdjClose でエグジットするため、H=MaxHoldDays なら両者のエグジットバーは
同一インデックスで一致する（「t+1+H 営業日後」と数えると off-by-one を入れやすいのでインデックスで定義）。

注: バックテストは ATR ストップ/MaxHoldDays で手仕舞いするためエグジットが固定 H と異なるが、
ランキングは相対順位を学ぶので固定 H ラベルで可（実損益は C# バックテストで別途集計）。

除外規約:
  - 未確定行: fwd[H] が DB 末尾を超える行（直近約 H 日）はラベル確定不能 → 学習から除外
    （ここを混ぜると未来情報リーク）。
  - エントリ不能行: エントリバー fwd[0] が存在しない（フォワード本数<2）最末尾行も除外し、
    SimulateTradeAsync が forward.Count<2 で trade を null 除外する母数と揃える。
"""
from __future__ import annotations

import numpy as np
import pandas as pd

DEFAULT_HORIZON = 20  # BacktestOptions.MaxHoldDays と一致させる。
N_QUANTILES = 5       # LambdaRank gain を 0..4 に離散化。


def build_labels(bars: pd.DataFrame, universe: pd.DataFrame, horizon: int = DEFAULT_HORIZON) -> pd.DataFrame:
    """universe（リバランス母集団 (Date,Code)）にフォワードリターンと LabelGain を付ける。

    返り値: 列 Date, Code, FwdReturn, LabelGain, EntryFeasible, LabelConfirmed。
      - EntryFeasible: フォワード本数>=2（SimulateTradeAsync が trade を作る母数）。
      - LabelConfirmed: fwd[horizon] が存在しラベル確定（True の行のみ学習に使う）。
      - LabelGain: 同一 Date 内で FwdReturn を 5 分位に離散化（確定行のみ。未確定は -1）。
    """
    # 銘柄ごとに昇順の日付・価格配列を用意（searchsorted で点-in-time にインデックス参照）。
    by_code: dict[str, dict] = {}
    for code, g in bars.sort_values(["Code", "Date"]).groupby("Code", sort=False):
        by_code[code] = {
            "dates": g["Date"].to_numpy(),
            "open": g["AdjOpen"].to_numpy(dtype=float),
            "close": g["AdjClose"].to_numpy(dtype=float),
        }

    uni = universe[["Date", "Code"]].drop_duplicates().reset_index(drop=True)
    fwd_ret = np.full(len(uni), np.nan)
    entry_ok = np.zeros(len(uni), dtype=bool)
    confirmed = np.zeros(len(uni), dtype=bool)

    for i, (t, code) in enumerate(zip(uni["Date"].to_numpy(), uni["Code"].to_numpy())):
        arr = by_code.get(code)
        if arr is None:
            continue
        dates = arr["dates"]
        # 翌営業日以降の先頭インデックス（Date>t の最初）。
        pos = int(np.searchsorted(dates, t, side="right"))
        n_forward = len(dates) - pos
        if n_forward < 2:
            continue  # エントリ不能（SimulateTradeAsync forward.Count<2 と同母数で除外）。
        entry_ok[i] = True

        entry = arr["open"][pos]
        if not np.isfinite(entry):
            entry = arr["close"][pos]
        if not np.isfinite(entry) or entry <= 0:
            entry_ok[i] = False
            continue

        exit_idx = pos + horizon
        if exit_idx < len(dates):
            exit_px = arr["close"][exit_idx]
            if np.isfinite(exit_px):
                fwd_ret[i] = exit_px / entry - 1.0
                confirmed[i] = True

    out = uni.copy()
    out["FwdReturn"] = fwd_ret
    out["EntryFeasible"] = entry_ok
    out["LabelConfirmed"] = confirmed

    # 同一 Date 内で確定行の FwdReturn を 5 分位に離散化（LambdaRank gain は整数）。
    out["LabelGain"] = -1
    conf = out["LabelConfirmed"]
    if conf.any():
        gain = (
            out.loc[conf]
            .groupby("Date")["FwdReturn"]
            .transform(lambda s: _safe_qcut(s, N_QUANTILES))
        )
        out.loc[conf, "LabelGain"] = gain.astype(int)
    return out


def _safe_qcut(s: pd.Series, q: int) -> pd.Series:
    """同一 Date 内で q 分位に離散化。同値過多/サンプル不足は順位ベースに退避。"""
    n = s.notna().sum()
    if n < 2:
        return pd.Series(0, index=s.index)
    bins = min(q, n)
    try:
        codes = pd.qcut(s, bins, labels=False, duplicates="drop")
    except ValueError:
        codes = None
    if codes is None or codes.nunique(dropna=True) < 2:
        # 退避: パーセンタイル順位を q 段にスケール（同値は中央寄せ）。
        rank = s.rank(pct=True, method="average")
        codes = np.floor(rank * (q - 1e-9)).clip(0, q - 1)
    return codes.fillna(0).astype(int)
