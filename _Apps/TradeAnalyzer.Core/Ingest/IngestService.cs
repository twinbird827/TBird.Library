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

            // EDINET は同一 docID を同日一覧内でも再掲しうるため重複排除（targets 抽出・スキップ数算出の基準）。
            var distinct = docs.DistinctBy(x => x.DocId).ToList();

            var targets = distinct
                .Where(x => x.DocTypeCode != null && TargetDocTypeCodes.Contains(x.DocTypeCode))
                .Where(x => x.CsvFlag == "1")
                .Where(x => x.NormalizedCode != null && MatchesKnown(x.NormalizedCode, codeSet))
                .ToList();

            if (limitPerDay is int lim && targets.Count > lim)
            {
                _logger.LogInformation("EDINET {Date}: 対象 {Total} 件のうち {Lim} 件に制限", d, targets.Count, lim);
                targets = targets.Take(lim).ToList();
            }

            // 書類メタを保存（衝突回避コアは SaveEdinetMetaAsync に抽出）。生の docs を渡す（メソッド内で dedup）。
            var toInsert = await SaveEdinetMetaAsync(_db, docs, d, ct).ConfigureAwait(false);
            var skippedExisting = distinct.Count - toInsert.Count;
            _logger.LogInformation("EDINET {Date}: 一覧 {Listed} 件（実 {Distinct} 件）・新規 {New} 件・他日既存スキップ {Skipped} 件",
                d, docs.Count, distinct.Count, toInsert.Count, skippedExisting);

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

    /// <summary>
    /// EDINET 書類メタの衝突回避保存コア。EDINET は同一 docID を複数のファイル日付一覧に返す（提出処理日＋
    /// 書類情報修正日＋開示不開示区分変更日。公式仕様 ESE140206.pdf の date 注記）。PK は
    /// <see cref="EdinetDocument.DocId"/> 単独のため、<paramref name="d"/> 単位の delete-insert だけでは
    /// 「他日に既存の同一 DocId」と UNIQUE 衝突する。対策として同日分は置換（冪等維持）しつつ、他日
    /// <see cref="EdinetDocument.SubmitDate"/> に既存の DocId はスキップして衝突を避ける（既存メタ行を書き換えない）。
    /// <c>SubmitDate != d</c> でフィルタするため delete と照会の実行順に依存しない。引数 <paramref name="docs"/> は
    /// 同一 DocID の再掲を含みうる生の一覧でよい（メソッド内で DocId 重複排除するため呼び出し側の dedup 規律に
    /// 依存しない＝misuse-proof）。SelfTest（別アセンブリ TradeAnalyzer.Worker）から in-memory DB で検証するため public。
    /// </summary>
    /// <returns>実際に挿入した（他日既存でスキップされなかった）メタ行。</returns>
    public static async Task<List<EdinetDocument>> SaveEdinetMetaAsync(
        AppDbContext db, IReadOnlyList<EdinetDocument> docs, DateOnly d, CancellationToken ct)
    {
        // 防御的 dedup: 生 docs 混入（同日重複 DocId）でも AddRange の identity-resolution 衝突
        // （InvalidOperationException）を招かない（PR #181 が根絶した構造的クラッシュ免疫を維持）。
        var deduped = docs.DistinctBy(x => x.DocId).ToList();
        await db.EdinetDocuments.Where(x => x.SubmitDate == d).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        var ids = deduped.Select(x => x.DocId).ToList();
        var otherDayIds = (await db.EdinetDocuments
                .Where(x => ids.Contains(x.DocId) && x.SubmitDate != d)
                .Select(x => x.DocId).ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        var toInsert = deduped.Where(x => !otherDayIds.Contains(x.DocId)).ToList();
        db.EdinetDocuments.AddRange(toInsert);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return toInsert;
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
