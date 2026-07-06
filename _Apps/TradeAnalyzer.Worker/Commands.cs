using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        // 当日は JST 固定で導出する（ホストローカル日付に依存させない＝非 JST ホスト移植でも1日ズレない）。
        // TimeProvider は既存 JQuantsRateLimiter と同じ「DI 未登録時は TimeProvider.System」方針で入手する。
        var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
        var today = ResolveTodayJst(timeProvider);
        // --edinet-limit 0 相当（EDINET CSV 解析を止める。文書一覧 API は日次で残る＝段階2と同じ）。
        await ingest.IngestAsync(today, today, skipJQuants, edinetLimitPerDay: 0);

        // 2. 最新営業日 t の解決＝DailyBar 存在日の最新（当日 EOD が入っていれば今日、未反映なら直近営業日）。
        //    窓 -10 暦日は年末年始/GW（連続休場最大≈6日）を十分カバーする。
        var tradingDays = await BacktestService.QueryTradingDaysAsync(db, today.AddDays(-10), today);
        if (tradingDays.Count == 0)
            throw new InvalidOperationException(
                "直近10暦日に DailyBar がありません（コールドスタート/DB が10日以上未更新）。"
                + "採点には複数年 ingest 済みの履歴が必要です。広期間 ingest でバックフィルしてください。");
        var t = tradingDays[^1];
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
            ("--date", t.ToString("yyyy-MM-dd")),
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

        Console.WriteLine($"=== {t:yyyy-MM-dd} Top-{topN}（MlScore 降順, Passed {passed.Count} 件中）===");
        Console.WriteLine($"{"Code",-8} {"MlScore",10} {"RuleScore",9}  Rationale");
        foreach (var s in top)
            Console.WriteLine($"{s.Code,-8} {s.MlScore!.Value,10:F4} {s.RuleScore,9}  {s.Rationale}");
    }

    /// <summary>
    /// uv run python &lt;script&gt; &lt;args...&gt; を Python:MlDir を作業ディレクトリに起動し、ExitCode≠0 なら
    /// stderr を添えて throw する小ヘルパ。stdout/stderr を逐次ログ、タイムアウト（既定10分）超過で kill＋throw。
    /// double.MinValue 等での黙殺は禁止（段階2 silent fallback 禁止方針を Process 境界にも適用）。
    /// run-today と将来の 3b が共有する Python 起動点。設定は型付き <see cref="PythonOptions"/> から受ける。
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
        int timeoutMinutes = opt.TimeoutMinutes > 0 ? opt.TimeoutMinutes : 10;

        var psi = new ProcessStartInfo
        {
            FileName = uvPath,
            WorkingDirectory = mlDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
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

        var stderr = new StringBuilder();
        // stderr(StringBuilder=非スレッドセーフ) を ErrorDataReceived の append と全読取（timeout/ExitCode 両経路）で保護する。
        // StringBuilder インスタンス自身を lock 対象にする公開ロックを避け、専用オブジェクトを用意する。
        object stderrLock = new();
        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) logger.LogInformation("[python] {Line}", e.Data); };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (stderrLock) { stderr.AppendLine(e.Data); }
            logger.LogWarning("[python:err] {Line}", e.Data);
        };

        logger.LogInformation("Python 起動: {Uv} run python {Script} {Args} (cwd={Cwd})",
            uvPath, scriptPath, string.Join(' ', options.Select(o => $"{o.key} {o.value}")), mlDir);

        // UseShellExecute=false の Process.Start() は実行ファイル不在時 false を返さず Win32Exception を
        // throw する（旧 `if (!proc.Start())` の親切メッセージは dead-code だった）。例外を捕捉し原因明示で包む。
        try
        {
            proc.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Python プロセスを起動できません: {uvPath}（Python:UvPath を確認）。", ex);
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));
        // BeginOutput/ErrorReadLine と CancelOutput/ErrorRead をペア化し購読寿命を明示する。
        // 正常終了経路では下の引数なし WaitForExit() の flush 完了までにハンドラが発火し切る。
        // timeout の bounded-drain false 経路（孫プロセス未終了で引数なし WaitForExit() を意図的にスキップ）では
        // 読取がアクティブなまま本 finally の Cancel で明示停止する。post-EOF/Kill 後でも _output/_error は
        // using Dispose まで非 null ゆえ Cancel は throw せず、伝播中の例外を握り潰さない。
        // ハンドラが参照する logger は DI 管理で本メソッドより寿命が長い。
        try
        {
            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Kill 後、受信済み stderr を診断として例外へ添付する（ExitCode≠0 経路と対称化＝Python トレースバック保持）。
                // 単に {stderr} を読むと Kill 後も発火しうる ErrorDataReceived ハンドラと並行 read になり torn read を招くため、
                // 非同期リーダを bounded に flush してから locked snapshot を読む。drain 失敗でも throw を保証するよう try/catch で囲む。
                // Kill/flush の例外は握り潰さず TimeoutException の innerException に連鎖させる（Win32Exception 等の原因を保持）。
                Exception? killEx = null;
                try
                {
                    proc.Kill(entireProcessTree: true);
                    // WaitForExit(TimeSpan) は非同期 ErrorDataReceived の flush を保証しない（MS docs: WaitForExit(Int32) の
                    // Remarks。true を受けた後に引数なし WaitForExit() を呼べ）。5 秒内に終了済(true)のときだけ続けて引数なし
                    // WaitForExit() で EOF flush を完了させる（終了済みなので即 return＝bounded 意図と両立）。false（孫プロセス
                    // wedge で 5 秒内に未終了）なら無限ブロック回避のため引数なし呼出を避け、受信済み分のみ添付する。
                    if (proc.WaitForExit(TimeSpan.FromSeconds(5)))
                        proc.WaitForExit();
                }
                catch (Exception ex) { killEx = ex; logger.LogWarning(ex, "Python プロセス kill/flush 失敗（既に終了済みの可能性）。"); }
                string tail;
                lock (stderrLock) { tail = stderr.ToString(); }
                throw new TimeoutException(
                    $"Python 実行が {timeoutMinutes} 分でタイムアウトしました: {scriptPath}\nstderr(タイムアウトまで):\n{tail}",
                    killEx);
            }

            // WaitForExitAsync は非同期 stdout/stderr リーダの EOF flush を保証しない（ExitCode≠0 時に throw する
            // stderr 末尾＝Python トレースバック最重要行が欠落しうる）。引数なし同期 WaitForExit() を1回呼び、
            // 非同期バッファを完全 flush してから ExitCode/stderr を参照する（正常終了済みなので即時 return）。
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                // 直前の引数なし WaitForExit() が非同期イベント処理の完了を保証するため並行 writer は不在だが、
                // 読取作法を timeout 経路と統一して locked snapshot で読む。
                string tail;
                lock (stderrLock) { tail = stderr.ToString(); }
                throw new InvalidOperationException(
                    $"Python が ExitCode={proc.ExitCode} で失敗しました: {scriptPath}\nstderr:\n{tail}");
            }
        }
        finally
        {
            // Begin/Cancel をペア化し購読寿命を明示。post-EOF/Kill 後でも _output/_error は using Dispose まで
            // 非 null（BeginOutput/ErrorReadLine は Start 後・proc.Start の Win32Exception catch が Begin 前に return）
            // ゆえ Cancel は throw せず、伝播中の TimeoutException/InvalidOperationException を握り潰さない。
            proc.CancelOutputRead();
            proc.CancelErrorRead();
        }
    }

    /// <summary>
    /// JST（Asia/Tokyo）の当日を <see cref="TimeProvider"/> から導出する純粋関数（DB/プロセス非依存・
    /// SelfTest で固定可能）。ホストローカル日付に依存せず、非 JST ホスト（VPS/クラウド）でも当日が
    /// JST 基準で一意に決まる。"Asia/Tokyo" は .NET6+ が ICU で IANA/Windows ID を相互解決する。
    /// ICU を無効化した旧構成では <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> が
    /// TimeZoneNotFoundException で起動時に明示失敗する（silent fallback しないため気付ける）。
    /// </summary>
    internal static DateOnly ResolveTodayJst(TimeProvider timeProvider)
    {
        var nowJst = TimeZoneInfo.ConvertTimeFromUtc(
            timeProvider.GetUtcNow().UtcDateTime,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo"));
        return DateOnly.FromDateTime(nowJst);
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
