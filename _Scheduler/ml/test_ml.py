"""段階2 ML パイプラインの最小回帰（F1/F2/F7-F10 の検証手段）。

実行（`_Scheduler/ml` をカレントにする。pyproject.toml/uv.lock が直下にあるため）:
  cd _Scheduler/ml
  uv run pytest

最重要のリーク是正 F1（embargo）はモデル出力を変える安全性クリティカル変更だが、C# の F6 SelfTest では
Python ロジックを固定できないため、ここで in-process に固定する。
"""
from __future__ import annotations

import sqlite3
import sys
from types import SimpleNamespace

import numpy as np
import optuna
import pandas as pd
import pytest

import db as dbmod
import explore
import explore_triggers as et
import features
import ml_common
import predict
import rules
import train
import triggers


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


# ---------- read_fin_summaries が Eps/Bps を開示日より後の分割係数で AdjClose の株数基準へ換算 ----------
def test_read_fin_summaries_applies_share_factor_after_disclose():
    conn = sqlite3.connect(":memory:")
    try:
        conn.execute("CREATE TABLE DailyBars (Code TEXT, Date TEXT, AdjustmentFactor REAL)")
        conn.execute(
            "CREATE TABLE FinSummaries (Code TEXT, DiscloseDate TEXT, DocType TEXT, "
            "Eps REAL, Bps REAL, Equity REAL, TotalAssets REAL, "
            "Sales REAL, OperatingProfit REAL, NetProfit REAL, CurrentFiscalYearEnd TEXT, NextFiscalYearEnd TEXT, "
            "ForecastOperatingProfit REAL, ForecastNetProfit REAL, NextForecastOperatingProfit REAL, "
            "NextForecastNetProfit REAL, ForecastNonConsolidatedOperatingProfit REAL, "
            "ForecastNonConsolidatedNetProfit REAL)"
        )
        conn.executemany(
            "INSERT INTO DailyBars VALUES (?,?,?)",
            [
                ("A", "2024-03-31", 1.0),
                ("A", "2024-05-01", 0.5),   # 開示日より後 → 掛かる
                ("B", "2024-03-31", 0.5),   # 開示日当日 → 掛からない
                ("B", "2024-01-15", 0.5),   # 開示日より前 → 掛からない
            ],
        )
        conn.executemany(
            "INSERT INTO FinSummaries (Code, DiscloseDate, DocType, Eps, Bps, Equity, TotalAssets) "
            "VALUES (?,?,?,?,?,?,?)",
            [
                ("A", "2024-03-31", "FY", 10.0, 100.0, 1000.0, 2000.0),
                ("B", "2024-03-31", "FY", 10.0, 100.0, 1000.0, 2000.0),
            ],
        )
        fin = dbmod.read_fin_summaries(conn).set_index("Code")
    finally:
        conn.close()

    assert fin.loc["A", "Eps"] == pytest.approx(5.0)
    assert fin.loc["A", "Bps"] == pytest.approx(50.0)
    assert fin.loc["B", "Eps"] == pytest.approx(10.0)
    assert fin.loc["B", "Bps"] == pytest.approx(100.0)
    assert (fin["Equity"] == 1000.0).all()
    assert list(fin.columns) == [
        "DiscloseDate", "DocType", "Eps", "Bps", "Equity", "TotalAssets", "Sales", "OperatingProfit", "NetProfit",
        "CurrentFiscalYearEnd", "NextFiscalYearEnd", "ForecastOperatingProfit", "ForecastNetProfit",
        "NextForecastOperatingProfit", "NextForecastNetProfit",
        "ForecastNonConsolidatedOperatingProfit", "ForecastNonConsolidatedNetProfit",
    ]


# ---------- rules.py: 形成日当日以降の開示・基準日 + 7 暦日前の信用残を使わない ----------
def test_fin_asof_excludes_disclosure_on_formation_day():
    d = pd.Timestamp("2024-06-28")
    fin = pd.DataFrame({
        "Code": ["A", "A", "B"],
        "DiscloseDate": [d - pd.Timedelta(days=1), d, d],
        "v": [1.0, 2.0, 3.0],
    })
    out = rules.fin_asof(fin, "v", pd.DatetimeIndex([d]), pd.Index(["A", "B"]))
    assert out.loc[d, "A"] == 1.0
    assert np.isnan(out.loc[d, "B"])


