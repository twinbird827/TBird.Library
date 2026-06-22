using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Data.Entities;
using TradeAnalyzer.Data.Options;

namespace TradeAnalyzer.Data.External.Edinet;

/// <summary>
/// EDINET API v2 クライアント。認証はクエリ Subscription-Key。
/// documents.json（書類一覧）と documents/{docID}?type=5（XBRL→CSV の ZIP）を扱う。
/// </summary>
public class EdinetClient
{
    private readonly HttpClient _http;
    private readonly EdinetOptions _options;
    private readonly ILogger<EdinetClient> _logger;

    public EdinetClient(HttpClient http, IOptions<EdinetOptions> options, ILogger<EdinetClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>指定日の提出書類一覧（type=2）を取得し、EdinetDocument へマップ。</summary>
    public async Task<List<EdinetDocument>> ListDocumentsAsync(DateOnly date, CancellationToken ct = default)
    {
        // 相対パス（先頭スラッシュ無し）＝ BaseAddress の /api/v2/ を保持させる。
        var url = $"documents.json?date={Iso(date)}&type=2&Subscription-Key={Key()}";
        var resp = await _http.GetFromJsonAsync<EdinetDocumentListResponse>(url, ct).ConfigureAwait(false);
        var results = resp?.Results ?? new();

        var docs = new List<EdinetDocument>(results.Count);
        foreach (var r in results)
        {
            if (r.DocId == null) continue;
            docs.Add(new EdinetDocument
            {
                DocId = r.DocId,
                EdinetCode = r.EdinetCode,
                SecCode = r.SecCode,
                NormalizedCode = NormalizeSecCode(r.SecCode),
                DocTypeCode = r.DocTypeCode,
                FormCode = r.FormCode,
                SubmitDate = date,
                PeriodStart = ParseDate(r.PeriodStart),
                PeriodEnd = ParseDate(r.PeriodEnd),
                CsvFlag = r.CsvFlag,
                XbrlFlag = r.XbrlFlag,
                Parsed = false,
            });
        }
        _logger.LogDebug("EDINET documents {Date}: {Count} 件", date, docs.Count);
        return docs;
    }

    /// <summary>書類の CSV(ZIP, type=5) をバイト列で取得。</summary>
    public async Task<byte[]> FetchCsvZipAsync(string docId, CancellationToken ct = default)
    {
        var url = $"documents/{Uri.EscapeDataString(docId)}?type=5&Subscription-Key={Key()}";
        return await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// EDINET secCode（通常5桁・末尾0）を J-Quants Code 照合用に正規化（5桁末尾0→4桁）。
    /// </summary>
    public static string? NormalizeSecCode(string? secCode)
    {
        if (string.IsNullOrWhiteSpace(secCode)) return null;
        var s = secCode!.Trim();
        if (s.Length == 5 && s.EndsWith("0", StringComparison.Ordinal)) return s.Substring(0, 4);
        return s;
    }

    private string Key()
    {
        if (string.IsNullOrWhiteSpace(_options.SubscriptionKey))
            throw new InvalidOperationException(
                "EDINET Subscription-Key 未設定。`dotnet user-secrets set \"Edinet:SubscriptionKey\" <key>` を実行してください。");
        return Uri.EscapeDataString(_options.SubscriptionKey!);
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
