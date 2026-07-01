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
import optuna
import pandas as pd
import pytest

import db as dbmod
import features
import ml_common
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


# ---------- F7/F8: 意図的 SystemExit（OOS 空 / 全 trial pruned）の回帰固定 ----------
class _NoopBooster:
    """_run_walkforward の save 経路を無害化する最小スタブ（OOS 空ガードまで到達させるのが目的）。"""

    def save_model(self, _path):
        pass

    def predict(self, x):
        return np.zeros(len(x))


def _walkforward_df() -> pd.DataFrame:
    """_run_walkforward が OOS 空ガードへ到達する最小 df（IS に確定ラベル ≥2 日、OOS は別年で空）。

    dtype 要件: Date=datetime64（_slice の比較互換）, feature_columns 全列=float かつ NaN なし
    （_make_dataset の to_numpy 用）, LabelGain=int, EntryFeasible/LabelConfirmed=bool（is_df のブール索引用）。
    """
    fcols = features.feature_columns()
    dates = pd.to_datetime(["2024-03-01", "2024-03-04", "2024-03-05"])
    rows = [{"Date": d, "Code": code} for d in dates for code in ("A", "B")]
    df = pd.DataFrame(rows)
    df["RuleScore"] = 1.0
    df["MlScore"] = np.nan
    for c in fcols:
        df[c] = 0.0
    df["FwdReturn"] = 0.0
    df["LabelGain"] = 0
    df["EntryFeasible"] = True
    df["LabelConfirmed"] = True
    return df


def test_run_walkforward_oos_empty_systemexit(monkeypatch):
    # load_dataset を手組み df に差し替え、IS=2024（df を含む）/ OOS=2099（空）で oos_df.empty ガード(F7)を固定。
    # _tune_hyperparams/_make_dataset/lgb.train/_ensure_models_dir/save_model を no-op 化し実 DB・実 models/・実学習に触れない。
    df = _walkforward_df()
    bars = pd.DataFrame({"Date": df["Date"].unique()})  # all_trading_days 算出用（値は stub 経路で未使用）。
    monkeypatch.setattr(train, "load_dataset", lambda db, horizon: (df, bars))
    monkeypatch.setattr(train, "_tune_hyperparams", lambda *a, **k: {})
    monkeypatch.setattr(train, "_make_dataset", lambda *a, **k: None)  # dtrain は stub lgb.train で未使用。
    monkeypatch.setattr(train.lgb, "train", lambda *a, **k: _NoopBooster())
    monkeypatch.setattr(train, "_ensure_models_dir", lambda: None)  # hermetic（実 models/ を作らない）。
    monkeypatch.setattr(sys, "argv", ["train.py", "--db", "x.db", "--is", "2024", "--oos", "2099"])
    with pytest.raises(SystemExit):
        train.main()


def test_tune_hyperparams_all_trials_pruned_systemexit(monkeypatch):
    # fold は構成可（_purged_folds stub で非空）だが全 trial が pruned → 完了 trial ゼロガード(F8)で SystemExit。
    # _objective を常時 TrialPruned に差し替え lgb を実行しない（決定的・高速）。lambda は global _objective を
    # 実行時解決するため monkeypatch.setattr(train, "_objective", ...) が効く。
    train_df = pd.DataFrame({"Date": pd.to_datetime(["2024-01-01", "2024-01-02"])})
    monkeypatch.setattr(train, "_purged_folds", lambda *a, **k: [(np.array([0]), np.array([1]))])

    def _always_pruned(*_a, **_k):
        raise optuna.TrialPruned()

    monkeypatch.setattr(train, "_objective", _always_pruned)
    with pytest.raises(SystemExit):
        train._tune_hyperparams(train_df, [], np.array([]), horizon=5, topk=10, trials=2)


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


# ---------- F6: predict.py の --date 厳格パース（不正値は SystemExit、NaT 不使用）----------
def test_parse_target_date_valid():
    assert predict._parse_target_date("2025-06-27") == pd.Timestamp("2025-06-27")


def test_parse_target_date_invalid_raises_systemexit():
    # 不正な月日（errors="raise" が ValueError）/ 非日付文字列（ParserError=ValueError 派生）→ SystemExit。
    with pytest.raises(SystemExit):
        predict._parse_target_date("2025-13-99")
    with pytest.raises(SystemExit):
        predict._parse_target_date("not-a-date")


# ---------- F9: ml_common.score_and_write（採点→NaN ガード→書戻し）----------
class _StubBooster:
    """score_and_write は booster.predict(ndarray) のみ呼ぶため、固定配列を返す最小スタブで十分。"""

    def __init__(self, values):
        self._values = values

    def predict(self, _x):
        return np.asarray(self._values, dtype=float)


def _scores_frame() -> pd.DataFrame:
    return pd.DataFrame({
        "Date": pd.to_datetime(["2025-01-06", "2025-01-06"]),
        "Code": ["A", "B"],
        "f0_rank": [0.1, 0.2],
    })


def test_score_and_write_writes_rows_and_returns_count():
    conn = _make_signals_db()
    try:
        n = ml_common.score_and_write(_StubBooster([0.7, 0.8]), _scores_frame(), ["f0_rank"], conn, expect=2)
        assert n == 2
        assert conn.execute("SELECT MlScore FROM Signals WHERE Code='A'").fetchone()[0] == 0.7
    finally:
        conn.close()