def test_margin_asof_waits_seven_days_after_record_date():
    d = pd.Timestamp("2024-06-28")
    mi = pd.DataFrame({
        "Code": ["A", "A", "B"],
        "Date": [d - pd.Timedelta(days=7), d - pd.Timedelta(days=6), d - pd.Timedelta(days=6)],
        "v": [1.0, 2.0, 3.0],
    })
    out = rules.margin_asof(mi, "v", pd.DatetimeIndex([d]), pd.Index(["A", "B"]))
    assert out.loc[d, "A"] == 1.0
    assert np.isnan(out.loc[d, "B"])


# ---------- triggers.py / explore_triggers.py: 約定・清算・再トリガー・予想修正・t 値・空売り・(b)(c)・損切り ----------
def _market(c, o=None, h=None, l=None, marg=True) -> et.Market:
    """銘柄 A だけの Market。o は省略時 c、h/l は省略時 o と c の高い方/低い方。"""
    c = np.asarray(c, float)
    o = c if o is None else np.asarray(o, float)
    h = np.fmax(o, c) if h is None else np.asarray(h, float)
    l = np.fmin(o, c) if l is None else np.asarray(l, float)
    dates = pd.bdate_range("2024-01-01", periods=len(c))

    def f(v):
        return pd.DataFrame({"A": v}, index=dates)

    return et.Market(SimpleNamespace(
        dates=dates, px=f(c), open=f(o), high=f(h), low=f(l), eligible=f(~np.isnan(c)),
        marginable=f(np.full(len(c), marg)), sec=f(np.full(len(c), "x", dtype=object))))


def _sig(n: int, *ts: int) -> np.ndarray:
    s = np.zeros((n, 1), bool)
    s[list(ts), 0] = True
    return s


def test_simulate_enters_next_open_and_skips_retrigger_while_held():
    c = np.arange(100.0, 112.0)
    o = c - 0.5
    m = _market(c, o)
    tr = et.simulate(m, [et.Leg(_sig(12, 0, 2, 6), 5, False)])
    # t=2 は t=0 の保有中（1〜5）なので数えない。
    assert tr["t"].tolist() == [0, 6]
    assert tr["exit"].tolist() == [5, 11]
    assert tr["px"].tolist() == [c[5], c[11]]
    assert m.bench[0, 5] == pytest.approx(c[5] / o[1] - 1)
    assert tr["ex"].iloc[0] == pytest.approx(c[5] / o[1] - 1 - m.bench[0, 5] - et.COST)


def test_simulate_liquidates_delisted_at_last_close():
    c = np.r_[100.0, 101.0, 102.0, 103.0, 104.0, 105.0, [np.nan] * 6]
    tr = et.simulate(_market(c), [et.Leg(_sig(12, 2), 5, False)])
    assert tr["exit"].tolist() == [7]
    assert tr["px"].tolist() == [105.0]


def test_stop_fills_at_stop_price_intraday_open_on_gap_and_not_for_reversal():
    n = 10
    c = np.full(n, 100.0)
    l = c.copy()
    l[3] = 89.0
    tr = et.simulate(_market(c, l=l), [et.Leg(_sig(n, 0), 5, True)])
    assert tr["exit"].iloc[0] == 3 and tr["px"].iloc[0] == pytest.approx(90.0)

    o = c.copy()
    o[3], l[3] = 85.0, 84.0
    tr = et.simulate(_market(c, o, l=l), [et.Leg(_sig(n, 0), 5, True)])
    assert tr["exit"].iloc[0] == 3 and tr["px"].iloc[0] == 85.0

    # 保有期間の最終日（t+5）の接触も、終値でなく逆指値の価格。
    l = c.copy()
    l[5] = 89.0
    tr = et.simulate(_market(c, l=l), [et.Leg(_sig(n, 0), 5, True)])
    assert tr["exit"].iloc[0] == 5 and tr["px"].iloc[0] == pytest.approx(90.0)

    # 【反発】（stop=False）には損切りが掛からない。
    l[3] = 80.0
    tr = et.simulate(_market(c, l=l), [et.Leg(_sig(n, 0), 5, False)])
    assert tr["exit"].iloc[0] == 5 and tr["px"].iloc[0] == 100.0


