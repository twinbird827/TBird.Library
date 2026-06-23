using System.Text.Json.Serialization;

namespace TradeAnalyzer.Data.External.JQuants;

/// <summary>
/// J-Quants V2 共通レスポンス封筒 {"data":[...],"pagination_key":"..."}。
/// data のキー名はエンドポイントごとに異なるため、各メソッドで個別 DTO を使う。
/// </summary>
public class JQuantsPage<T>
{
    [JsonPropertyName("pagination_key")]
    public string? PaginationKey { get; set; }
}

// --- 上場銘柄一覧 /v2/equities/master ---
public class EquityMasterPage : JQuantsPage<EquityMasterItem>
{
    [JsonPropertyName("data")]
    public List<EquityMasterItem> Data { get; set; } = new();
}

public class EquityMasterItem
{
    [JsonPropertyName("Date")] public string? Date { get; set; }
    [JsonPropertyName("Code")] public string? Code { get; set; }
    [JsonPropertyName("CoName")] public string? CoName { get; set; }
    [JsonPropertyName("S17")] public string? S17 { get; set; }
    [JsonPropertyName("S33")] public string? S33 { get; set; }
    [JsonPropertyName("ScaleCat")] public string? ScaleCat { get; set; }
    [JsonPropertyName("Mkt")] public string? Mkt { get; set; }
    [JsonPropertyName("Mrgn")] public string? Mrgn { get; set; }
}

// --- 株価四本値 /v2/equities/bars/daily ---
public class DailyBarsPage : JQuantsPage<DailyBarItem>
{
    [JsonPropertyName("data")]
    public List<DailyBarItem> Data { get; set; } = new();
}

public class DailyBarItem
{
    [JsonPropertyName("Date")] public string? Date { get; set; }
    [JsonPropertyName("Code")] public string? Code { get; set; }

    [JsonPropertyName("O")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? O { get; set; }
    [JsonPropertyName("H")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? H { get; set; }
    [JsonPropertyName("L")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? L { get; set; }
    [JsonPropertyName("C")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? C { get; set; }
    [JsonPropertyName("Vo")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? Vo { get; set; }
    [JsonPropertyName("Va")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? Va { get; set; }

    [JsonPropertyName("AdjFactor")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? AdjFactor { get; set; }
    [JsonPropertyName("AdjO")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? AdjO { get; set; }
    [JsonPropertyName("AdjH")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? AdjH { get; set; }
    [JsonPropertyName("AdjL")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? AdjL { get; set; }
    [JsonPropertyName("AdjC")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? AdjC { get; set; }
    [JsonPropertyName("AdjVo")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? AdjVo { get; set; }
}

// --- 財務サマリー /v2/fins/summary ---
// 注: フィールド名は公式 fin-summary 仕様で実装時に最終確認すること（推測実装しない）。
public class FinSummaryPage : JQuantsPage<FinSummaryItem>
{
    [JsonPropertyName("data")]
    public List<FinSummaryItem> Data { get; set; } = new();
}

public class FinSummaryItem
{
    [JsonPropertyName("DiscDate")] public string? DiscDate { get; set; }
    [JsonPropertyName("Code")] public string? Code { get; set; }
    [JsonPropertyName("DocType")] public string? DocType { get; set; }

    [JsonPropertyName("Sales")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? Sales { get; set; }
    [JsonPropertyName("OP")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? OP { get; set; }
    [JsonPropertyName("NP")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? NP { get; set; }
    [JsonPropertyName("EPS")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? EPS { get; set; }
    [JsonPropertyName("BPS")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? BPS { get; set; }
    [JsonPropertyName("TA")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? TA { get; set; }
    [JsonPropertyName("Eq")][JsonConverter(typeof(FlexibleDoubleConverter))] public double? Eq { get; set; }
}

// --- 決算予定日 /v2/equities/earnings-calendar ---
public class EarningsCalendarPage : JQuantsPage<EarningsCalendarItem>
{
    [JsonPropertyName("data")]
    public List<EarningsCalendarItem> Data { get; set; } = new();
}

public class EarningsCalendarItem
{
    [JsonPropertyName("Date")] public string? Date { get; set; }
    [JsonPropertyName("Code")] public string? Code { get; set; }
    [JsonPropertyName("FY")] public string? FY { get; set; }
    [JsonPropertyName("FQ")] public string? FQ { get; set; }
}

// --- 取引カレンダー /v2/markets/calendar ---
public class CalendarPage : JQuantsPage<CalendarItem>
{
    [JsonPropertyName("data")]
    public List<CalendarItem> Data { get; set; } = new();
}

public class CalendarItem
{
    [JsonPropertyName("Date")] public string? Date { get; set; }
    [JsonPropertyName("HolDiv")] public string? HolDiv { get; set; }
}
