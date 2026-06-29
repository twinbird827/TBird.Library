"""段階2 ML パイプラインの最小回帰（F1/F2/F7-F10 の検証手段）。

実行（`_Apps/ml` をカレントにする。pyproject.toml/uv.lock が直下にあるため）:
  cd _Apps/ml
  uv run pytest

最重要のリーク是正 F1（embargo）はモデル出力を変える安全性クリティカル変更だが、C# の F6 SelfTest では
Python ロジックを固定できないため、ここで in-process に固定する。
"""
from __future__ import annotations

import sqlite3
import sys

import numpy as np
import pandas as pd
import pytest

import db as dbmod
import features
import predict
import train


def _calendar(n: int, start: str = "2024-01-01") -> np.ndarray:
    """連続した取引日カレンダー（embargo は「取引日距離」＝カレンダー上の位置で測るので暦日連続で可）。"""
    return np.array(pd.date_range(start, periods=n, freq="D").values)


# ---------- F1: Purged K-Fold の embargo が「取引日距離」で正しく効く ----------
def test_purged_folds_embargo_in_trading_days_uneven():
    horizon = 5
    cal = _calendar(100)
    # 不均一間隔のリバランス日（IS 末尾の未確定日が間引かれて間隔が一定でないケースを模す）。
    pos = [0, 5, 9, 12, 20, 27, 33, 40, 48, 55, 60, 66, 70, 77, 80, 88, 93, 99]
    reb = cal[pos]

    folds = train._purged_folds(reb, 4, cal, horizon)
    assert folds, "fold が空（少なくとも1 fold は構成できるはず）"

    for tr, va in folds:
        assert len(tr) > 0 and len(va) > 0
        # 生き残る train 日と各 val 日の距離が「実取引日で」horizon 以上離れること。
        # （embargo は horizon 取引日を除外し、生き残る最近接 train 日はさらに外側＝実距離は horizon を上回る。
        #   よって厳密等値でなく `>= horizon` の下限で固定する。）
        for v in va:
            iv = int(np.searchsorted(cal, v))
            for t in tr:
                it = int(np.searchsorted(cal, t))
                assert abs(it - iv) >= horizon


# ---------- F2: fold 構成不能なら空 fold（main の事前ガード SystemExit のトリガ条件）----------
def test_purged_folds_empty_when_insufficient_dates():
    cal = _calendar(50)
    # リバランス日数 3 < k=4 → 空（main はこれを検知して原因明示 SystemExit を投げる）。
    assert train._purged_folds(cal[[0, 1, 2]], 4, cal, 5) == []
    # 十分な日数なら非空（ガードが正常系を誤って止めないことの裏取り）。
    assert train._purged_folds(cal[: 20], 4, cal, 2)


# ---------- F7: 欠損出来高は NaN 保持（0 希釈しない）＋補完規約に乗る ----------
def _one_code_bars(n: int = 40) -> pd.DataFrame:
    dates = pd.date_range("2024-01-01", periods=n, freq="D")
    px = np.linspace(100.0, 140.0, n)
    return pd.DataFrame({
        "Code": "A",
        "Date": dates,
        "AdjOpen": px,
        "AdjHigh": px + 1.0,
        "AdjLow": px - 1.0,
        "AdjClose": px,
        "AdjVolume": np.full(n, 1000.0),
    })


def test_per_code_raw_volume_nan_preserved_not_diluted():
    g = _one_code_bars()
    nan_idx = 25
    g.loc[g.index[nan_idx], "AdjVolume"] = np.nan

    out = features._per_code_raw(g).reset_index(drop=True)
    # 当日出来高 NaN の行は vol_ratio が NaN（旧 fillna(0.0) なら 0 になり 0 希釈する）。
    assert np.isnan(out.loc[nan_idx, "vol_ratio"])


def test_cross_section_normalize_imputes_nan():
    # 同一 Date 内に NaN を含む生特徴量 → Rank=0.5 / Z=0 に補完される（F7 の欠損行がここに乗る）。
    df = pd.DataFrame({
        "Date": pd.to_datetime(["2024-01-01"] * 3),
        "Code": ["A", "B", "C"],
    })
    for col in features.BASE_FEATURES:
        df[col] = [1.0, 2.0, np.nan]

    out = features._cross_section_normalize(df)
    for col in features.BASE_FEATURES:
        assert out[f"{col}_rank"].iloc[2] == 0.5
        assert out[f"{col}_z"].iloc[2] == 0.0