def test_short_only_marginable_and_pays_lending_fee():
    n = 10
    c = np.linspace(100.0, 91.0, n)
    empty = et.simulate(_market(c, marg=False), [et.Leg(_sig(n, 0), 5, False)], short=True)
    assert empty.empty
    assert et.summarize(empty, _market(c), et.periods(_market(c).dates), 0)[0]["件数"] == 0
    m = _market(c)
    tr = et.simulate(m, [et.Leg(_sig(n, 0), 5, False)], short=True)
    r = c[5] / c[1] - 1 - m.bench[0, 5]
    assert tr["ex"].iloc[0] == pytest.approx(-r - et.COST - et.LENDING_FEE * 5 / et.DAYS_PER_YEAR)


def test_lookback_ignores_sells_after_trigger_day():
    n, t = 40, 25
    for day, expect in [(t + 1, False), (t, True), (t - 19, True), (t - 20, False)]:
        assert et.lookback(_sig(n, day))[t, 0] == expect


def test_early_sell_exits_at_next_open():
    n = 10
    c = np.arange(100.0, 110.0)
    o = c - 0.5
    tr = et.simulate(_market(c, o), [et.Leg(_sig(n, 0), 5, False, sell=_sig(n, 3))])
    assert tr["exit"].iloc[0] == 4 and tr["px"].iloc[0] == o[4]
    # 買いのトリガー日 t の売りのトリガーは保有中ではない。
    tr = et.simulate(_market(c, o), [et.Leg(_sig(n, 0), 5, False, sell=_sig(n, 0))])
    assert tr["exit"].iloc[0] == 5


def test_nw_t_lag0_matches_explore_t_stat():
    x = pd.Series([0.01, -0.02, 0.03, 0.005, 0.012, -0.004])
    assert et.nw_t(x, 0) == pytest.approx(explore.t_stat(x))


def _fin(code, date, doctype, fye, nfye=None, fop=None, nxfop=None, fncop=None):
    return {"Code": code, "DiscloseDate": pd.Timestamp(date), "DocType": doctype, "CurrentFiscalYearEnd": fye,
            "NextFiscalYearEnd": nfye, triggers.OP_COLS[0]: fop, triggers.OP_COLS[1]: nxfop,
            triggers.OP_COLS[2]: fncop, **{c: np.nan for c in triggers.NP_COLS}}


def test_revisions_compare_same_fiscal_year_and_use_nonconsolidated():
    allfin = pd.DataFrame([
        _fin("A", "2024-02-01", "EarnForecastRevision", "2024-03-31", fop=500.0),
        _fin("A", "2024-05-10", "FYFinancialStatements_Consolidated_JP", "2024-03-31", "2025-03-31", nxfop=100.0),
        # 年度最初の修正は、本決算の来期予想（100）と比べる。前年度の修正（500）とは比べない。
        _fin("A", "2024-08-01", "EarnForecastRevision", "2025-03-31", fop=115.0),
        _fin("A", "2024-08-05", "1QFinancialStatements_Consolidated_JP", "2025-03-31", fop=115.0),
        _fin("A", "2024-11-01", "EarnForecastRevision", "2025-03-31", fop=138.0),
        # 単体決算の会社: 決算短信は連結の列に単体の値、修正は単体の列。
        _fin("B", "2024-02-10", "3QFinancialStatements_NonConsolidated_JP", "2024-03-31", fop=200.0),
        _fin("B", "2024-03-01", "EarnForecastRevision", "2024-03-31", fncop=230.0),
    ])
    out = triggers.revisions(SimpleNamespace(allfin=allfin)).set_index(["Code", "DiscloseDate"])["chg"]
    assert len(out) == 3
    assert out["A", pd.Timestamp("2024-08-01")] == pytest.approx(0.15)
    assert out["A", pd.Timestamp("2024-11-01")] == pytest.approx(0.2)
    assert out["B", pd.Timestamp("2024-03-01")] == pytest.approx(0.15)