def test_score_and_write_nan_raises_systemexit():
    conn = _make_signals_db()
    try:
        with pytest.raises(SystemExit):
            ml_common.score_and_write(_StubBooster([0.7, np.nan]), _scores_frame(), ["f0_rank"], conn, expect=2)
        # rollback 不要（NaN は書込み前に弾く）＝MlScore は依然 null。
        assert conn.execute("SELECT MlScore FROM Signals WHERE Code='A'").fetchone()[0] is None
    finally:
        conn.close()


def test_score_and_write_no_write_skips_but_still_guards_nan():
    conn = _make_signals_db()
    try:
        # write=False: 書込まず採点行数を返す（NaN なしは正常）。conn は使われない。
        n = ml_common.score_and_write(_StubBooster([0.7, 0.8]), _scores_frame(), ["f0_rank"], None, write=False)
        assert n == 2
        assert conn.execute("SELECT MlScore FROM Signals WHERE Code='A'").fetchone()[0] is None
        # write=False でも NaN ガードは走る（--no-write 経路でも特徴量欠損を検出）。
        with pytest.raises(SystemExit):
            ml_common.score_and_write(_StubBooster([0.7, np.nan]), _scores_frame(), ["f0_rank"], None, write=False)
    finally:
        conn.close()


# ---------- F4: load_one_day が全期間ロード（load_dataset）と t 行でビット等価 ----------
def _build_trade_db(tmp_path) -> tuple[str, pd.Timestamp, pd.Timestamp]:
    """ビット等価検証用の小さな trade.db を作る。

    A,B,C は t+ まで bar を持つ（Date>t bar が t 行の後ろ向き特徴量に影響しないことを検証）。
    D は t-5 までしか bar が無いが t で Passed（停止間際銘柄＝per-code 全履歴読みでビット等価を検証）。
    返り値: (db_path, t, t0)。
    """
    cal = pd.bdate_range("2024-06-03", periods=160)
    t = cal[140]
    t0 = cal[120]
    db_path = str(tmp_path / "trade.db")
    conn = sqlite3.connect(db_path)
    conn.execute(
        "CREATE TABLE Signals (Date TEXT, Code TEXT, Passed INTEGER, RuleScore REAL, MlScore REAL)"
    )
    conn.execute(
        "CREATE TABLE DailyBars (Code TEXT, Date TEXT, AdjOpen REAL, AdjHigh REAL, "
        "AdjLow REAL, AdjClose REAL, AdjVolume REAL)"
    )
    conn.execute(
        "CREATE TABLE FinSummaries (Code TEXT, DiscloseDate TEXT, DocType TEXT, "
        "Eps REAL, Bps REAL, Equity REAL, TotalAssets REAL)"
    )

    # (bar 本数, 価格 base, slope)。D は 136 本＝cal[0..135]（=t-5 まで）で t に bar 無し。
    code_specs = {"A": (160, 100.0, 1.0), "B": (160, 120.0, 0.8), "C": (160, 90.0, 1.2), "D": (136, 110.0, 0.5)}
    bar_rows, fin_rows = [], []
    for code, (n, base, slope) in code_specs.items():
        for i in range(n):
            px = base + i * slope
            bar_rows.append((code, cal[i].strftime("%Y-%m-%d"), px, px + 1.0, px - 1.0, px, 1_000_000.0))
        fin_rows.append((code, cal[100].strftime("%Y-%m-%d"), "FY", 10.0, 50.0, 1000.0, 2000.0))
    conn.executemany("INSERT INTO DailyBars VALUES (?,?,?,?,?,?,?)", bar_rows)
    conn.executemany("INSERT INTO FinSummaries VALUES (?,?,?,?,?,?,?)", fin_rows)

    sig_rows = [(t0.strftime("%Y-%m-%d"), c, 1, 3.0, None) for c in ("A", "B", "C")]
    sig_rows += [(t.strftime("%Y-%m-%d"), c, 1, float(j + 1), None) for j, c in enumerate(("A", "B", "C", "D"))]
    conn.executemany("INSERT INTO Signals (Date, Code, Passed, RuleScore, MlScore) VALUES (?,?,?,?,?)", sig_rows)
    conn.commit()
    conn.close()
    return db_path, t, t0


def test_load_one_day_bit_equivalent_to_full_load(tmp_path):
    db_path, t, _ = _build_trade_db(tmp_path)
    fcols = features.feature_columns()
    cols = ["Date", "Code", "RuleScore"] + fcols

    df_full, _bars = train.load_dataset(db_path, horizon=20)
    day_full = train._slice(df_full, t, t)[cols].sort_values(["Date", "Code"]).reset_index(drop=True)
    day_scoped = train.load_one_day(db_path, t, horizon=20)[cols].sort_values(["Date", "Code"]).reset_index(drop=True)

    # 停止間際銘柄 D（t に bar 無し）も両者に含まれる（per-code 全履歴読みで as-of 被覆）。
    assert "D" in day_scoped["Code"].values
    # t 行の特徴量・RuleScore が全期間ロードと date-scope ロードでビット等価。
    pd.testing.assert_frame_equal(day_full, day_scoped)


def test_load_one_day_empty_when_no_passed_signals(tmp_path):
    db_path, t, _ = _build_trade_db(tmp_path)
    # signals 未投入の日（t の翌営業日）は空フレーム＝predict.main が .empty を見て SystemExit する経路。
    next_bday = pd.bdate_range(t, periods=2)[1]
    assert train.load_one_day(db_path, next_bday, horizon=20).empty