# ---------- F8: 未ソート入力でも _per_code_raw が Date 昇順前提の正特徴量を返す ----------
def test_per_code_raw_sorts_unsorted_input():
    g = _one_code_bars()
    out_sorted = features._per_code_raw(g).reset_index(drop=True)
    out_reversed = features._per_code_raw(g.iloc[::-1]).reset_index(drop=True)
    pd.testing.assert_frame_equal(out_sorted, out_reversed)


# ---------- F9: write_mlscores の expect 検証（in-memory sqlite・原子的 rollback）----------
def _make_signals_db() -> sqlite3.Connection:
    conn = sqlite3.connect(":memory:")
    conn.execute(
        "CREATE TABLE Signals (Date TEXT, Code TEXT, Passed INTEGER, RuleScore REAL, MlScore REAL)"
    )
    conn.executemany(
        "INSERT INTO Signals (Date, Code, Passed, MlScore) VALUES (?, ?, 1, NULL)",
        [("2025-01-06", "A"), ("2025-01-06", "B")],
    )
    conn.commit()
    return conn


def test_write_mlscores_expect_match_and_mismatch():
    conn = _make_signals_db()
    try:
        scores = pd.DataFrame({
            "Date": pd.to_datetime(["2025-01-06", "2025-01-06"]),
            "Code": ["A", "B"],
            "MlScore": [0.1, 0.2],
        })
        # 一致 → 正常終了し rowcount=2。
        assert dbmod.write_mlscores(conn, scores, expect=2) == 2
        assert conn.execute("SELECT MlScore FROM Signals WHERE Code='A'").fetchone()[0] == 0.1

        # 不一致（存在しない Code を混ぜ rowcount=2 != expect=3）→ ValueError かつ rollback。
        bad = pd.DataFrame({
            "Date": pd.to_datetime(["2025-01-06", "2025-01-06", "2025-01-06"]),
            "Code": ["A", "B", "ZZZ"],
            "MlScore": [0.9, 0.9, 0.9],
        })
        with pytest.raises(ValueError):
            dbmod.write_mlscores(conn, bad, expect=3)

        # rollback により bad の書込みは反映されず、直前 commit の 0.1 を保持（部分書込みを残さない）。
        assert conn.execute("SELECT MlScore FROM Signals WHERE Code='A'").fetchone()[0] == 0.1
    finally:
        conn.close()


# ---------- F10: 同一 (Code,DiscloseDate) に複数 DocType → PER/PBR が入力順非依存で決定的 ----------
def test_attach_financials_deterministic_with_duplicate_doctype():
    raw = pd.DataFrame({
        "Code": ["A"],
        "Date": pd.to_datetime(["2024-06-30"]),
        "adjclose_asof": [100.0],
    })
    fin = pd.DataFrame({
        "Code": ["A", "A"],
        "DiscloseDate": pd.to_datetime(["2024-03-31", "2024-03-31"]),
        "DocType": ["FY", "Q1"],  # 辞書順最大="Q1"（'Q'>'F'）→ keep="last" で Q1 を採用。
        "Eps": [10.0, 5.0],
        "Bps": [50.0, 25.0],
        "Equity": [1000.0, 500.0],
        "TotalAssets": [2000.0, 1000.0],
    })

    out_a = features._attach_financials(raw.copy(), fin.copy())
    out_b = features._attach_financials(raw.copy(), fin.iloc[::-1].copy())  # 入力順を反転

    # 入力順を変えても同値（決定的）。
    assert out_a["per_inv"].iloc[0] == out_b["per_inv"].iloc[0]
    assert out_a["pbr_inv"].iloc[0] == out_b["pbr_inv"].iloc[0]
    assert out_a["equity_ratio"].iloc[0] == out_b["equity_ratio"].iloc[0]
    # Q1 行（Eps=5）採用 → per=100/5=20, per_inv=0.05。
    assert out_a["per_inv"].iloc[0] == pytest.approx(1.0 / (100.0 / 5.0))


