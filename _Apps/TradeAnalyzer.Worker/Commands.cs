using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Core.Backtest;
using TradeAnalyzer.Core.Ingest;
using TradeAnalyzer.Core.Rules;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Options;

namespace TradeAnalyzer.Worker;

/// <summary>ワンショット CLI コマンド。日付は YYYY-MM-DD。期間は YYYY か YYYY-MM-DD:YYYY-MM-DD。</summary>
public static class Commands
{
    public static void PrintUsage()
    {
        Console.WriteLine(
@"TradeAnalyzer Worker — 段階1 CLI

使い方:
  migrate                                    DB を最新マイグレーションへ更新
  ingest  --from YYYY-MM-DD --to YYYY-MM-DD  J-Quants/EDINET 取得→保存（要 APIキー）
          [--skip-jquants]                   J-Quants をスキップし EDINET のみ取得（既存 Stocks で突合）
          [--edinet-limit N]                 EDINET の日あたり解析件数を N に制限
  analyze --date YYYY-MM-DD                  指定日でルール評価し Signal 保存
  backtest --is <期間> --oos <期間>          バックテスト実行（期間=YYYY または YYYY-MM-DD:YYYY-MM-DD）
  selftest                                   APIキー不要の単体検証（指標/ルール/先読み防止）

例:
  dotnet run -- migrate
  dotnet run -- ingest --from 2024-01-01 --to 2025-12-31
  dotnet run -- analyze --date 2025-06-30
  dotnet run -- backtest --is 2024 --oos 2025");
    }

    public static async Task MigrateAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILogger<AppDbContext>>();
        await db.Database.MigrateAsync();
        logger.LogInformation("マイグレーション完了: {Path}", db.Database.GetConnectionString());
    }

    public static async Task IngestAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        var from = RequireDate(opts, "from");
        var to = RequireDate(opts, "to");
        if (from > to) throw new ArgumentException("--from は --to 以前である必要があります。");

        bool skipJQuants = opts.ContainsKey("skip-jquants");
        int? edinetLimit = opts.TryGetValue("edinet-limit", out var lv) && int.TryParse(lv, out var l) ? l : null;

        ValidateIngestConfig(sp, skipJQuants);

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(); // 取得前に DB を用意