def _panel(px: dict, **kw) -> triggers.DailyPanel:
    """px（銘柄 → 終値の列）から作る、メソッドの使える DailyPanel。"""
    px = pd.DataFrame(px, index=pd.bdate_range("2024-01-01", periods=len(next(iter(px.values())))), dtype=float)
    p = object.__new__(triggers.DailyPanel)
    p.__dict__.update(px=px, dates=px.index, codes=px.columns, cidx={c: j for j, c in enumerate(px.columns)},
                      ewi=np.ones(len(px)), **kw)
    return p


def test_sector_pbr_rank_within_eligible_and_sector():
    codes = list("ABCDEFGHI")
    px = {c: [v] * 3 for c, v in zip(codes, [10, 20, 30, 40, 70, 50, 60, 10, 20])}
    elig = pd.DataFrame(True, index=pd.bdate_range("2024-01-01", periods=3), columns=codes)
    elig.loc[elig.index[1], ["F", "G"]] = False
    elig.loc[elig.index[2], "G"] = False
    fin = pd.DataFrame({"Code": codes + ["E"], "DiscloseDate": pd.Timestamp("2023-05-01"), "Bps": 100.0, "q": 4})
    # E は前の期の BPS が正で、直近の本決算の BPS が 0 以下。
    fin.loc[len(codes), ["DiscloseDate", "Bps"]] = [pd.Timestamp("2023-12-01"), -5.0]
    sec = pd.DataFrame([["s1"] * 7 + ["s2"] * 2] * 3, index=elig.index, columns=codes)
    r = triggers._sector_pbr_rank(_panel(px, eligible=elig, sec=sec, fin=fin))
    assert r.iloc[0, :7].tolist() == pytest.approx([1 / 6, 2 / 6, 3 / 6, 4 / 6, np.nan, 5 / 6, 1.0], nan_ok=True)
    assert r.iloc[1].isna().all()  # s1 の母集団が 4、s2 は 2。
    assert r.iloc[2, :6].tolist() == pytest.approx([0.2, 0.4, 0.6, 0.8, np.nan, 1.0], nan_ok=True)
    assert r.iloc[2, 6:].isna().all()


def test_c5_judges_drop_until_previous_day():
    n = 70
    a = np.full(n, 100.0)
    a[61:66] = [95.0, 90.0, 85.0, 80.0, 75.0]
    a[66:] = 79.5
    vol = np.full((n, 1), 1000.0)
    vol[66] = 6000.0
    p = _panel({"A": a})
    p.vol = pd.DataFrame(vol, index=p.dates, columns=p.codes)
    hit = triggers.COMBO_TRIGGERS["C5 急落後の出来高急増の大陽線"](p)["A"]
    assert hit.tolist() == [i == 66 for i in range(n)]
    # t で見ると当日の戻りで −20% を割らない。
    assert p.excess(5)["A"].iloc[66] > -0.20


def test_revision_threshold(monkeypatch):
    p = _panel({"A": [1.0] * 3})
    monkeypatch.setattr(triggers, "revisions", lambda _: pd.DataFrame(
        {"Code": ["A"], "DiscloseDate": [p.dates[1]], "chg": [0.07]}))
    assert not triggers._revision(p, True).to_numpy().any()
    assert triggers._revision(p, True, 0.05)["A"].tolist() == [False, True, False]


def _rev(p, *rows):
    """(Code, 営業日の位置, fye, np, chg) の行から作る revisions の戻り値。"""
    return pd.DataFrame([(c, p.dates[i], fye, np_, chg) for c, i, fye, np_, chg in rows],
                        columns=["Code", "DiscloseDate", "fye", "np", "chg"])


def _fyfin(codes, sh=100.0, bk=20.0):
    return pd.DataFrame({"Code": codes, "DiscloseDate": pd.Timestamp("2023-05-01"), "q": 4, "sh": sh, "bk": bk,
                         "sue": np.nan})