# ---------- 段階3a: train.py の argparse 契約変更（--full の条件付き必須・既存 --is/--oos 経路の不変）----------
# DB に触れず argparse のディスパッチだけを固定する（_run_full/_run_walkforward を monkeypatch で no-op 化）。
def test_full_requires_train_end(monkeypatch):
    # --full 指定で --train-end 不在なら ap.error → SystemExit（DB アクセス前に弾く）。
    monkeypatch.setattr(sys, "argv", ["train.py", "--full", "--db", "x.db"])
    with pytest.raises(SystemExit):
        train.main()


def test_full_dispatches_without_is_oos(monkeypatch):
    # --full --train-end なら --is/--oos 省略でも通り _run_full にディスパッチされる。
    captured = {}
    monkeypatch.setattr(train, "_run_full", lambda args: captured.setdefault("args", args))
    monkeypatch.setattr(sys, "argv", ["train.py", "--full", "--db", "x.db", "--train-end", "2025-06-27"])
    train.main()
    assert captured["args"].train_end == "2025-06-27"
    assert captured["args"].is_window is None and captured["args"].oos_window is None


def test_walkforward_requires_is_oos(monkeypatch):
    # --full 不在で --is/--oos 省略なら ap.error → SystemExit（既存契約の維持）。
    monkeypatch.setattr(sys, "argv", ["train.py", "--db", "x.db"])
    with pytest.raises(SystemExit):
        train.main()


def test_walkforward_dispatches_with_is_oos(monkeypatch):
    # 既存 --is/--oos 経路が不変: --full 無しで _run_walkforward にディスパッチされる。
    captured = {}
    monkeypatch.setattr(train, "_run_walkforward", lambda args: captured.setdefault("args", args))
    monkeypatch.setattr(sys, "argv", ["train.py", "--db", "x.db", "--is", "2024", "--oos", "2025"])
    train.main()
    assert captured["args"].is_window == "2024" and captured["args"].oos_window == "2025"


# ---------- 段階3a: predict.py の FULL モデル自動解決（降順最新）・不在時 SystemExit ----------
def test_resolve_latest_model_descending(monkeypatch, tmp_path):
    models = tmp_path / "models"
    models.mkdir()
    for name in (
        "lambdarank_FULL_20240101.txt",
        "lambdarank_FULL_20250101.txt",
        "lambdarank_FULL_20231231.txt",
    ):
        (models / name).write_text("x", encoding="utf-8")
    monkeypatch.setattr(predict, "MODELS_DIR", str(models))
    assert predict._resolve_latest_model().endswith("lambdarank_FULL_20250101.txt")


def test_resolve_latest_model_absent_raises(monkeypatch, tmp_path):
    empty = tmp_path / "empty_models"
    empty.mkdir()
    monkeypatch.setattr(predict, "MODELS_DIR", str(empty))
    with pytest.raises(SystemExit):
        predict._resolve_latest_model()


def test_resolve_target_date_max(tmp_path):
    # MAX(Date) は TEXT 辞書順=時系列順で最新営業日を返し、pd.Timestamp に変換される。
    db_path = str(tmp_path / "trade.db")
    conn = sqlite3.connect(db_path)
    conn.execute("CREATE TABLE DailyBars (Code TEXT, Date TEXT)")
    conn.executemany(
        "INSERT INTO DailyBars (Code, Date) VALUES (?, ?)",
        [("A", "2025-06-25"), ("A", "2025-06-27"), ("A", "2025-06-26")],
    )
    conn.commit()
    conn.close()
    assert predict._resolve_target_date(db_path) == pd.Timestamp("2025-06-27")


def test_resolve_target_date_empty_raises(tmp_path):
    # DailyBars が空なら silent fallback せず SystemExit（採点対象日を解決できない）。
    db_path = str(tmp_path / "empty.db")
    conn = sqlite3.connect(db_path)
    conn.execute("CREATE TABLE DailyBars (Code TEXT, Date TEXT)")
    conn.commit()
    conn.close()
    with pytest.raises(SystemExit):
        predict._resolve_target_date(db_path)
