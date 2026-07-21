using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Core.Backtest;
using TradeAnalyzer.Core.Ingest;
using TradeAnalyzer.Core.Rules;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;
using TradeAnalyzer.Data.Options;
using TradeAnalyzer.Worker.Claude;

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
  signals --is <期間> --oos <期間>           IS/OOS 各ウィンドウの全リバランス日でルール評価→Signal 保存（Python 母集団入力。要 ingest 済み DB）
  backtest --is <期間> --oos <期間>          バックテスト実行（期間=YYYY または YYYY-MM-DD:YYYY-MM-DD。両モードとも要 signals 済み）
          [--use-ml true|false]              picks を MlScore 順（true）/RuleScore 順（false 既定）で選ぶ A/B 切替（母集団は両モードとも保存 Signal）
  run-today                                  段階3a 当日 EOD 推論: 当日 ingest→最新営業日 analyze→Python 採点→Top-K 出力（要 migrate 済み DB）
          [--skip-jquants]                   当日 ingest 済みの再実行時のみ（当日 bar を足さず既存 DB 最新営業日を採点）
  explain-today                              段階3b 当日定性層: run-today 後の Top-K に実データ注入→Claude 根拠文生成→QualitativeJson 書戻し
          [--date YYYY-MM-DD]                対象日を明示（既定は直近営業日）。要 run-today 済み（MlScore 充足）
          [--force]                          生成済み（QualitativeJson あり）銘柄も再生成（既定は再利用スキップ＝クレジット節約）
  selftest                                   APIキー不要の単体検証（指標/ルール/先読み防止）

