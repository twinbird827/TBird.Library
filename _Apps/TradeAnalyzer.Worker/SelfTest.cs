using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Core.Indicators;
using TradeAnalyzer.Core.Rules;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Worker;

/// <summary>
/// APIキー不要の単体検証（プラン検証手順3）。
/// 指標の期待値、ルール評価、そして「先読みが起きない（t の判断に Date&gt;t を使わない）」ことを検査する。
/// </summary>
public static class SelfTest
{
    public static async Task RunAsync()
    {
        int failed = 0;
        failed += RunIndicatorTests();
        failed += RunRuleEngineTests();
        failed += await RunLookAheadTestAsync();
        failed += await RunEntryAfterDecisionTestAsync();

        if (failed == 0) Console.WriteLine("SelfTest: 全テスト PASS");
        else
        {
            Console.WriteLine($"SelfTest: {failed} 件 FAIL");
            throw new InvalidOperationException($"SelfTest で {failed} 件失敗しました。");
        }
    }

    private static int RunIndicatorTests()
    {
        int f = 0;
        var values = new double[] { 1, 2, 3, 4, 5 };
        var sma3 = TechnicalIndicators.Sma(values, 3);
        f += Assert("SMA[2]=2", sma3[2] == 2.0);
        f += Assert("SMA[4]=4", sma3[4] == 4.0);
        f += Assert("SMA[0]=null", sma3[0] == null);

        // 単調増加列の RSI は 100。
        var up = new double[20];
        for (int i = 0; i < up.Length; i++) up[i] = 100 + i;
        var rsi = TechnicalIndicators.Rsi(up, 14);
        f += Assert("RSI(up)=100", rsi[19] is double r && Math.Abs(r - 100) < 1e-6);

        var ema = TechnicalIndicators.Ema(values, 3);
        f += Assert("EMA[1]=null", ema[1] == null);
        f += Assert("EMA[4] not null", ema[4] != null);

        var highs = new double[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 };
        var lows = new double[] { 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 };
        var closes = new double[] { 9.5, 10.5, 11.5, 12.5, 13.5, 14.5, 15.5, 16.5, 17.5, 18.5, 19.5, 20.5, 21.5, 22.5, 23.5, 24.5 };
        var atr = TechnicalIndicators.Atr(highs, lows, closes, 14);
        f += Assert("ATR last not null", atr[atr.Length - 1] != null);
        return f;
    }

    private static int RunRuleEngineTests()
    {
        int f = 0;
        var opt = Options.Create(new RuleOptions
        {
            MinAvailableBars = 100,
            MinTurnoverYen = 0,
            MarketCapMode = MarketCapMode.Disabled,
            SmaShort = 25, SmaLong = 75,
            RsiLow = 0, RsiHigh = 100,
            VolumeSpikeMultiplier = 0,
        });
        var engine = new RuleEngine(NewMemoryDb(out var conn), opt, NullLogger<RuleEngine>.Instance);
        try
        {
            // 上昇トレンドのバー → トレンドゲート通過 (Passed=true)。
            var asOf = new DateOnly(2024, 12, 31);
            var bars = SyntheticBars("TEST", asOf, 200, trendUp: true);
            var sig = engine.EvaluateStock("TEST", asOf, bars, null);
            f += Assert("Rule: 上昇トレンドで Passed", sig.Passed);
            f += Assert("Rule: RuleScore>=1", sig.RuleScore >= 1);

            // バー不足 → ハードフィルタ不通過。
            var few = SyntheticBars("TEST", asOf, 10, trendUp: true);
            var sig2 = engine.EvaluateStock("TEST", asOf, few, null);
            f += Assert("Rule: バー不足で not Passed", !sig2.Passed);

            // 時価総額近似: Eq/BPS×px。
            var cap = RuleEngine.ApproxMarketCap(new FinSummary { Equity = 1000, Bps = 10 }, 50);
            f += Assert("MarketCap≈(1000/10)*50=5000", cap is double c && Math.Abs(c - 5000) < 1e-6);
            var capBad = RuleEngine.ApproxMarketCap(new FinSummary { Equity = 1000, Bps = 0 }, 50);
            f += Assert("MarketCap: BPS<=0 → null", capBad == null);
        }
        finally { conn.Dispose(); }
        return f;
    }

