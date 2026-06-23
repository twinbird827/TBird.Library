using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;
using TradeAnalyzer.Data.External.Edinet;
using TradeAnalyzer.Data.External.JQuants;

namespace TradeAnalyzer.Core.Ingest;

/// <summary>
/// 外部API取得 → 正規化保存のオーケストレーション。各 upsert は日付単位の delete-insert で冪等。
/// 注: J-Quants Free は 5req/分のため、広い期間の取得は数時間規模になりうる（土台用途として許容）。
/// </summary>
public class IngestService
{
    private readonly AppDbContext _db;
    private readonly JQuantsClient _jq;
    private readonly EdinetClient _edinet;
    private readonly EdinetCsvParser _edinetParser;
    private readonly ILogger<IngestService> _logger;

    // EDINET 書類種別: 120=有価証券報告書（確認済）。四半期/半期等は公式コードリスト確定後に追加。
    private static readonly HashSet<string> TargetDocTypeCodes = new(StringComparer.Ordinal) { "120" };

    public IngestService(
        AppDbContext db, JQuantsClient jq, EdinetClient edinet,
        EdinetCsvParser edinetParser, ILogger<IngestService> logger)
    {
        _db = db;
        _jq = jq;
        _edinet = edinet;
        _edinetParser = edinetParser;
        _logger = logger;
    }

    public async Task IngestAsync(
        DateOnly from, DateOnly to,
        bool skipJQuants = false, int? edinetLimitPerDay = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Ingest 開始 {From}..{To} (skipJQuants={Skip}, edinetLimit={Limit})",
            from, to, skipJQuants, edinetLimitPerDay);

        if (!skipJQuants)
        {
            await IngestCalendarAsync(from, to, ct).ConfigureAwait(false);
        }

        var tradingDays = await TradingDaysAsync(from, to, ct).ConfigureAwait(false);
        _logger.LogInformation("取引日 {Count} 日", tradingDays.Count);

        if (!skipJQuants)
        {
            await IngestEquityMasterAsync(from, ct).ConfigureAwait(false);
            await IngestEarningsCalendarAsync(ct).ConfigureAwait(false);
            await IngestDailyBarsAsync(tradingDays, ct).ConfigureAwait(false);
            await IngestFinSummaryAsync(tradingDays, ct).ConfigureAwait(false);
        }

        await IngestEdinetAsync(tradingDays, edinetLimitPerDay, ct).ConfigureAwait(false);

