using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TradeAnalyzer.Core.Rules;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Worker.Claude;

/// <summary>
/// 当日 t の対象銘柄について DB 実数を収集し、派生指標を C# で決定論的に計算して <see cref="ClaudeFacts"/> を組む。
/// 「Claude が新しい数値を作る」余地を構造的に排除する（数値は全て C#/DB 由来）。先読み防止は RuleEngine と同じ
/// 開示日ガード（<c>DiscloseDate &lt;= t</c>）を踏襲する。読取は <c>AsNoTracking</c>。
/// <para>
/// <b>PER/PBR の分割方針（実装確定）</b>: この DB の AdjClose は ingest 時点で既知の分割しか反映せず遡及調整が
/// 不整合（live 日次 bar は後発分割が過去へ伝播しない）。よって F_t/F_disc 係数補正は正しく行えないため採らない。
/// 代わりに区間 (DiscloseDate, t] に分割（<c>AdjustmentFactor != 1</c>）が無いときのみ <c>AdjClose/EPS(BPS)</c> を
/// 「近似」として算出し、分割があれば算出を見送る（歪んだ値を「事実」として出す害を回避）。時価総額は
/// <see cref="RuleEngine.ApproxMarketCap"/> を再利用し「ルール判定に使った近似値」として注入する。
/// </para>
/// </summary>
internal static class ClaudeFactGatherer
{
    public static async Task<ClaudeFacts> GatherAsync(
        AppDbContext db, DateOnly t, Signal signal, CancellationToken ct = default)
    {
        string code = signal.Code;

        var stock = await db.Stocks.AsNoTracking()
            .Where(s => s.Code == code && s.AsOfDate <= t)
            .OrderByDescending(s => s.AsOfDate)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var bar = await db.DailyBars.AsNoTracking()
            .Where(b => b.Code == code && b.Date <= t)
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var fin = await db.FinSummaries.AsNoTracking()
            .Where(f => f.Code == code && f.DiscloseDate <= t)
            .OrderByDescending(f => f.DiscloseDate)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        // 生 Close へフォールバックしない: RuleEngine（EvaluateStock）は AdjClose 非 null の bar だけで判定するため、
        // 生値を「調整後終値」「ルール判定に使用」ラベルで注入すると実判定と食い違う事実になる。null は正直に
        // 「データなし」/算出不可とし、生値は既存の「終値（生値）」行が示す。なお J-Quants 仕様では取引なし日は
        // 四本値（生・調整とも）同時に Null＝「AdjClose のみ null」の帯は仕様上存在せず、通常運用は挙動不変。
        double? adjClose = bar?.AdjClose;

        // 開示日〜t の間に分割（AdjustmentFactor≠1）があったか。あれば EPS/BPS の株数基準が AdjClose と食い違うため
        // PER/PBR の算出を見送る（この DB では係数補正が不整合で信頼できない）。
        bool splitBetween = false;
        if (fin != null)
        {
            var disc = fin.DiscloseDate;
            splitBetween = await db.DailyBars.AsNoTracking()
                .Where(b => b.Code == code && b.Date > disc && b.Date <= t
                    && b.AdjustmentFactor != null && b.AdjustmentFactor != 1.0)
                .AnyAsync(ct).ConfigureAwait(false);
        }

        var lines = new List<FactLine>
        {
            new("17業種コード", stock?.Sector17),
            new("33業種コード", stock?.Sector33),
            new("市場区分", stock?.MarketCode),
            new("規模区分", stock?.ScaleCategory),
            // 日付は Label でなく Value に置く: Label はコンパイル時定数（許可集合・Flatten の対象外）という
            // 不変条件を保ち、Claude が株価日付を引用しても許可集合（Value 側）で正当化されるようにする。
            new("最新株価（調整後終値）", FmtNum(adjClose, "円", DecimalFmt)),
            new("株価日付", FmtDate(bar?.Date)),
            new("終値（生値）", FmtNum(bar?.Close, "円", DecimalFmt)),
            new("出来高", FmtNum(bar?.Volume, "株")),
            new("売買代金", FmtNum(bar?.TurnoverValue, "円")),
            new("財務開示日", FmtDate(fin?.DiscloseDate)),
            new("書類種別", fin?.DocType),
            new("売上高", FmtNum(fin?.Sales, "円")),
            new("営業利益", FmtNum(fin?.OperatingProfit, "円")),
            new("純利益", FmtNum(fin?.NetProfit, "円")),
            new("EPS（1株利益）", FmtNum(fin?.Eps, "円", DecimalFmt)),
            new("BPS（1株純資産）", FmtNum(fin?.Bps, "円", DecimalFmt)),
            new("純資産", FmtNum(fin?.Equity, "円")),
            new("総資産", FmtNum(fin?.TotalAssets, "円")),
            new("PER（調整後終値÷EPS・C#計算・近似）", Ratio(adjClose, fin?.Eps, splitBetween)),
            new("PBR（調整後終値÷BPS・C#計算・近似）", Ratio(adjClose, fin?.Bps, splitBetween)),
            new("時価総額近似（ルール判定に使用・C#計算）", FmtNum(RuleEngine.ApproxMarketCap(fin, adjClose), "円")),
            new("ルールスコア（通過ゲート数）", signal.RuleScore.ToString("0.##", CultureInfo.InvariantCulture)),
            new("MLスコア（LambdaRank順位付け）", signal.MlScore?.ToString("F4", CultureInfo.InvariantCulture)),
            new("ルール通過理由", signal.Rationale),
        };

        return new ClaudeFacts(code, stock?.CompanyName, lines);
    }

    /// <summary>PER/PBR の近似値文字列。EPS/BPS が null/非正なら算出不可、区間に分割があれば見送り。</summary>
    private static string Ratio(double? price, double? denom, bool splitBetween)
    {
        if (splitBetween) return "開示後に分割あり・算出見送り（株数基準が不一致）";
        if (price is not double p || denom is not double d) return "算出不可（データ欠損）";
        if (d <= 0) return "算出不可（分母が非正）";
        return (p / d).ToString("F1", CultureInfo.InvariantCulture) + " 倍";
    }

    /// <summary>小数を持ち得る系列（EPS/BPS/株価）用の書式（カンマ維持・小数最大2桁）。"N0" の整数丸めで注入すると
    /// 「表示株価÷表示EPS ≠ 表示PER」の不整合が注入事実内に生じ、許可集合も実値とずれるため小数を保存する。</summary>
    private const string DecimalFmt = "#,0.##";

    private static string? FmtNum(double? v, string unit, string format = "N0") =>
        v is double d ? d.ToString(format, CultureInfo.InvariantCulture) + " " + unit : null;

    // InvariantCulture 必須: "yyyy" は現在カルチャの暦の年を出すため、非グレゴリオ暦カルチャ（th-TH=仏暦等）の
    // ホストでは「2569-07-16」等の誤った年を「実データ」として注入してしまう（FmtNum/RuleScore と同じ方針）。
    private static string? FmtDate(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