        var ingest = sp.GetRequiredService<IngestService>();
        await ingest.IngestAsync(from, to, skipJQuants, edinetLimit);
    }

    public static async Task AnalyzeAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        var date = RequireDate(opts, "date");

        var db = sp.GetRequiredService<AppDbContext>();
        var rules = sp.GetRequiredService<RuleEngine>();
        var logger = sp.GetRequiredService<ILogger<RuleEngine>>();

        var signals = await rules.EvaluateAsync(date);
        await db.Signals.Where(s => s.Date == date).ExecuteDeleteAsync();
        db.Signals.AddRange(signals);
        await db.SaveChangesAsync();

        logger.LogInformation("analyze {Date}: {Total} 件保存 ({Passed} 通過)",
            date, signals.Count, signals.Count(s => s.Passed));
    }

    public static async Task BacktestAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        var (isStart, isEnd) = RequireRange(opts, "is");
        var (oosStart, oosEnd) = RequireRange(opts, "oos");

        var backtest = sp.GetRequiredService<BacktestService>();
        var run = await backtest.RunAsync(isStart, isEnd, oosStart, oosEnd, label: $"IS{isStart:yyyy}-OOS{oosStart:yyyy}");

        Console.WriteLine($"Backtest 完了: trades={run.TradeCount}, winRate={run.WinRate:P1}, avgReturn={run.AvgReturn:P2}");
    }

    public static async Task StatsAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        var db = sp.GetRequiredService<AppDbContext>();

        Console.WriteLine("=== テーブル行数 ===");
        Console.WriteLine($"Stocks            : {await db.Stocks.CountAsync()}");
        Console.WriteLine($"DailyBars         : {await db.DailyBars.CountAsync()}");
        Console.WriteLine($"FinSummaries      : {await db.FinSummaries.CountAsync()}");
        Console.WriteLine($"EarningsCalendars : {await db.EarningsCalendars.CountAsync()}");
        Console.WriteLine($"TradingCalendars  : {await db.TradingCalendars.CountAsync()}");
        Console.WriteLine($"EdinetDocuments   : {await db.EdinetDocuments.CountAsync()}");
        Console.WriteLine($"EdinetFinFacts    : {await db.EdinetFinFacts.CountAsync()}");
        Console.WriteLine($"Signals           : {await db.Signals.CountAsync()}");
        Console.WriteLine($"BacktestRuns      : {await db.BacktestRuns.CountAsync()}");
        Console.WriteLine($"BacktestResults   : {await db.BacktestResults.CountAsync()}");

        var code = opts.TryGetValue("code", out var c) ? c : "7203";
        Console.WriteLine($"\n=== 代表銘柄 {code} の DailyBar（最新5件）===");
        var bars = await db.DailyBars.Where(b => b.Code == code)
            .OrderByDescending(b => b.Date).Take(5).ToListAsync();
        foreach (var b in bars)
            Console.WriteLine($"  {b.Date} C={b.Close} AdjC={b.AdjClose} Vo={b.Volume} AdjVo={b.AdjVolume} Va={b.TurnoverValue}");

        var fin = await db.FinSummaries.Where(f => f.Code == code)
            .OrderByDescending(f => f.DiscloseDate).FirstOrDefaultAsync();
        if (fin != null)
            Console.WriteLine($"\n=== {code} 最新財務: Disc={fin.DiscloseDate} DocType={fin.DocType} Sales={fin.Sales} OP={fin.OperatingProfit} NP={fin.NetProfit} EPS={fin.Eps} BPS={fin.Bps} Eq={fin.Equity} ===");

        var facts = await db.EdinetFinFacts.OrderBy(f => f.Id).Take(10).ToListAsync();
        if (facts.Count > 0)
        {
            Console.WriteLine("\n=== EdinetFinFact サンプル ===");
            foreach (var f in facts)
                Console.WriteLine($"  Code={f.Code} {f.FactName}={f.Value} ({(f.IsConsolidated ? "連結" : "個別")}, ctx={f.ContextId}, unit={f.Unit})");
        }
    }

    // --- 設定検証 ---
    private static void ValidateIngestConfig(IServiceProvider sp, bool skipJQuants)
    {
        var jq = sp.GetRequiredService<IOptions<JQuantsOptions>>().Value;
        var ed = sp.GetRequiredService<IOptions<EdinetOptions>>().Value;
        var missing = new List<string>();
        // --skip-jquants 時は J-Quants キー不要（EDINET のみ取得する宣伝パスを起動可能にする）。
        if (!skipJQuants && string.IsNullOrWhiteSpace(jq.ApiKey)) missing.Add("JQuants:ApiKey");
        if (string.IsNullOrWhiteSpace(ed.SubscriptionKey)) missing.Add("Edinet:SubscriptionKey");
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"必須の APIキーが未設定です: {string.Join(", ", missing)}。\n" +
                "user-secrets に設定してください。例:\n" +
                "  dotnet user-secrets set \"JQuants:ApiKey\" <key>\n" +
                "  dotnet user-secrets set \"Edinet:SubscriptionKey\" <key>");
        }
    }

    // --- 引数パース ---
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++) // args[0] はコマンド名
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i].Substring(2);
            var value = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                ? args[++i] : "true";
            dict[key] = value;
        }
        return dict;
    }

    private static DateOnly RequireDate(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var v))
            throw new ArgumentException($"--{key} (YYYY-MM-DD) が必要です。");
        if (!DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            throw new ArgumentException($"--{key} の日付形式が不正です: {v}（YYYY-MM-DD）");
        return d;
    }

    /// <summary>期間引数を (開始, 終了) に解釈。YYYY=その暦年、YYYY-MM-DD:YYYY-MM-DD=範囲。</summary>
    private static (DateOnly start, DateOnly end) RequireRange(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var v))
            throw new ArgumentException($"--{key} (YYYY または YYYY-MM-DD:YYYY-MM-DD) が必要です。");

        if (v.Contains(':'))
        {
            var parts = v.Split(':', 2);
            var s = DateOnly.Parse(parts[0], CultureInfo.InvariantCulture);
            var e = DateOnly.Parse(parts[1], CultureInfo.InvariantCulture);
            if (s > e) throw new ArgumentException($"--{key} の開始が終了より後です: {v}");
            return (s, e);
        }

        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
            return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

        throw new ArgumentException($"--{key} の形式が不正です: {v}");
    }
}