        _logger.LogInformation("Ingest 完了");
    }

    private async Task IngestCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var cal = await _jq.GetCalendarAsync(from, to, ct).ConfigureAwait(false);
        await _db.TradingCalendars.Where(c => c.Date >= from && c.Date <= to)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        _db.TradingCalendars.AddRange(cal);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("TradingCalendar {Count} 行", cal.Count);
    }

    private async Task IngestEquityMasterAsync(DateOnly asOf, CancellationToken ct)
    {
        // 段階1: 期間先頭の単一スナップショットで母集団を近似（点-in-time の厳密化は段階2）。
        var stocks = await _jq.GetEquityMasterAsync(date: asOf, ct: ct).ConfigureAwait(false);
        foreach (var s in stocks) s.AsOfDate = asOf; // 期間中ずっと利用可能にする
        var codes = stocks.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);

        await _db.Stocks.Where(s => s.AsOfDate == asOf).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        _db.Stocks.AddRange(stocks);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Stock master {Count} 行 (AsOf={AsOf})", stocks.Count, asOf);
    }

    private async Task IngestEarningsCalendarAsync(CancellationToken ct)
    {
        var items = await _jq.GetEarningsCalendarAsync(ct).ConfigureAwait(false);
        // 全件 upsert（キー重複を避けるため一旦全削除→追加）。
        await _db.EarningsCalendars.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        var deduped = items.DistinctBy(x => (x.Code, x.Date)).ToList();
        _db.EarningsCalendars.AddRange(deduped);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("EarningsCalendar {Count} 行", deduped.Count);
    }

    private async Task IngestDailyBarsAsync(IReadOnlyList<DateOnly> tradingDays, CancellationToken ct)
    {
        foreach (var d in tradingDays)
        {
            ct.ThrowIfCancellationRequested();
            var bars = await _jq.GetDailyBarsAsync(date: d, ct: ct).ConfigureAwait(false);
            if (bars.Count == 0) continue;
            await _db.DailyBars.Where(b => b.Date == d).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            var deduped = bars.DistinctBy(x => (x.Code, x.Date)).ToList();
            _db.DailyBars.AddRange(deduped);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogDebug("DailyBar {Date}: {Count} 行", d, deduped.Count);
        }
    }

    private async Task IngestFinSummaryAsync(IReadOnlyList<DateOnly> tradingDays, CancellationToken ct)
    {
        foreach (var d in tradingDays)
        {
            ct.ThrowIfCancellationRequested();
            var fins = await _jq.GetFinSummaryAsync(date: d, ct: ct).ConfigureAwait(false);
            if (fins.Count == 0) continue;
            await _db.FinSummaries.Where(f => f.DiscloseDate == d).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            var deduped = fins.DistinctBy(x => (x.Code, x.DiscloseDate, x.DocType)).ToList();
            _db.FinSummaries.AddRange(deduped);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogDebug("FinSummary {Date}: {Count} 行", d, deduped.Count);
        }
    }

    private async Task IngestEdinetAsync(IReadOnlyList<DateOnly> days, int? limitPerDay, CancellationToken ct)
    {
        var knownCodes = await _db.Stocks.Select(s => s.Code).Distinct()
            .ToListAsync(ct).ConfigureAwait(false);
        var codeSet = knownCodes.ToHashSet(StringComparer.Ordinal);

        foreach (var d in days)
        {
            ct.ThrowIfCancellationRequested();
            List<EdinetDocument> docs;
            try
            {
                docs = await _edinet.ListDocumentsAsync(d, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EDINET 一覧取得失敗 {Date}（スキップ）", d);
                continue;
            }

            var targets = docs
                .Where(x => x.DocTypeCode != null && TargetDocTypeCodes.Contains(x.DocTypeCode))
                .Where(x => x.CsvFlag == "1")
                .Where(x => x.NormalizedCode != null && MatchesKnown(x.NormalizedCode, codeSet))
                .ToList();

            if (limitPerDay is int lim && targets.Count > lim)
            {
                _logger.LogInformation("EDINET {Date}: 対象 {Total} 件のうち {Lim} 件に制限", d, targets.Count, lim);
                targets = targets.Take(lim).ToList();
            }

            // 書類メタを保存（delete-insert by submit date）。
            await _db.EdinetDocuments.Where(x => x.SubmitDate == d).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            _db.EdinetDocuments.AddRange(docs);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            foreach (var doc in targets)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var zip = await _edinet.FetchCsvZipAsync(doc.DocId, ct).ConfigureAwait(false);
                    var facts = _edinetParser.Parse(zip, doc.DocId, doc.NormalizedCode, doc.PeriodEnd);
                    await _db.EdinetFinFacts.Where(f => f.DocId == doc.DocId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
                    _db.EdinetFinFacts.AddRange(facts);
                    var tracked = await _db.EdinetDocuments.FindAsync(new object[] { doc.DocId }, ct).ConfigureAwait(false);
                    if (tracked != null) tracked.Parsed = true;
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    _logger.LogDebug("EDINET {DocId}: {Count} facts", doc.DocId, facts.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "EDINET 書類解析失敗 {DocId}（スキップ）", doc.DocId);
                }
            }

            if (targets.Count > 0)
                _logger.LogInformation("EDINET {Date}: 対象 {Count} 件解析", d, targets.Count);
        }
    }

    /// <summary>EDINET 正規化コード（4桁）が J-Quants Code 集合（4桁/5桁）に一致するか。</summary>
    private static bool MatchesKnown(string normalized, HashSet<string> codeSet)
        => codeSet.Contains(normalized) || codeSet.Contains(normalized + "0");

    private async Task<List<DateOnly>> TradingDaysAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var cal = await _db.TradingCalendars
            .Where(c => c.Date >= from && c.Date <= to && c.HolidayDivision != "0")
            .OrderBy(c => c.Date)
            .Select(c => c.Date)
            .ToListAsync(ct).ConfigureAwait(false);
        if (cal.Count > 0) return cal;

        // フォールバック: カレンダー未取得時は土日を除く平日。
        var days = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                days.Add(d);
        return days;
    }
}
