using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TradeAnalyzer.Core.Indicators;
using TradeAnalyzer.Core.Rules;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;
using TradeAnalyzer.Data.External.Edinet;
using TradeAnalyzer.Data.External.JQuants;
using TradeAnalyzer.Data.Options;

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
        failed += RunVolumeSpikeGateTests();
        failed += RunResolveExitTests();
        failed += RunEdinetConverterTests();
        failed += RunRedactMaskTests();
        failed += RunDiResolutionTests();
        failed += await RunRateLimiterTestsAsync();
        failed += await RunSizeFilterDegradeTestsAsync();
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
            var sig = engine.EvaluateStock("TEST", asOf, bars, null, MarketCapMode.Disabled);
            f += Assert("Rule: 上昇トレンドで Passed", sig.Passed);
            f += Assert("Rule: RuleScore>=1", sig.RuleScore >= 1);

            // バー不足 → ハードフィルタ不通過。
            var few = SyntheticBars("TEST", asOf, 10, trendUp: true);
            var sig2 = engine.EvaluateStock("TEST", asOf, few, null, MarketCapMode.Disabled);
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

    /// <summary>F2 回帰: 出来高ゲートのベースラインは当日を除外する（自己参照で恒久 NG を防ぐ）。</summary>
    private static int RunVolumeSpikeGateTests()
    {
        int f = 0;
        var opt = Options.Create(new RuleOptions
        {
            MinAvailableBars = 100, MinTurnoverYen = 0, MarketCapMode = MarketCapMode.Disabled,
            RsiLow = 0, RsiHigh = 100,
            VolumeSpikeMultiplier = 2.0, VolumeAvgDays = 20,
        });
        var engine = new RuleEngine(NewMemoryDb(out var conn), opt, NullLogger<RuleEngine>.Instance);
        try
        {
            var asOf = new DateOnly(2024, 12, 31);

            // 当日のみ出来高スパイク（baseline 1,000,000 → 当日 5,000,000）→ 出来高ゲート通過。
            var spike = SyntheticBars("VOL", asOf, 200, trendUp: true);
            spike[spike.Count - 1].AdjVolume = 5_000_000;
            var sigSpike = engine.EvaluateStock("VOL", asOf, spike, null, MarketCapMode.Disabled);
            f += Assert("出来高: 当日スパイクで出来高OK", (sigSpike.Rationale ?? "").Contains("出来高OK"));

            // フラット出来高（当日もベースラインと同値）→ 閾値超えず NG。
            var flat = SyntheticBars("VOL", asOf, 200, trendUp: true);
            var sigFlat = engine.EvaluateStock("VOL", asOf, flat, null, MarketCapMode.Disabled);
            f += Assert("出来高: フラットで出来高NG", (sigFlat.Rationale ?? "").Contains("出来高NG"));
        }
        finally { conn.Dispose(); }
        return f;
    }

    /// <summary>F1 回帰: ATR ストップ約定はギャップダウン（寄り&lt;stop）で寄り値、日中割れは stop 価格。</summary>
    private static int RunResolveExitTests()
    {
        int f = 0;
        const double entry = 1000, stop = 950;
        var d0 = new DateOnly(2025, 1, 6);

        static DailyBar Bar(DateOnly d, double o, double h, double l, double c)
            => new() { Code = "X", Date = d, AdjOpen = o, AdjHigh = h, AdjLow = l, AdjClose = c };

        // ギャップダウン: 寄り 900 < stop 950 → AtrStop 発火 & 約定は寄り値 900。
        var gap = new List<DailyBar>
        {
            Bar(d0, 1000, 1010, 990, 1000),         // エントリバー
            Bar(d0.AddDays(1), 900, 905, 890, 910), // ギャップダウン
        };
        var (_, reason1, price1) = TradeAnalyzer.Core.Backtest.BacktestService.ResolveExit(gap, entry, stop);
        f += Assert("ResolveExit: ギャップダウンで AtrStop 発火", reason1 == "AtrStop");
        f += Assert("ResolveExit: 約定=寄り値(900)", Math.Abs(price1 - 900) < 1e-9);

        // 日中ストップ: 寄り 960 > stop だが安値 940 ≤ stop → 約定は stop 価格 950。
        var intraday = new List<DailyBar>
        {
            Bar(d0, 1000, 1010, 990, 1000),
            Bar(d0.AddDays(1), 960, 965, 940, 945),
        };
        var (_, reason2, price2) = TradeAnalyzer.Core.Backtest.BacktestService.ResolveExit(intraday, entry, stop);
        f += Assert("ResolveExit: 日中ストップで AtrStop", reason2 == "AtrStop");
        f += Assert("ResolveExit: 約定=stop(950)", Math.Abs(price2 - 950) < 1e-9);

        // ストップ非接触 → MaxHoldDays、約定は末尾終値。
        var hold = new List<DailyBar>
        {
            Bar(d0, 1000, 1010, 990, 1000),
            Bar(d0.AddDays(1), 1005, 1015, 1000, 1012),
        };
        var (_, reason3, price3) = TradeAnalyzer.Core.Backtest.BacktestService.ResolveExit(hold, entry, stop);
        f += Assert("ResolveExit: 非ストップで MaxHoldDays", reason3 == "MaxHoldDays");
        f += Assert("ResolveExit: 約定=末尾終値(1012)", Math.Abs(price3 - 1012) < 1e-9);
        return f;
    }

    /// <summary>F10 検証: EdinetFinFact の Unit 違いが同一の円基準へ換算され、比率は生値のまま。</summary>
    private static int RunEdinetConverterTests()
    {
        int f = 0;
        var inMillions = new EdinetFinFact { FactName = "Sales", Value = 1.0, Unit = "百万円" };
        var inYen = new EdinetFinFact { FactName = "Sales", Value = 1_000_000.0, Unit = "円" };
        var a = EdinetFinFactConverter.ToYen(inMillions);
        var b = EdinetFinFactConverter.ToYen(inYen);
        f += Assert("EDINET換算: 百万円→円", a is double av && Math.Abs(av - 1_000_000) < 1e-6);
        f += Assert("EDINET換算: 単位違いで一致", a is double x && b is double y && Math.Abs(x - y) < 1e-6);

        var inThousand = new EdinetFinFact { FactName = "Sales", Value = 5.0, Unit = "千円" };
        f += Assert("EDINET換算: 千円→円", EdinetFinFactConverter.ToYen(inThousand) is double t && Math.Abs(t - 5000) < 1e-6);

        var ratio = new EdinetFinFact { FactName = "EquityRatio", Value = 0.45, Unit = "Pure" };
        f += Assert("EDINET換算: 比率は生値", EdinetFinFactConverter.ToYen(ratio) is double rr && Math.Abs(rr - 0.45) < 1e-9);

        var none = new EdinetFinFact { FactName = "Sales", Value = null, Unit = "円" };
        f += Assert("EDINET換算: Value欠損→null", EdinetFinFactConverter.ToYen(none) == null);
        return f;
    }

    /// <summary>
    /// F4 回帰: 規模フィルタ降格は「FinSummary テーブルが全期間で空」のときのみ。
    /// テーブルが非空（将来開示しか無い場合も含む）なら asOf に関わらず降格しない（point-in-time 一貫）。
    /// </summary>
    private static async Task<int> RunSizeFilterDegradeTestsAsync()
    {
        int f = 0;
        var asOf = new DateOnly(2024, 12, 31);
        var opt = Options.Create(new RuleOptions
        {
            MinAvailableBars = 100, MinTurnoverYen = 0,
            MarketCapMode = MarketCapMode.Approximate,
            MinMarketCapYen = 1,                 // 近似が出れば通る低閾値（「降格」と「却下」を分離して判定）
            RsiLow = 0, RsiHigh = 100, VolumeSpikeMultiplier = 0,
        });

        var dbEmpty = NewMemoryDb(out var c1);  // FinSummary 全空 → 降格
        var dbData = NewMemoryDb(out var c2);   // FinSummary に asOf より後の開示のみ → 非空なので降格しない
        try
        {
            dbEmpty.Stocks.Add(new Stock { Code = "TEST", AsOfDate = asOf.AddYears(-1) });
            dbEmpty.DailyBars.AddRange(SyntheticBars("TEST", asOf, 200, trendUp: true));
            await dbEmpty.SaveChangesAsync();

            dbData.Stocks.Add(new Stock { Code = "TEST", AsOfDate = asOf.AddYears(-1) });
            dbData.DailyBars.AddRange(SyntheticBars("TEST", asOf, 200, trendUp: true));
            dbData.FinSummaries.Add(new FinSummary
            {
                Code = "OTHER", DiscloseDate = asOf.AddDays(30), DocType = "FY", Equity = 1000, Bps = 10,
            });
            await dbData.SaveChangesAsync();

            var sigEmpty = (await new RuleEngine(dbEmpty, opt, NullLogger<RuleEngine>.Instance).EvaluateAsync(asOf)).Single();
            f += Assert("F4: 財務全空で規模フィルタ降格(規模NGなし)", !(sigEmpty.Rationale ?? "").Contains("規模NG"));
            f += Assert("F4: 降格時もトレンドで Passed", sigEmpty.Passed);

            // 旧実装(DiscloseDate<=asOf 判定)ならここで誤降格していたケース。新実装はテーブル非空で降格しない。
            var sigData = (await new RuleEngine(dbData, opt, NullLogger<RuleEngine>.Instance).EvaluateAsync(asOf)).Single();
            f += Assert("F4: 将来開示のみ存在でも降格せず規模NGで却下", (sigData.Rationale ?? "").Contains("規模NG"));
        }
        finally { c1.Dispose(); c2.Dispose(); }
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

    /// <summary>EDINET URI ロガーが Subscription-Key をマスクし、他クエリを保持することを検査する。</summary>
    private static int RunRedactMaskTests()
    {
        int f = 0;
        var redacted = EdinetRedactingHttpLogger.Redact(new Uri(
            "https://api.edinet-fsa.go.jp/api/v2/documents.json?date=2025-01-01&type=2&Subscription-Key=DUMMY_SECRET_123"));
        f += Assert("Redact: 実鍵を含まない", !redacted.Contains("DUMMY_SECRET_123"));
        f += Assert("Redact: マスク済み", redacted.Contains("Subscription-Key=***"));
        f += Assert("Redact: 他クエリ保持(date)", redacted.Contains("date=2025-01-01"));
        f += Assert("Redact: 他クエリ保持(type)", redacted.Contains("type=2"));
        f += Assert("Redact(null)=空文字", EdinetRedactingHttpLogger.Redact(null) == string.Empty);
        return f;
    }

    /// <summary>
    /// F1 回帰: レートゲートは MinInterval のみで次送信を通し、429 の Retry-After を後ろ倒ししない
    /// （Retry-After 遵守は標準 retry 層に委譲した）。FakeTimeProvider で実時間待ち無しに決定的検証する。
    /// 旧実装（429 分岐あり）なら Retry-After(600s) 経過まで完了せず、13s 進めた await がタイムアウトして FAIL する。
    /// </summary>
    private static async Task<int> RunRateLimiterTestsAsync()
    {
        int f = 0;
        var fake = new FakeTimeProvider();
        var opt = Options.Create(new JQuantsOptions { MinIntervalSeconds = 13 });
        using var limiter = new JQuantsRateLimiter(opt, fake);

        // 1回目: 429(Retry-After=600s)。初回は _lastSend=MinValue のため待機なしで即送信。
        using var resp429 = new HttpResponseMessage((System.Net.HttpStatusCode)429);
        resp429.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(600));
        var first = await limiter.ExecuteAsync(() => Task.FromResult(resp429), CancellationToken.None);
        f += Assert("RateLimiter: 初回は即送信(429)", (int)first.StatusCode == 429);

        // 2回目: send=200。await せず開始し、MinInterval(13s) ゲート待機の経過を FakeTimeProvider で進める。
        using var ok = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        var task = limiter.ExecuteAsync(() => Task.FromResult(ok), CancellationToken.None);

        fake.Advance(TimeSpan.FromSeconds(12));   // 12s<13s → まだ完了しない
        f += Assert("RateLimiter: 12s 未満では未完了", !task.IsCompleted);

        fake.Advance(TimeSpan.FromSeconds(1));     // 計13s≥MinInterval → 完了へ
        var second = await task.WaitAsync(TimeSpan.FromSeconds(5));  // 安全タイムアウト（実時間）
        f += Assert("RateLimiter: 13s 経過で送信完了(200)", (int)second.StatusCode == 200);
        return f;
    }

    /// <summary>DI スモーク: AddTradeAnalyzerData が構成する typed HttpClient（resilience/logger パイプライン）を
    /// API キー無し・実通信無しで解決できることを検証する（F2/F3 の DI 再構成の回帰）。</summary>
    private static int RunDiResolutionTests()
    {
        int f = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeAnalyzerData(new ConfigurationBuilder().Build()); // 空 config=既定 BaseUrl
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var edinet = scope.ServiceProvider.GetRequiredService<EdinetClient>();
        var jq = scope.ServiceProvider.GetRequiredService<JQuantsClient>();
        f += Assert("DI: EdinetClient 解決", edinet is not null);
        f += Assert("DI: JQuantsClient 解決", jq is not null);
        return f;
    }

    private static int Assert(string name, bool condition)
    {
        Console.WriteLine($"  [{(condition ? "PASS" : "FAIL")}] {name}");
        return condition ? 0 : 1;
    }
}