def test_c9_judges_forecast_net_profit_over_mcap(monkeypatch):
    p = _panel({"A": [10.0] * 3, "B": [10.0] * 3}, fin=_fyfin(["A", "B"]))
    # 時価総額 1000。A は 150/1000、B は 50/1000、A の 2 日目は予想純利益が無い。
    monkeypatch.setattr(triggers, "revisions", lambda _: _rev(
        p, ("A", 1, "2025-03-31", 150.0, 0.2), ("B", 1, "2025-03-31", 50.0, 0.2), ("A", 2, "2025-03-31", np.nan, 0.2)))
    hit = triggers.COMBO_TRIGGERS["C9 予想割安×上方修正"](p)
    assert hit["A"].tolist() == [False, True, False]
    assert not hit["B"].any()


def test_c10_needs_earlier_upward_revision_in_same_fiscal_year(monkeypatch):
    p = _panel({c: [10.0] * 3 for c in "ABC"}, fin=_fyfin(list("ABC")))
    monkeypatch.setattr(triggers, "revisions", lambda _: _rev(
        p, ("A", 0, "2025-03-31", np.nan, 0.05), ("A", 2, "2025-03-31", np.nan, 0.15),
        ("B", 1, "2025-03-31", np.nan, -0.1), ("B", 2, "2025-03-31", np.nan, 0.15),
        ("C", 0, "2024-03-31", np.nan, 0.2), ("C", 2, "2025-03-31", np.nan, 0.2)))
    hit = triggers.COMBO_TRIGGERS["C10 割安×2回目以降の上方修正"](p)
    assert hit["A"].tolist() == [False, False, True]
    assert not hit["B"].any() and not hit["C"].any()


@pytest.mark.parametrize("e1, expect", [(5, True), (4, False), (65, False)])
def test_c11_sees_revision_from_t60_to_t1(monkeypatch, e1, expect):
    n, t = 70, 65
    p = _panel({"A": [10.0] * n})
    p.fin = pd.concat([_fyfin(["A"]), pd.DataFrame({"Code": ["A"], "DiscloseDate": [p.dates[t]], "q": [1], "sue": [3.0]})])
    monkeypatch.setattr(triggers, "revisions", lambda _: _rev(p, ("A", e1, "2025-03-31", np.nan, 0.2)))
    hit = triggers.COMBO_TRIGGERS["C11 割安×上方修正後の増益サプライズ"](p)["A"]
    assert hit.tolist() == [i == t and expect for i in range(n)]


def test_f1_progress_gap_vs_previous_year_only_2q_3q():
    p = _panel({c: [1.0] * 3 for c in "ABCD"})
    # A は 0.70 − 0.40、B は通期予想が 0、C は 1Q、D は 0.50 − 0.40。
    p.fin = pd.DataFrame({"Code": list("ABCD"), "DiscloseDate": p.dates[1], "q": [2, 3, 1, 3],
                          "OP": [70.0, 70.0, 70.0, 50.0], triggers.OP_COLS[0]: [100.0, 0.0, 100.0, 100.0],
                          "OP_py": 40.0, "OP_fy": 100.0})
    hit = triggers.COMBO_TRIGGERS["F1 進捗率の上振れ"](p)
    assert hit["A"].tolist() == [False, True, False]
    assert not hit[list("BCD")].to_numpy().any()


def test_f2_landing_vs_latest_forecast_before_fy_statement():
    rows = [
        _fin("A", "2023-05-10", "FYFinancialStatements_Consolidated_JP", "2023-03-31", "2024-03-31", nxfop=100.0),
        _fin("A", "2024-02-01", "EarnForecastRevision", "2024-03-31", fop=110.0),
        # 会計年度の違う予想と、本決算と同じ日の修正は使わない。
        _fin("A", "2024-04-01", "EarnForecastRevision", "2025-03-31", fop=50.0),
        _fin("A", "2024-05-10", "EarnForecastRevision", "2024-03-31", fop=200.0),
        _fin("B", "2024-02-01", "EarnForecastRevision", "2024-03-31", fop=100.0),
    ]
    allfin = pd.DataFrame(rows)
    allfin.loc[4, triggers.NP_COLS[0]] = 100.0
    p = _panel({"A": [1.0] * 100, "B": [1.0] * 100}, allfin=allfin)
    # A は 130 ÷ 110（+18%）。B は営業利益が無いので純利益 120 ÷ 100。
    p.fin = pd.DataFrame({"Code": ["A", "B"], "DiscloseDate": pd.Timestamp("2024-05-10"), "q": 4,
                          "CurrentFiscalYearEnd": "2024-03-31", "OP": [130.0, np.nan], "NP": [np.nan, 120.0]})
    assert triggers._landing(p.fin, p).tolist() == pytest.approx([130 / 110 - 1, 0.2])
    hit = triggers.COMBO_TRIGGERS["F2 着地の上振れ"](p)
    t = p.dates.searchsorted(pd.Timestamp("2024-05-10"))
    assert hit["A"].tolist() == [i == t for i in range(100)]
    assert hit["B"].tolist() == [i == t for i in range(100)]


