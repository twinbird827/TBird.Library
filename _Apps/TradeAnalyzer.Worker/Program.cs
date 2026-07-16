using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeAnalyzer.Core;
using TradeAnalyzer.Data;
using TradeAnalyzer.Worker;
using TradeAnalyzer.Worker.Claude;

// 日本語ログ/出力を UTF-8 に固定（Windows 既定 console は cp932＝run-today.ps1 の Tee/リダイレクトで
// 文字化けし監視ログが読めなくなるのを防ぐ）。コンソール非対応環境では best-effort で既定のまま。
try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }

var builder = Host.CreateApplicationBuilder(args);

// APIキーの読み込み元（いずれでも可。後勝ちで Secrets.json が優先）:
//  1. user-secrets（リポジトリ外。dotnet user-secrets set ...）
//  2. _Tools/TradeAnalyzer/Secrets.json（AppPaths.SecretsPath＝絶対パス・CWD 非依存。.gitignore の
//     _Tools/ で追跡除外。_Apps 削除やブランチ切替でも生存する置き場）
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
builder.Configuration.AddJsonFile(AppPaths.SecretsPath, optional: true, reloadOnChange: false);

builder.Services.AddTradeAnalyzerData(builder.Configuration);
builder.Services.AddTradeAnalyzerCore(builder.Configuration);

// Worker 固有の Python 採点設定（run-today / 将来 3b が共有）。Worker は composition root のため
// Core/Data の「Configure は拡張メソッドに集約」規約とは別に Program.cs で直接バインドする。
builder.Services.Configure<PythonOptions>(
    builder.Configuration.GetSection(PythonOptions.SectionName));

// 段階3b の Claude 定性層設定＋経路分岐 DI（explain-today が使う）。Route=cli を既定・SDK は未実装（要件化時に配線）。
builder.Services.Configure<ClaudeOptions>(
    builder.Configuration.GetSection(ClaudeOptions.SectionName));
var claudeRoute = builder.Configuration.GetSection(ClaudeOptions.SectionName)["Route"] ?? "cli";
switch (claudeRoute.ToLowerInvariant())
{
    case "cli":
        builder.Services.AddScoped<IClaudeAnalysisService, ClaudeCliAnalysisService>();
        break;
    case "sdk":
        // 公式 Anthropic SDK 経路は未実装（数値安全性が要件化した時点で ClaudeSdkAnalysisService＋Anthropic NuGet を配線）。
        throw new NotSupportedException(
            "Claude:Route=sdk は未実装です。Route=cli を使用してください（SDK 経路は要件化時に配線）。");
    default:
        throw new InvalidOperationException($"Claude:Route が不正です: {claudeRoute}（cli を指定してください）。");
}

var host = builder.Build();

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cli");

using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

try
{
    switch (command)
    {
        case "migrate":
            await Commands.MigrateAsync(sp);
            break;
        case "ingest":
            await Commands.IngestAsync(sp, args);
            break;
        case "analyze":
            await Commands.AnalyzeAsync(sp, args);
            break;
        case "signals":
            await Commands.SignalsAsync(sp, args);
            break;
        case "backtest":
            await Commands.BacktestAsync(sp, args);
            break;
        case "run-today":
            await Commands.RunTodayAsync(sp, args);
            break;
        case "explain-today":
            await Commands.ExplainTodayAsync(sp, args);
            break;
        case "selftest":
            await SelfTest.RunAsync();
            break;
        case "stats":
            await Commands.StatsAsync(sp, args);
            break;
        default:
            Commands.PrintUsage();
            break;
    }
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "コマンド '{Command}' が失敗しました", command);
    return 1;
}
