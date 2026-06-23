using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Data.External.JQuants;

/// <summary>
/// J-Quants V2 クライアント（Free）。認証は x-api-key（既定ヘッダ。DI 側で付与）。
/// レート制御は <see cref="JQuantsRateLimitHandler"/>、429/5xx 再試行は resilience handler が担う。
/// 本クラスは pagination_key を辿って全ページを連結し、エンティティへマップする。
/// </summary>
public class JQuantsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<JQuantsClient> _logger;

    public JQuantsClient(HttpClient http, ILogger<JQuantsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>上場銘柄一覧。date 指定でその日のポイントインタイム master、code 指定で銘柄の履歴。</summary>
    public async Task<List<Stock>> GetEquityMasterAsync(
        DateOnly? date = null, string? code = null, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>();
        if (date.HasValue) query["date"] = Iso(date.Value);
        if (code != null) query["code"] = code;

        var items = await GetAllPagesAsync<EquityMasterPage, EquityMasterItem>(
            "/v2/equities/master", query, p => p.Data, p => p.PaginationKey, ct).ConfigureAwait(false);

        var result = new List<Stock>(items.Count);
        foreach (var i in items)
        {
            if (i.Code == null) continue;
            var asOf = ParseDate(i.Date) ?? (date ?? default);
            result.Add(new Stock
            {
                AsOfDate = asOf,
                Code = i.Code,
                CompanyName = i.CoName,
                Sector17 = i.S17,
                Sector33 = i.S33,
                ScaleCategory = i.ScaleCat,
                MarketCode = i.Mkt,
                MarginCode = i.Mrgn,
            });
        }
        return result;
    }

    /// <summary>株価四本値。code か date のいずれか必須。</summary>
    public async Task<List<DailyBar>> GetDailyBarsAsync(
        string? code = null, DateOnly? date = null,
        DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        if (code == null && date == null)
            throw new ArgumentException("GetDailyBarsAsync requires either code or date.");

        var query = new Dictionary<string, string?>();
        if (code != null) query["code"] = code;
        if (date.HasValue) query["date"] = Iso(date.Value);
        if (from.HasValue) query["from"] = Iso(from.Value);
        if (to.HasValue) query["to"] = Iso(to.Value);

        var items = await GetAllPagesAsync<DailyBarsPage, DailyBarItem>(
            "/v2/equities/bars/daily", query, p => p.Data, p => p.PaginationKey, ct).ConfigureAwait(false);

        var result = new List<DailyBar>(items.Count);
        foreach (var i in items)
        {
            var d = ParseDate(i.Date);
            if (i.Code == null || d == null) continue;
            result.Add(new DailyBar
            {
                Date = d.Value,
                Code = i.Code,
                Open = i.O, High = i.H, Low = i.L, Close = i.C, Volume = i.Vo, TurnoverValue = i.Va,
                AdjustmentFactor = i.AdjFactor,
                AdjOpen = i.AdjO, AdjHigh = i.AdjH, AdjLow = i.AdjL, AdjClose = i.AdjC, AdjVolume = i.AdjVo,
            });
        }
        return result;
    }

    /// <summary>財務サマリー。code か date のいずれか必須。</summary>
    public async Task<List<FinSummary>> GetFinSummaryAsync(
        string? code = null, DateOnly? date = null, CancellationToken ct = default)
    {
        if (code == null && date == null)
            throw new ArgumentException("GetFinSummaryAsync requires either code or date.");

        var query = new Dictionary<string, string?>();
        if (code != null) query["code"] = code;
        if (date.HasValue) query["date"] = Iso(date.Value);

        var items = await GetAllPagesAsync<FinSummaryPage, FinSummaryItem>(
            "/v2/fins/summary", query, p => p.Data, p => p.PaginationKey, ct).ConfigureAwait(false);

        var result = new List<FinSummary>(items.Count);
        foreach (var i in items)
        {
            var d = ParseDate(i.DiscDate);
            if (i.Code == null || d == null) continue;
            result.Add(new FinSummary
            {
                DiscloseDate = d.Value,
                Code = i.Code,
                DocType = i.DocType ?? string.Empty,
                Sales = i.Sales, OperatingProfit = i.OP, NetProfit = i.NP,
                Eps = i.EPS, Bps = i.BPS, TotalAssets = i.TA, Equity = i.Eq,
            });
        }
        return result;
    }

    /// <summary>決算予定日。</summary>
    public async Task<List<EarningsCalendar>> GetEarningsCalendarAsync(CancellationToken ct = default)
    {
        var items = await GetAllPagesAsync<EarningsCalendarPage, EarningsCalendarItem>(
            "/v2/equities/earnings-calendar", new(), p => p.Data, p => p.PaginationKey, ct).ConfigureAwait(false);

        var result = new List<EarningsCalendar>(items.Count);
        foreach (var i in items)
        {
            var d = ParseDate(i.Date);
            if (i.Code == null || d == null) continue;
            result.Add(new EarningsCalendar
            {
                Date = d.Value, Code = i.Code, FiscalYear = i.FY, FiscalQuarter = i.FQ,
            });
        }
        return result;
    }

    /// <summary>取引カレンダー。</summary>
    public async Task<List<TradingCalendar>> GetCalendarAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?> { ["from"] = Iso(from), ["to"] = Iso(to) };
        var items = await GetAllPagesAsync<CalendarPage, CalendarItem>(
            "/v2/markets/calendar", query, p => p.Data, p => p.PaginationKey, ct).ConfigureAwait(false);

        var result = new List<TradingCalendar>(items.Count);
        foreach (var i in items)
        {
            var d = ParseDate(i.Date);
            if (d == null) continue;
            result.Add(new TradingCalendar { Date = d.Value, HolidayDivision = i.HolDiv });
        }
        return result;
    }

    /// <summary>pagination_key を辿って全ページの data[] を連結する。</summary>
    private async Task<List<TItem>> GetAllPagesAsync<TPage, TItem>(
        string path, Dictionary<string, string?> query,
        Func<TPage, List<TItem>> getData, Func<TPage, string?> getKey, CancellationToken ct)
        where TPage : class
    {
        // pagination_key が前進しない（同一キーを返し続ける）場合の無限ループ・メモリ膨張・
        // レート上限消費を防ぐ。直前キーと同一なら打ち切り、安全弁として最大ページ数も設ける。
        const int MaxPages = 10_000;
        var all = new List<TItem>();
        string? key = null;
        string? prevKey = null;
        int pages = 0;
        do
        {
            var url = BuildUrl(path, query, key);
            var page = await _http.GetFromJsonAsync<TPage>(url, ct).ConfigureAwait(false);
            if (page == null) break;
            all.AddRange(getData(page));
            prevKey = key;
            key = getKey(page);
            if (!string.IsNullOrEmpty(key) && key == prevKey)
            {
                _logger.LogWarning("J-Quants {Path}: pagination_key が前進しないため打ち切り（{Count} 件取得済み）", path, all.Count);
                break;
            }
            if (++pages >= MaxPages)
            {
                _logger.LogWarning("J-Quants {Path}: 最大ページ数 {Max} に到達し打ち切り（{Count} 件）", path, MaxPages, all.Count);
                break;
            }
        } while (!string.IsNullOrEmpty(key));

        _logger.LogDebug("J-Quants {Path}: {Count} 件取得", path, all.Count);
        return all;
    }

    private static string BuildUrl(string path, Dictionary<string, string?> query, string? paginationKey)
    {
        var parts = new List<string>();
        foreach (var kv in query)
        {
            if (kv.Value == null) continue;
            parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
        }
        if (!string.IsNullOrEmpty(paginationKey))
            parts.Add($"pagination_key={Uri.EscapeDataString(paginationKey!)}");
        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateOnly.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
        return null;
    }
}
