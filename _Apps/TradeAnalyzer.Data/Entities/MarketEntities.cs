namespace TradeAnalyzer.Data.Entities;

/// <summary>
/// 上場銘柄マスタ（J-Quants V2 /v2/equities/master）。
/// ポイントインタイム設計: 同一 Code でも基準日 (AsOfDate) ごとに行を持ち、
/// バックテストの各時点で「その日に上場していた母集団」を再現できるようにする。
/// </summary>
public class Stock
{
    /// <summary>マスタ基準日（J-Quants `Date`）。複合キーの一部。</summary>
    public DateOnly AsOfDate { get; set; }

    /// <summary>銘柄コード（J-Quants `Code`。4桁/5桁。複合キーの一部）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>会社名（`CoName`）。</summary>
    public string? CompanyName { get; set; }

    /// <summary>17業種コード（`S17`）。</summary>
    public string? Sector17 { get; set; }

    /// <summary>33業種コード（`S33`）。</summary>
    public string? Sector33 { get; set; }

    /// <summary>規模区分（`ScaleCat`）。</summary>
    public string? ScaleCategory { get; set; }

    /// <summary>市場区分（`Mkt`）。</summary>
    public string? MarketCode { get; set; }

    /// <summary>貸借区分（`Mrgn`）。</summary>
    public string? MarginCode { get; set; }
}

/// <summary>
/// 日次株価四本値（J-Quants V2 /v2/equities/bars/daily）。
/// 生値と調整後の両方を保持。バックテスト/指標計算は調整後 (Adj*) を使う。
/// 数値は double（円・株数ともこの桁数では十分な精度。SQLite の decimal 順序問題も回避）。
/// </summary>
public class DailyBar
{
    /// <summary>取引日（`Date`。複合キーの一部）。</summary>
    public DateOnly Date { get; set; }

    /// <summary>銘柄コード（`Code`。複合キーの一部）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>始値（生値 `O`）。</summary>
    public double? Open { get; set; }
    /// <summary>高値（生値 `H`）。</summary>
    public double? High { get; set; }
    /// <summary>安値（生値 `L`）。</summary>
    public double? Low { get; set; }
    /// <summary>終値（生値 `C`）。</summary>
    public double? Close { get; set; }
    /// <summary>出来高（生値 `Vo`）。</summary>
    public double? Volume { get; set; }
    /// <summary>売買代金（`Va`）。</summary>
    public double? TurnoverValue { get; set; }

    /// <summary>累積調整係数（`AdjFactor`）。</summary>
    public double? AdjustmentFactor { get; set; }

    /// <summary>調整後始値（`AdjO`）。</summary>
    public double? AdjOpen { get; set; }
    /// <summary>調整後高値（`AdjH`）。</summary>
    public double? AdjHigh { get; set; }
    /// <summary>調整後安値（`AdjL`）。</summary>
    public double? AdjLow { get; set; }
    /// <summary>調整後終値（`AdjC`）。指標・バックテストの基準。</summary>
    public double? AdjClose { get; set; }
    /// <summary>調整後出来高（`AdjVo`）。</summary>
    public double? AdjVolume { get; set; }
}

/// <summary>
/// 財務サマリー（J-Quants V2 /v2/fins/summary）。
/// 複合キー (Code, DiscloseDate, DocType)。
/// DiscloseDate は「開示日（決算発表日）＝市場が情報を入手した日」。期末日ではない点に注意
/// （バックテストの先読み防止はこの開示日でガードする）。
/// </summary>
public class FinSummary
{
    /// <summary>開示日（`DiscDate`。複合キーの一部）。先読み防止の基準日。</summary>
    public DateOnly DiscloseDate { get; set; }

    /// <summary>銘柄コード（`Code`。複合キーの一部）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>書類種別（`DocType`。複合キーの一部。本決算/四半期等）。</summary>
    public string DocType { get; set; } = string.Empty;

    /// <summary>売上高（`Sales`）。※フィールド名は実装時に公式 fin-summary 仕様で最終確認。</summary>
    public double? Sales { get; set; }
    /// <summary>営業利益（`OP`）。</summary>
    public double? OperatingProfit { get; set; }
    /// <summary>純利益（`NP`）。</summary>
    public double? NetProfit { get; set; }
    /// <summary>1株当たり利益（`EPS`）。</summary>
    public double? Eps { get; set; }
    /// <summary>1株当たり純資産（`BPS`）。時価総額近似に使用。</summary>
    public double? Bps { get; set; }
    /// <summary>総資産（`TA`）。</summary>
    public double? TotalAssets { get; set; }
    /// <summary>純資産（`Eq`）。時価総額近似（発行済株数 ≈ Eq/BPS）に使用。</summary>
    public double? Equity { get; set; }
}

/// <summary>決算予定日（J-Quants V2 /v2/equities/earnings-calendar）。</summary>
public class EarningsCalendar
{
    /// <summary>発表予定日（`Date`。複合キーの一部）。</summary>
    public DateOnly Date { get; set; }
    /// <summary>銘柄コード（`Code`。複合キーの一部）。</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>会計年度（`FY`）。</summary>
    public string? FiscalYear { get; set; }
    /// <summary>会計四半期（`FQ`）。</summary>
    public string? FiscalQuarter { get; set; }
}

/// <summary>取引カレンダー（J-Quants V2 /v2/markets/calendar）。営業日判定に使用。</summary>
public class TradingCalendar
{
    /// <summary>日付（`Date`。主キー）。</summary>
    public DateOnly Date { get; set; }
    /// <summary>休日区分（`HolDiv`。0=非営業日,1=営業日 等。実装時に仕様確認）。</summary>
    public string? HolidayDivision { get; set; }
}