    /// <summary>先読み防止: t 時点の評価は Date&gt;t のバーが DB にあっても結果が変わらない。</summary>
    private static async Task<int> RunLookAheadTestAsync()
    {
        int f = 0;
        var asOf = new DateOnly(2024, 12, 31);
        var opt = Options.Create(new RuleOptions
        {
            MinAvailableBars = 100, MinTurnoverYen = 0, MarketCapMode = MarketCapMode.Disabled,
            RsiLow = 0, RsiHigh = 100, VolumeSpikeMultiplier = 0,
        });

        // DB1: Date<=asOf のみ
        var dbTrunc = NewMemoryDb(out var c1);
        // DB2: asOf 以降に極端な値のバーを追加（漏れたら指標が変わる）
        var dbFull = NewMemoryDb(out var c2);
        try
        {
            var upTo = SyntheticBars("TEST", asOf, 200, trendUp: true);
            dbTrunc.DailyBars.AddRange(upTo);
            dbTrunc.Stocks.Add(new Stock { Code = "TEST", AsOfDate = asOf.AddYears(-1) });
            await dbTrunc.SaveChangesAsync();

            var withFuture = new List<DailyBar>(upTo);
            for (int i = 1; i <= 30; i++)
            {
                var d = asOf.AddDays(i);
                withFuture.Add(new DailyBar { Code = "TEST", Date = d, AdjClose = 99999, AdjVolume = 1, AdjHigh = 99999, AdjLow = 99999, AdjOpen = 99999 });
            }
            dbFull.DailyBars.AddRange(withFuture);
            dbFull.Stocks.Add(new Stock { Code = "TEST", AsOfDate = asOf.AddYears(-1) });
            await dbFull.SaveChangesAsync();

            var sigTrunc = (await new RuleEngine(dbTrunc, opt, NullLogger<RuleEngine>.Instance).EvaluateAsync(asOf)).Single();
            var sigFull = (await new RuleEngine(dbFull, opt, NullLogger<RuleEngine>.Instance).EvaluateAsync(asOf)).Single();

            f += Assert("先読み防止: RuleScore 一致", sigTrunc.RuleScore == sigFull.RuleScore);
            f += Assert("先読み防止: Passed 一致", sigTrunc.Passed == sigFull.Passed);
            f += Assert("先読み防止: Rationale 一致", sigTrunc.Rationale == sigFull.Rationale);
        }
        finally { c1.Dispose(); c2.Dispose(); }
        return f;
    }

    /// <summary>エントリは必ず判断日より後の営業日（同日・過去を使わない）。</summary>
    private static async Task<int> RunEntryAfterDecisionTestAsync()
    {
        int f = 0;
        var db = NewMemoryDb(out var conn);
        try
        {
            var asOf = new DateOnly(2024, 12, 31);
            db.Stocks.Add(new Stock { Code = "TEST", AsOfDate = asOf.AddYears(-2) });
            var bars = SyntheticBars("TEST", new DateOnly(2025, 6, 30), 400, trendUp: true);
            db.DailyBars.AddRange(bars);
            await db.SaveChangesAsync();

            var rules = new RuleEngine(db, Options.Create(new RuleOptions
            {
                MinAvailableBars = 100, MinTurnoverYen = 0, MarketCapMode = MarketCapMode.Disabled,
                RsiLow = 0, RsiHigh = 100, VolumeSpikeMultiplier = 0,
            }), NullLogger<RuleEngine>.Instance);
            var bt = new TradeAnalyzer.Core.Backtest.BacktestService(db, rules,
                Options.Create(new TradeAnalyzer.Core.Backtest.BacktestOptions { RebalanceIntervalDays = 20, TopN = 5, MaxHoldDays = 10, AtrStopMultiplier = 0 }),
                NullLogger<TradeAnalyzer.Core.Backtest.BacktestService>.Instance);

            var run = await bt.RunAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31),
                new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30), "selftest");

            f += Assert("Backtest: トレード発生", run.TradeCount > 0);
            f += Assert("Backtest: Exit>=Entry", run.Results.All(r => r.ExitDate >= r.EntryDate));
        }
        finally { conn.Dispose(); }
        return f;
    }

    // --- helpers ---
    private static AppDbContext NewMemoryDb(out SqliteConnection conn)
    {
        conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>合成日次バー（営業日近似で土日を除く、終値トレンド付き）。</summary>
    private static List<DailyBar> SyntheticBars(string code, DateOnly end, int count, bool trendUp)
    {
        var bars = new List<DailyBar>(count);
        var dates = new List<DateOnly>();
        var d = end;
        while (dates.Count < count)
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                dates.Add(d);
            d = d.AddDays(-1);
        }
        dates.Reverse();
        for (int i = 0; i < dates.Count; i++)
        {
            double px = trendUp ? 1000 + i * 2.0 : 1000 - i * 0.5;
            bars.Add(new DailyBar
            {
                Code = code, Date = dates[i],
                AdjOpen = px, AdjHigh = px + 5, AdjLow = px - 5, AdjClose = px,
                AdjVolume = 1_000_000, Close = px, Volume = 1_000_000,
            });
        }
        return bars;
    }

    private static int Assert(string name, bool condition)
    {
        Console.WriteLine($"  [{(condition ? "PASS" : "FAIL")}] {name}");
        return condition ? 0 : 1;
    }
}