def test_mcap_tiers_rank_within_eligible():
    codes = list("ABCDEFG")
    p = _panel({c: [v] * 2 for c, v in zip(codes, [1, 2, 3, 4, 5, 6, 100])}, fin=_fyfin(codes, sh=1.0))
    p.eligible = pd.DataFrame(True, index=p.dates, columns=codes).assign(G=False)
    tier = {s: triggers._STRATA[s](p).iloc[0] for s in ("大型", "中型", "小型")}
    assert tier["小型"][tier["小型"]].index.tolist() == ["A", "B"]
    assert tier["中型"][tier["中型"]].index.tolist() == ["C", "D"]
    assert tier["大型"][tier["大型"]].index.tolist() == ["E", "F"]


def test_market_regime_against_200_day_average():
    p = _panel({"A": [1.0] * 202})
    p.ewi = np.r_[np.ones(200), 2.0, 0.5]
    up, down = triggers._STRATA["地合い上"](p)[:, 0], triggers._STRATA["地合い下"](p)[:, 0]
    assert not up[:199].any() and not down[:199].any()
    assert down[199] and not up[199]
    assert up[200] and not down[200]
    assert down[201] and not up[201]


def test_merge_adopted_ors_two_skips():
    a, b, s = _sig(5, 1), _sig(5, 3), _sig(5, 2)
    kw = et.merge_adopted([{"skip": a}, {"sell": s}, {"skip": b}])
    assert (kw["skip"] == (a | b)).all() and kw["sell"] is s


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
        "AdjLow REAL, AdjClose REAL, AdjVolume REAL, AdjustmentFactor REAL, "
        "Close REAL, Volume REAL, TurnoverValue REAL)"
    )
    conn.execute(
        "CREATE TABLE FinSummaries (Code TEXT, DiscloseDate TEXT, DocType TEXT, "
        "Eps REAL, Bps REAL, Equity REAL, TotalAssets REAL, "
        "Sales REAL, OperatingProfit REAL, NetProfit REAL, CurrentFiscalYearEnd TEXT, NextFiscalYearEnd TEXT, "
        "ForecastOperatingProfit REAL, ForecastNetProfit REAL, NextForecastOperatingProfit REAL, "
        "NextForecastNetProfit REAL, ForecastNonConsolidatedOperatingProfit REAL, "
        "ForecastNonConsolidatedNetProfit REAL)"
    )

    # (bar 本数, 価格 base, slope)。D は 136 本＝cal[0..135]（=t-5 まで）で t に bar 無し。
    code_specs = {"A": (160, 100.0, 1.0), "B": (160, 120.0, 0.8), "C": (160, 90.0, 1.2), "D": (136, 110.0, 0.5)}
    bar_rows, fin_rows = [], []
    for code, (n, base, slope) in code_specs.items():
        for i in range(n):
            px = base + i * slope
            bar_rows.append((code, cal[i].strftime("%Y-%m-%d"), px, px + 1.0, px - 1.0, px, 1_000_000.0))
        fin_rows.append((code, cal[100].strftime("%Y-%m-%d"), "FY", 10.0, 50.0, 1000.0, 2000.0))
    conn.executemany(
        "INSERT INTO DailyBars (Code, Date, AdjOpen, AdjHigh, AdjLow, AdjClose, AdjVolume) VALUES (?,?,?,?,?,?,?)",
        bar_rows,
    )
    conn.executemany(
        "INSERT INTO FinSummaries (Code, DiscloseDate, DocType, Eps, Bps, Equity, TotalAssets) "
        "VALUES (?,?,?,?,?,?,?)",
        fin_rows,
    )

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