例:
  dotnet run -- migrate
  dotnet run -- ingest --from 2024-01-01 --to 2025-12-31
  dotnet run -- analyze --date 2025-06-30
  dotnet run -- signals --is 2024 --oos 2025
  dotnet run -- backtest --is 2024 --oos 2025 --use-ml true
  dotnet run -- run-today");
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

        var signals = await PersistSignalsForDateAsync(db, rules, date);

        logger.LogInformation("analyze {Date}: {Total} 件保存 ({Passed} 通過)",
            date, signals.Count, signals.Count(s => s.Passed));
    }

    /// <summary>
    /// IS/OOS 各ウィンドウの全リバランス日でルール評価し Signal を保存する（Python への母集団入力生成）。
    /// リバランス日列挙は BacktestService と同一の純粋関数 EnumerateRebalanceDays に一元化し、
    /// ML バックテストが読む OOS の日付集合と母数を構造的に一致させる。Passed 絞り込みは読取側が行うため全件保存する。
    /// </summary>
    public static async Task SignalsAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        var (isStart, isEnd) = RequireRange(opts, "is");
        var (oosStart, oosEnd) = RequireRange(opts, "oos");

        var db = sp.GetRequiredService<AppDbContext>();
        var rules = sp.GetRequiredService<RuleEngine>();
        var logger = sp.GetRequiredService<ILogger<RuleEngine>>();
        int interval = sp.GetRequiredService<IOptions<BacktestOptions>>().Value.RebalanceIntervalDays;

        int isCount = await GenerateSignalsForWindowAsync(db, rules, isStart, isEnd, interval);
        int oosCount = await GenerateSignalsForWindowAsync(db, rules, oosStart, oosEnd, interval);

        logger.LogInformation("signals 完了: IS[{IsStart:yyyy-MM-dd}..{IsEnd:yyyy-MM-dd}]={IsDays}日, OOS[{OosStart:yyyy-MM-dd}..{OosEnd:yyyy-MM-dd}]={OosDays}日 のリバランス日で Signal を生成しました。",
            isStart, isEnd, isCount, oosStart, oosEnd, oosCount);
    }

    /// <summary>
    /// 1ウィンドウのリバランス日を列挙し、各日で <see cref="PersistSignalsForDateAsync"/> による
    /// date 単位 delete→insert（冪等）で Signal を保存する。列挙日数を返す。
    /// SelfTest からも本番経路を検証するため internal で公開する（同一 TradeAnalyzer.Worker アセンブリ内）。
    /// </summary>
    internal static async Task<int> GenerateSignalsForWindowAsync(
        AppDbContext db, RuleEngine rules, DateOnly start, DateOnly end, int interval)
    {
        // 取引日は BacktestService と共通のヘルパで取得し、母数起点を1箇所に閉じ込める
        // （SignalsAsync は ct を持たないため CancellationToken.None＝既定を渡す）。
        var tradingDays = await BacktestService.QueryTradingDaysAsync(db, start, end);

        var rebalanceDays = BacktestService.EnumerateRebalanceDays(tradingDays, interval).ToList();
        foreach (var t in rebalanceDays)
            await PersistSignalsForDateAsync(db, rules, t);
        return rebalanceDays.Count;
    }

    /// <summary>
    /// 単一リバランス日 <paramref name="t"/> の冪等保存プリミティブ。RuleEngine で評価し、
    /// 既存 Signal を date 単位で delete してから全件 insert する（delete→insert の冪等順・
    /// Passed 絞り込みなしの全件保存・date 単位スコープの契約をここ1箇所に閉じ込める）。
    /// 評価済み <see cref="Signal"/> リストを返し、呼出側がログ用件数を導出できるようにする。
    /// analyze（単一日）/ signals（ウィンドウ）/ SelfTest が同一コードを共有する。
    /// </summary>
    private static async Task<List<Signal>> PersistSignalsForDateAsync(
        AppDbContext db, RuleEngine rules, DateOnly t)
    {
        var signals = await rules.EvaluateAsync(t);
        await db.Signals.Where(s => s.Date == t).ExecuteDeleteAsync();
        db.Signals.AddRange(signals);
        await db.SaveChangesAsync();
        return signals;
    }

    public static async Task BacktestAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        var (isStart, isEnd) = RequireRange(opts, "is");
        var (oosStart, oosEnd) = RequireRange(opts, "oos");

        // --use-ml で MlScore 順／RuleScore 順を A/B 切替。既定は Options.UseMlScore（appsettings）。
        // DI singleton の破壊書換えはせず、引数で RunAsync に渡す（常駐/テスト再利用での前回値漏れを防ぐ）。
        bool useMl = sp.GetRequiredService<IOptions<BacktestOptions>>().Value.UseMlScore;
        if (opts.TryGetValue("use-ml", out var useMlStr))
        {
            if (!bool.TryParse(useMlStr, out useMl))
                throw new ArgumentException($"--use-ml は true/false を指定してください: {useMlStr}");
        }

        var backtest = sp.GetRequiredService<BacktestService>();
        var label = $"IS{isStart:yyyy}-OOS{oosStart:yyyy}-{(useMl ? "ML" : "Rule")}";
        var run = await backtest.RunAsync(isStart, isEnd, oosStart, oosEnd, useMl, label: label);

        Console.WriteLine($"Backtest 完了 [{label}]: trades={run.TradeCount}, winRate={run.WinRate:P1}, avgReturn={run.AvgReturn:P2}");
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

    /// <summary>
    /// 段階3a 当日 EOD 推論オーケストレータ。当日 ingest→最新営業日 t の解決→t の analyze→Python 採点
    /// （predict.py）→ MlScore null 検査→ Top-K 標準出力 を単一プロセスで逐次実行する。各段は失敗で即 throw
    /// （silent fallback 禁止）。Process 分離で Python を起動し ExitCode で成否を判定する（pythonnet 不採用）。
    /// </summary>
    public static async Task RunTodayAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        bool skipJQuants = opts.ContainsKey("skip-jquants");

        var db = sp.GetRequiredService<AppDbContext>();
        var rules = sp.GetRequiredService<RuleEngine>();
        var ingest = sp.GetRequiredService<IngestService>();
        var pythonOptions = sp.GetRequiredService<IOptions<PythonOptions>>();
        int topN = sp.GetRequiredService<IOptions<BacktestOptions>>().Value.TopN;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("run-today");

        // 1. 当日 ingest（型付き IngestService を直接呼ぶ＝CLI ラッパの MigrateAsync は同梱しない）。
        //    Migrate を呼ばないのはコールドスタート前提（段階1/2 で migrate/ingest 済みの trade.db）＋
        //    3a は新規マイグレーションを導入しないため。空/未マイグレーション DB だと ingest 内の
        //    ExecuteDeleteAsync が no such table で停止する（run-today.ps1 冒頭に「要 migrate 済み DB」明記）。
        ValidateIngestConfig(sp, skipJQuants);
        // Python 設定は数分の ingest/analyze が完走した後の Python 段で汎用 AOORE として死ぬ前に、
        // 起動直後に config キー名つきで fail-fast する（run-today 専用＝Python を使わない ingest 単体には課さない）。
        ValidatePythonConfig(pythonOptions.Value);
        // 当日は JST 固定で導出する（ホストローカル日付に依存させない＝非 JST ホスト移植でも1日ズレない）。
        // TimeProvider は既存 JQuantsRateLimiter と同じ「DI 未登録時は TimeProvider.System」方針で入手する。
        var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
        var today = ResolveTodayJst(timeProvider);
        // --edinet-limit 0 相当（EDINET CSV 解析を止める。文書一覧 API は日次で残る＝段階2と同じ）。
        await ingest.IngestAsync(today, today, skipJQuants, edinetLimitPerDay: 0);

        // 2. 最新営業日 t の解決＝DailyBar 存在日の最新（当日 EOD が入っていれば今日、未反映なら直近営業日）。
        var t = await ResolveLatestTradingDayAsync(db, today,
            "採点には複数年 ingest 済みの履歴が必要です。広期間 ingest でバックフィルしてください。");
        if (t < today)
            logger.LogWarning(
                "当日 {Today} の EOD が DB に未反映のため直近営業日 {T} を採点対象にします（EOD 反映時刻/休場日の可能性）。",
                today, t);

        // 3. 当日 analyze（delete→insert で冪等）。MlScore=null で再挿入されるため次段の predict が必須。
        var signals = await PersistSignalsForDateAsync(db, rules, t);
        logger.LogInformation("analyze {T}: {Total} 件保存 ({Passed} 通過)",
            t, signals.Count, signals.Count(s => s.Passed));

        // 4. Python 採点。trade.db の絶対パスは C# が実際に使う接続から導出する（U-DBPATH 方式 A）。
        //    接続文字列は相対 Data Source=trade.db で SQLite は C# プロセスの CWD 基準で解決するため、
        //    DbConnection.DataSource（EF Core Relational の面のみ＝Microsoft.Data.Sqlite を直接参照しない）の
        //    値を Path.GetFullPath で絶対化し、Python の CWD（MlDir）と別ファイルを開かないようにする。
        string dbPath = Path.GetFullPath(db.Database.GetDbConnection().DataSource);
        string predictScript = pythonOptions.Value.PredictScript;
        await RunPythonAsync(pythonOptions.Value, logger, predictScript, new[]
        {
            ("--db", dbPath),
            ("--date", t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        });

        // 5. null 検査＋6. Top-K 読取を AsNoTracking で1回にまとめる。
        //    重要: step3 の PersistSignalsForDateAsync は同一 scoped AppDbContext に MlScore=null の Signal を
        //    トラッキングしたまま SaveChanges する。トラッキングのまま読むと EF Core は識別マップ上の追跡済み
        //    インスタンス（MlScore=null）を返し Python 更新値で上書きしない＝null 検査が誤発火する。
        //    AsNoTracking（DB 行から新規生成）で Python が書いた MlScore を正しく反映する（BacktestService:97 と同作法）。
        var passed = await db.Signals.AsNoTracking()
            .Where(s => s.Date == t && s.Passed)
            .ToListAsync();
        if (passed.Any(r => r.MlScore is null))
            throw new InvalidOperationException(
                $"{t}: MlScore 未設定の Passed 行があります（Python 書戻し漏れ）。predict.py の出力を確認してください。");

        // 6. Top-K 出力。並べ替え／件数は純粋関数 SelectTopPicks（バックテスト picks と同一規則）に共通化し、
        //    本番出力と戦略の乖離を防ぐ（ソートキーの正典は SelectTopPicks の XML doc）。
        //    件数は当日 Top-K＝バックテスト TopN（同一概念。F11 で統合）の単一ノブを使う。
        var top = BacktestService.SelectTopPicks(passed, topN, useMl: true);

        // 日付は Invariant 明示（explain-today ヘッダと同型。非グレゴリオ暦カルチャ対策・表示専用）。
        Console.WriteLine($"=== {t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} Top-{topN}（MlScore 降順, Passed {passed.Count} 件中）===");
        Console.WriteLine($"{"Code",-8} {"MlScore",10} {"RuleScore",9}  Rationale");
        foreach (var s in top)
            Console.WriteLine($"{s.Code,-8} {s.MlScore!.Value,10:F4} {s.RuleScore,9}  {s.Rationale}");
    }

    /// <summary>
    /// 段階3b 当日定性層オーケストレータ。run-today が確定した当日 t の Top-K に実データを注入して Claude で
    /// 根拠文／リスクを生成し、<see cref="Signal.QualitativeJson"/> へ書戻して標準出力に付す。run-today と別コマンド
    /// ＝フォールバック隔離（Claude が落ちても ML パイプラインは無傷）。Claude 失敗は throw せず当該銘柄スキップ
    /// （非必須層＝フォールバック契約）。ただしデータ前提未達（取引日なし）は run-today と同型に fail-fast（ExitCode=1）。
    /// 同一 t への再実行は生成済み（QualitativeJson あり）銘柄を既定でスキップし <c>--force</c> で再生成
    /// （facts は同一 t で決定論的＝再生成は情報利得ゼロのクレジット消費。run-today 再実行は date 単位
    /// delete→insert で QualitativeJson=null に戻るため、このスキップは run-today を挟まない再実行でのみ発動）。
    /// </summary>
    public static async Task ExplainTodayAsync(IServiceProvider sp, string[] args)
    {
        var opts = ParseOptions(args);
        bool force = opts.ContainsKey("force");

        var db = sp.GetRequiredService<AppDbContext>();
        int topN = sp.GetRequiredService<IOptions<BacktestOptions>>().Value.TopN;
        var claudeOpt = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
        var claude = sp.GetRequiredService<ClaudeCliAnalysisService>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("explain-today");
        var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;

        // Claude 設定はループ前に config キー名つきで fail-fast（誤設定を per-銘柄 catch が飲んで全銘柄スキップ→
        // ExitCode=0 に偽装するのを防ぐ。config 誤設定=fatal／Claude 実行時失敗=非致命）。
        ValidateClaudeConfig(claudeOpt);

        // 1. 対象日 t の解決（--date 指定 or 直近営業日＝run-today と共通ヘルパ）。取引日皆無は前提破綻＝fail-fast。
        DateOnly t;
        if (opts.ContainsKey("date"))
        {
            t = RequireDate(opts, "date");
        }
        else
        {
            var today = ResolveTodayJst(timeProvider);
            t = await ResolveLatestTradingDayAsync(db, today, "run-today 済みの DB が前提です。");
        }

        // 2. 当日 Passed 行を AsNoTracking で読取（run-today と別プロセスなら EF 一次キャッシュの罠は無いが射影で明示）。
        var passed = await db.Signals.AsNoTracking()
            .Where(s => s.Date == t && s.Passed)
            .ToListAsync();
        if (passed.Count == 0)
        {
            // 対象日に Passed 行が皆無＝run-today 未実行 or --date 指定違いの可能性。無言 no-op を避け診断を出す（非致命）。
            logger.LogWarning("{T}: Passed 行がありません（run-today 未実行 or --date 指定違いの可能性）。", t);
            return;
        }
        // null 検査は SelectTopPicks(useMl:true) の .Value 前に置く（null 混入で InvalidOperationException＝非致命に到達不可）。
        // Passed=0（上）と非対称に throw＝ExitCode=1: Passed=0 は全面下落局面で正当に全滅し得る（run-today も
        // Top-0 を緑出力する契約）が、MlScore null の Passed 行は run-today 成功時に同条件で既に throw 済み（L276-278）
        // ＝この状態は常にパイプライン障害であり、警告のままだとスケジューラが緑で真の障害が見えない。
        if (passed.Any(r => r.MlScore is null))
            throw new InvalidOperationException(
                $"{t}: MlScore 未設定の Passed 行があります（run-today の ML 採点が未完了＝パイプライン障害）。" +
                "run-today を成功させてから explain-today を再実行してください。");

        // 3. Top-K（run-today と同一の純粋関数で並べ替え）。Claude に回すのは Top-K のみ＝コスト/クレジットを bound。
        var top = BacktestService.SelectTopPicks(passed, topN, useMl: true);

        var results = new Dictionary<string, ClaudeAnalysisResult>();
        var reused = new HashSet<string>();
        // --force 再生成失敗だが旧 QualitativeJson が残存（last-good 保持）した銘柄数。表示と DB 状態の一致用。
        int kept = 0;
        foreach (var signal in top)
        {
            // 生成済み銘柄は既定でスキップ（--force で上書き再生成）。部分失敗の回復・二重トリガの再実行で
            // 成功済み銘柄まで Claude を払い直さない（クレジット節約）＋残存 JSON と表示の食い違いも防ぐ。
            if (!force && signal.QualitativeJson != null)
            {
                reused.Add(signal.Code);
                continue;
            }
            // 4. 実データ収集（DB 実数＋C# 派生指標）。
            var facts = await ClaudeFactGatherer.GatherAsync(db, t, signal);
            // 5. Claude 採点。null（失敗）なら当該銘柄はスキップ（ML のみ・ログのみ）。1 銘柄の失敗が他を止めない。
            //    旧 JSON あり（--force 再生成失敗）なら DB は last-good を保持したまま＝「保持」として集計する。
            var res = await claude.AnalyzeAsync(facts);
            if (res == null)
            {
                if (signal.QualitativeJson != null) kept++;
                continue;
            }

            // JST の生成時刻（provenance）。銘柄ごとに書戻し直前で評価する（Claude 実行は 1 銘柄あたり最大
            // TimeoutMinutes かかるため、ループ前の一括取得では後半の銘柄で実生成時刻と大きくずれる）。
            var generatedAt = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Jst);

            // 6. 根拠文の書戻し（追跡回避 UPDATE。AsNoTracking で読んだ行の部分更新）。
            string json = JsonSerializer.Serialize(new
            {
                summary = res.Summary,
                risks = res.Risks,
                usedFacts = res.UsedFacts,
                model = res.Model,
                // 経路は CLI 直結の1実装のみだが provenance の JSON 契約（route フィールド）は維持する
                // （SDK 経路を再導入したらここも実経路値に戻す）。
                route = "cli",
                generatedAt = generatedAt.ToString("o"),
                numericUnverified = res.NumericUnverified,
            });
            await db.Signals.Where(s => s.Date == t && s.Code == signal.Code)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.QualitativeJson, json));
            results[signal.Code] = res;
        }

        // 7. Top-K 出力（summary/risks/numericUnverified を付す）。日付は Invariant 明示（非グレゴリオ暦
        //    カルチャのホストで年が仏暦等になるのを防ぐ。r4-F4 と同機序・表示専用）。
        string tStr = t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string keptSuffix = kept > 0 ? $"・保持(再生成失敗) {kept} 件" : "";
        Console.WriteLine($"=== {tStr} Top-{topN} 定性レビュー（Passed {passed.Count} 件中・{results.Count}/{top.Count} 件生成・既存再利用 {reused.Count} 件{keptSuffix}）===");
        foreach (var s in top)
        {
            Console.WriteLine($"\n[{s.Code}] MlScore={s.MlScore!.Value:F4} RuleScore={s.RuleScore}");
            if (results.TryGetValue(s.Code, out var res))
            {
                Console.WriteLine($"  要約: {res.Summary}");
                foreach (var risk in res.Risks) Console.WriteLine($"  リスク: {risk}");
                if (res.NumericUnverified) Console.WriteLine("  ⚠ numericUnverified（注入外の数値が混入した疑い）");
            }
            else if (reused.Contains(s.Code))
            {
                Console.WriteLine("  （既存生成あり・再利用。--force で再生成）");
            }
            else if (s.QualitativeJson != null)
            {
                // --force 再生成失敗。DB は旧 JSON（last-good）を保持したまま＝「生成なし」ではない。
                // 失敗理由は ClaudeCliAnalysisService の per-銘柄 LogWarning 側にある。
                Console.WriteLine("  （再生成失敗・既存生成を保持。ログ参照）");
            }
            else
            {
                Console.WriteLine("  （Claude 生成なし・ML のみ）");
            }
        }
    }

    /// <summary>
    /// uv run python &lt;script&gt; &lt;args...&gt; を Python:MlDir を作業ディレクトリに起動し、ExitCode≠0 なら
    /// stderr を添えて throw する小ヘルパ。Process 起動の堅牢化（逐次ログ/timeout kill/EOF flush/起動失敗の
    /// 包み込み）は共有起動点 <see cref="ProcessRunner.RunAsync"/> へ委譲し、本メソッドは psi 構築と
    /// fail-fast 判断のみ持つ。double.MinValue 等での黙殺は禁止（段階2 silent fallback 禁止方針を Process
    /// 境界にも適用）。設定は型付き <see cref="PythonOptions"/> から受ける。
    /// </summary>
    private static async Task RunPythonAsync(
        PythonOptions opt, ILogger logger, string scriptPath,
        IReadOnlyList<(string key, string value)> options, CancellationToken ct = default)
    {
        string uvPath = string.IsNullOrWhiteSpace(opt.UvPath) ? "uv" : opt.UvPath; // PATH 非依存なら絶対パス指定可。
        // MlDir 未指定時は AppPaths.MlScriptsDir（_Apps/ml の絶対パス・CWD 非依存）。相対指定時のみ
        // リポジトリルート基準で絶対化する。ml スクリプトは追跡ソースのため成果物置き場（_Tools）とは別。
        string mlDir = string.IsNullOrWhiteSpace(opt.MlDir)
            ? AppPaths.MlScriptsDir
            : Path.GetFullPath(opt.MlDir, AppPaths.RepoRoot);

        var psi = new ProcessStartInfo
        {
            FileName = uvPath,
            WorkingDirectory = mlDir,
            // Redirect*/UseShellExecute は ProcessRunner.RunAsync が強制する（ここでは設定しない）。
            // Python の print は日本語を含む。Windows 既定 console は cp932 のため、双方を UTF-8 に固定して
            // ログ文字化けを防ぐ（PYTHONUTF8=1 で Python が UTF-8 出力、Standard*Encoding で C# が UTF-8 読取）。
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.Environment["PYTHONUTF8"] = "1";
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("python");
        psi.ArgumentList.Add(scriptPath);
        foreach (var (key, value) in options)
        {
            psi.ArgumentList.Add(key);
            psi.ArgumentList.Add(value);
        }

        // TimeoutMinutes の非正値クランプはしない（RunAsync が ArgumentOutOfRangeException で fail-fast＝設定ミスの顕在化）。
        var result = await ProcessRunner.RunAsync(psi, logger, TimeSpan.FromMinutes(opt.TimeoutMinutes), stdin: null,
            stdoutLogPrefix: "[python]", stderrLogPrefix: "[python:err]",
            displayName: $"Python 実行: {scriptPath}", startErrorHint: "Python:UvPath を確認", ct: ct);
        // Python 経路は stdout を捨てる（DB 書戻しが結果）。ExitCode≠0 の fail-fast 判断は呼び手＝ここに残す。
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Python が ExitCode={result.ExitCode} で失敗しました: {scriptPath}\nstderr:\n{result.Stderr}");
    }

    /// <summary>JST タイムゾーンの単一定義（"Asia/Tokyo" リテラルの散在＝市場/TZ 変更時の片直しドリフトを防ぐ）。
    /// "Asia/Tokyo" は .NET6+ が ICU で IANA/Windows ID を相互解決する。ICU を無効化した旧構成では
    /// FindSystemTimeZoneById の TimeZoneNotFoundException が型初期化時の TypeInitializationException に
    /// 包まれて初回利用時に明示失敗する（silent fallback しないため気付ける）。</summary>
    private static readonly TimeZoneInfo Jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    /// <summary>
    /// JST（Asia/Tokyo）の当日を <see cref="TimeProvider"/> から導出する純粋関数（DB/プロセス非依存・
    /// SelfTest で固定可能）。ホストローカル日付に依存せず、非 JST ホスト（VPS/クラウド）でも当日が
    /// JST 基準で一意に決まる。TZ 未解決時の fail-loud 挙動は <see cref="Jst"/> を参照。
    /// </summary>
    internal static DateOnly ResolveTodayJst(TimeProvider timeProvider)
    {
        var nowJst = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, Jst);
        return DateOnly.FromDateTime(nowJst);
    }

    /// <summary>
    /// 直近の営業日（DailyBar 存在日の最新）を解決する。窓 -10 暦日は年末年始/GW（連続休場最大≈6日）を
    /// 十分カバーする。取引日皆無は前提破綻＝fail-fast（共通コア文言＋<paramref name="hint"/> で throw）。
    /// run-today と explain-today の --date 未指定パスで共有し、-10 窓と fail-fast 文言のドリフトを防ぐ。
    /// </summary>
    /// <param name="hint">呼び手固有の復旧誘導（run-today＝広期間 ingest でバックフィル /
    /// explain-today＝run-today 実行が前提。2つの障害は復旧手順が異なるため呼び手が渡す）。</param>
    internal static async Task<DateOnly> ResolveLatestTradingDayAsync(
        AppDbContext db, DateOnly today, string hint, CancellationToken ct = default)
    {
        var tradingDays = await BacktestService.QueryTradingDaysAsync(db, today.AddDays(-10), today, ct);
        if (tradingDays.Count == 0)
            throw new InvalidOperationException(
                "直近10暦日に DailyBar がありません（コールドスタート/DB が10日以上未更新）。" + hint);
        return tradingDays[^1];
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

    /// <summary>
    /// run-today 専用の Python 設定検証。RunAsync の timeout ガードは最終防衛線として残る（paramName=timeout の
    /// 汎用文言でしか語れない）ため、設定境界では config キー名（Python:TimeoutMinutes）つきで診断する。
    /// 上限は <see cref="ProcessRunner.MaxTimeout"/> からの導出（生リテラルの再エンコードはドリフト源のためしない）。
    /// 共有 ValidateIngestConfig には入れない — Python を使わない ingest 単体実行が無関係な誤設定で
    /// 偽 fail-fast する結合を避けるため。
    /// </summary>
    private static void ValidatePythonConfig(PythonOptions py)
    {
        int maxMinutes = (int)ProcessRunner.MaxTimeout.TotalMinutes;
        if (py.TimeoutMinutes <= 0 || py.TimeoutMinutes > maxMinutes)
            throw new InvalidOperationException(
                $"Python:TimeoutMinutes は 1〜{maxMinutes} の範囲で指定してください（現在値: {py.TimeoutMinutes}）。");
    }

    /// <summary>
    /// explain-today 専用の Claude 設定検証（<see cref="ValidatePythonConfig"/> の対）。TimeoutMinutes の下限＋上限を
    /// config キー名（Claude:TimeoutMinutes）つきで事前検証する（下限のみだと上限超過値がループ内 RunAsync の
    /// AOORE→全銘柄スキップ偽装に至るため上限も弾く）。ExecutablePath / Model の非空も確認する（誤設定=ExitCode1。
    /// 空 Model は `--model ""` のまま CLI に渡り毎回非0終了→全銘柄「非致命スキップ」→ExitCode=0 偽装に至る）。
    /// </summary>
    private static void ValidateClaudeConfig(ClaudeOptions cl)
    {
        int maxMinutes = (int)ProcessRunner.MaxTimeout.TotalMinutes;
        if (cl.TimeoutMinutes <= 0 || cl.TimeoutMinutes > maxMinutes)
            throw new InvalidOperationException(
                $"Claude:TimeoutMinutes は 1〜{maxMinutes} の範囲で指定してください（現在値: {cl.TimeoutMinutes}）。");
        if (string.IsNullOrWhiteSpace(cl.ExecutablePath))
            throw new InvalidOperationException("Claude:ExecutablePath が空です（Windows は claude.cmd を指定）。");
        if (string.IsNullOrWhiteSpace(cl.Model))
            throw new InvalidOperationException("Claude:Model が空です（例: claude-opus-4-8）。");
    }

    // --- 引数パース ---
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++) // args[0] はコマンド名
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
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
