using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeAnalyzer.Core.Backtest;
using TradeAnalyzer.Data;

namespace TradeAnalyzer.Worker.Notify;

/// <summary>
/// チャネル非依存の配信ペイロード（段階3c）。STEP2 の Discord 整形・STEP3 の REST 応答が共用する唯一の形。
/// provenance（usedFacts/model/route/generatedAt）は現行要件に表示が無いため意図的に運ばない
/// （必要になれば record へプロパティ追加で足りる＝内部 DTO のため後方互換問題なし）。
/// </summary>
public sealed record DeliveryReport(DateOnly Date, int TotalPassed, IReadOnlyList<DeliveryItem> Items);

/// <summary>Top-K の 1 銘柄分。Qualitative=null は定性なし（ML のみ）＝3b フォールバック契約の下流継承。</summary>
public sealed record DeliveryItem(
    string Code, string? CompanyName, int Rank,
    double? MlScore, double RuleScore, string Rationale,
    DeliveryQualitative? Qualitative);

/// <summary><see cref="TradeAnalyzer.Data.Entities.Signal.QualitativeJson"/> から抜き出す定性層の表示部分。</summary>
public sealed record DeliveryQualitative(
    string Summary, IReadOnlyList<string> Risks, bool NumericUnverified);

/// <summary>
/// <see cref="DeliveryReport"/> を DB から組み立てる読取専用ビルダ（送信と分離。STEP3 の REST も同じメソッドを呼ぶ）。
/// 前提破綻の検査をここに 1 か所集約し、呼び手が ExitCode / HTTP 応答へ写像する。
/// </summary>
internal static class DeliveryReportBuilder
{
    // QualitativeJson は 3b が匿名オブジェクトで保存した camelCase JSON（再利用可能な保存 DTO クラスは無い）。
    // System.Text.Json 既定は case-sensitive のため、CaseInsensitive を明示しないと "summary"→Summary が不一致で
    // Summary/Risks=null に「無言で」バインドされ、try-parse では検知できない（要約が空欄のまま通知される）。
    private static readonly JsonSerializerOptions QualitativeJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 当日 t の Top-K＋根拠文を読み取り配信ペイロードを組む。前提破綻（Signal 行ゼロ／Passed 行に MlScore=null
    /// 混在）は <see cref="InvalidOperationException"/> で fail-loud——notify-today 経路はコマンド内で捕捉せず
    /// Program.cs の外側 catch が ExitCode=1 に写像し、STEP3 REST は catch して HTTP 応答へ変換する契約。
    /// Passed=0 は正常（0 件ペイロード）＝「無通知」と「故障」を区別可能にする。
    /// null 検査は <see cref="BacktestService.SelectTopPicks"/>(useMl:true) の .Value 前に置く（呼出側検査契約）。
    /// </summary>
    public static async Task<DeliveryReport> BuildDeliveryReportAsync(
        AppDbContext db, DateOnly t, int topN, ILogger logger, CancellationToken ct = default)
    {
        // 存在判定（AnyAsync）とデータ取得を分け、小さい Passed 集合のみ材料化する（explain-today の読取と同型）。
        if (!await db.Signals.AsNoTracking().AnyAsync(s => s.Date == t, ct))
            throw new InvalidOperationException(
                $"{t}: Signal 行がありません（run-today 未実行/未完了）。run-today を成功させてから再実行してください。");

        var passed = await db.Signals.AsNoTracking()
            .Where(s => s.Date == t && s.Passed)
            .ToListAsync(ct);
        if (passed.Any(r => r.MlScore is null))
            throw new InvalidOperationException(
                $"{t}: MlScore 未設定の Passed 行があります（run-today の ML 採点が未完了＝パイプライン障害）。" +
                "run-today を成功させてから再実行してください。");

        var top = BacktestService.SelectTopPicks(passed, topN, useMl: true);

        // 会社名: AsOfDate <= t の最新スナップショット（ClaudeFactGatherer と同じ選定規則）。Top-K の code 集合に
        // 絞り GroupBy 群毎 top-1 の 1 クエリで引く（EF Core 10 で単一 SQL に翻訳される）。
        var codes = top.Select(s => s.Code).ToList();
        var names = await db.Stocks.AsNoTracking()
            .Where(s => codes.Contains(s.Code) && s.AsOfDate <= t)
            .GroupBy(s => s.Code)
            .Select(g => new
            {
                Code = g.Key,
                Name = g.OrderByDescending(x => x.AsOfDate).Select(x => x.CompanyName).First(),
            })
            .ToDictionaryAsync(x => x.Code, x => x.Name, ct);

        var items = new List<DeliveryItem>(top.Count);
        for (int i = 0; i < top.Count; i++)
        {
            var s = top[i];
            items.Add(new DeliveryItem(
                s.Code, names.GetValueOrDefault(s.Code), Rank: i + 1,
                s.MlScore, s.RuleScore, s.Rationale ?? "",
                ParseQualitative(s.QualitativeJson, s.Code, logger)));
        }
        return new DeliveryReport(t, passed.Count, items);
    }

    /// <summary>
    /// QualitativeJson を表示部分へ変換する。null（未生成/失敗）は正常の「定性なし」。構文不正は JsonException を
    /// 捕捉して警告＋null（1 行の壊れた JSON が通知全体を殺さない）。構文妥当でも summary/risks キー欠落は
    /// JsonException を投げず非 null 参照型へ null を無言バインドする（System.Text.Json は nullable アノテーション
    /// 非検証）ため、デシリアライズ後の null 検査で corrupt 扱い＝警告＋null に落とす（?? "" の部分救済は
    /// 「解析済みだが中身なし」に見えてデータ破損を隠蔽するため不採用）。SelfTest から回帰検証するため internal。
    /// </summary>
    internal static DeliveryQualitative? ParseQualitative(string? json, string code, ILogger logger)
    {
        if (json is null) return null;
        try
        {
            var q = JsonSerializer.Deserialize<DeliveryQualitative>(json, QualitativeJsonOptions);
            if (q?.Summary is null || q.Risks is null)
            {
                logger.LogWarning("[{Code}] QualitativeJson に summary/risks がありません（破損扱い＝定性なしで配信）。", code);
                return null;
            }
            return q;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[{Code}] QualitativeJson の解析に失敗しました（破損扱い＝定性なしで配信）。", code);
            return null;
        }
    }
}
