// ImageGrabber: 指定URLを開き、ブラウザが受信した image/* 応答をすべて保存する

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;
using SkiaSharp;
using TBird.Core;

Console.OutputEncoding = Encoding.UTF8;

string? url = null, outDir = null, profileDir = null;
bool headless = false;
int maxScrolls = 200, waitMs = 600, minBytes = 0, minSide = 200, timeoutSec = 60;

try
{
    for (int i = 0; i < args.Length; i++)
    {
        string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} に値がありません");
        switch (args[i])
        {
            case "-o": case "--out": outDir = Next(); break;
            case "--headless": headless = true; break;
            case "--profile": profileDir = Next(); break;
            case "--max-scrolls": maxScrolls = int.Parse(Next()); break;
            case "--wait": waitMs = int.Parse(Next()); break;
            case "--min-bytes": minBytes = int.Parse(Next()); break;
            case "--min-side": minSide = int.Parse(Next()); break;
            case "--timeout": timeoutSec = int.Parse(Next()); break;
            case "-h": case "--help": return Usage();
            default:
                if (args[i].StartsWith('-')) throw new ArgumentException($"不明なオプション: {args[i]}");
                url = args[i];
                break;
        }
    }
}
catch (Exception ex) when (ex is ArgumentException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    return Usage();
}

if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
{
    Console.Error.WriteLine("http/https の絶対URLを指定してください。");
    return Usage();
}

outDir ??= Path.Combine(Directories.DownloadDirectory, $"{DateTime.Now:yyyyMMdd_HHmmss}");
outDir = Directory.CreateDirectory(outDir).FullName;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    if (cts.IsCancellationRequested) return;   // 2回目は強制終了
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n中断要求を受けました。受信済み分を保存して終了します...");
};

var stats = new Stats();
var seenUrl = new ConcurrentDictionary<string, byte>();
var seenHash = new ConcurrentDictionary<string, byte>();
var pending = new ConcurrentBag<Task>();
var log = new ConcurrentQueue<string>();

async Task SaveAsync(IResponse res)
{
    try
    {
        if (!res.Headers.TryGetValue("content-type", out var ct)) return;
        ct = ct.Split(';')[0].Trim().ToLowerInvariant();
        if (!ct.StartsWith("image/")) return;
        if (res.Status is < 200 or >= 300) return;     // 3xx/304等はボディが無い
        if (!seenUrl.TryAdd(res.Url, 0)) return;

        var body = await res.BodyAsync();
        if (body.Length < minBytes) { Interlocked.Increment(ref stats.Small); return; }
        if (minSide > 0 && !IsLargeEnough(body, minSide)) { Interlocked.Increment(ref stats.Small); return; }

        var hash = Convert.ToHexString(SHA256.HashData(body));
        if (!seenHash.TryAdd(hash, 0)) { Interlocked.Increment(ref stats.Duplicate); return; }

        var no = Interlocked.Increment(ref stats.Saved);
        var name = $"{no:D4}_{SafeBaseName(res.Url)}{Extension(ct, res.Url)}";
        await File.WriteAllBytesAsync(Path.Combine(outDir, name), body);
        log.Enqueue($"{name}\t{body.Length}\t{ct}\t{res.Url}");
        Console.WriteLine($"[{no,4}] {body.Length,9:N0} B  {Truncate(res.Url, 100)}");
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref stats.Failed);
        Console.Error.WriteLine($"失敗: {Truncate(res.Url, 100)} ({ex.GetType().Name}: {ex.Message.Split('\n')[0]})");
    }
}

using var pw = await Playwright.CreateAsync();
IBrowser? browser = null;
IBrowserContext context;

try
{
    context = await LaunchWithAutoInstall(async () =>
    {
        if (profileDir is not null)
        {
            return await pw.Chromium.LaunchPersistentContextAsync(Path.GetFullPath(profileDir), new()
            {
                Headless = headless,
                ViewportSize = new() { Width = 1280, Height = 900 },
            });
        }
        browser = await pw.Chromium.LaunchAsync(new() { Headless = headless });
        return await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
    });
}
catch (PlaywrightException ex)
{
    Console.Error.WriteLine($"ブラウザ起動失敗: {ex.Message.Split('\n')[0]}");
    return 2;
}

// ページ単位でなくコンテキスト単位で拾う（iframe・ポップアップも対象）
context.Response += (_, res) => pending.Add(SaveAsync(res));

var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

Console.WriteLine($"URL : {uri}");
Console.WriteLine($"保存先: {outDir}\n");

try
{
    await page.GotoAsync(uri.ToString(), new() { WaitUntil = WaitUntilState.Load, Timeout = timeoutSec * 1000 });
}
catch (PlaywrightException ex)
{
    // タイムアウトでも、それまでに受信した分は保存する
    Console.Error.WriteLine($"ページ読込で例外（続行）: {ex.Message.Split('\n')[0]}");
}
await WaitNetworkIdle(page, 10_000);

// lazy-load や無限スクロールで遅れて読み込まれる画像を拾うため、最下部に達するまでスクロールする
await page.Mouse.MoveAsync(640, 450);
double lastHeight = -1;
int stableAtBottom = 0;
for (int i = 0; i < maxScrolls && !cts.IsCancellationRequested; i++)
{
    try
    {
        // 内側スクロールコンテナ向けにホイール、通常ページ向けに scrollBy の両方を行う
        await page.Mouse.WheelAsync(0, 900);
        await page.EvaluateAsync("() => window.scrollBy(0, window.innerHeight * 0.9)");
        await page.WaitForTimeoutAsync(waitMs);

        var m = await page.EvaluateAsync<double[]>(
            "() => [document.documentElement.scrollHeight, window.scrollY + window.innerHeight]");
        bool atBottom = m[1] >= m[0] - 2;
        stableAtBottom = (atBottom && m[0] == lastHeight) ? stableAtBottom + 1 : 0;
        lastHeight = m[0];
        if (stableAtBottom >= 3) break;   // 最下部で3回連続して高さが変わらなければ終了
    }
    catch (PlaywrightException ex)
    {
        // スクロール中に遷移した等
        Console.Error.WriteLine($"スクロール中断: {ex.Message.Split('\n')[0]}");
        break;
    }
}

await WaitNetworkIdle(page, 10_000);

// SaveAsync 実行中に追加された応答も含めて全て完了を待つ
while (true)
{
    var snapshot = pending.ToArray();
    await Task.WhenAll(snapshot);
    if (pending.Count == snapshot.Length) break;
}

await context.CloseAsync();
if (browser is not null) await browser.CloseAsync();

await File.WriteAllLinesAsync(Path.Combine(outDir, "_urls.tsv"),
    log.OrderBy(l => l, StringComparer.Ordinal).Prepend("file\tbytes\tcontent-type\turl"));

Console.WriteLine();
Console.WriteLine($"保存 {stats.Saved} / 内容重複 {stats.Duplicate} / 小さい・寸法不明 {stats.Small} / 失敗 {stats.Failed}");
Console.WriteLine($"一覧: {Path.Combine(outDir, "_urls.tsv")}");
return 0;


static int Usage()
{
    Console.Error.WriteLine("""
        使い方: ImageGrabber <url> [options]
          -o, --out <dir>      保存先（既定 <ダウンロード>\yyyyMMdd_HHmmss）
          --headless           ブラウザ非表示
          --profile <dir>      永続プロファイル（ログインが必要なサイト用）
          --max-scrolls <n>    スクロール上限（既定 200）
          --wait <ms>          スクロール毎の待機（既定 600）
          --min-bytes <n>      これ未満のサイズは保存しない（既定 0）
          --min-side <px>      短辺がこれ未満の画像と寸法を読めない形式（SVG・AVIF 等）は保存しない（既定 200、0 で無効）
          --timeout <sec>      初回読込タイムアウト（既定 60）
        Ctrl+C でスクロールを打ち切り、受信済みの画像を保存して終了します。
        """);
    return 1;
}

// SKCodec はヘッダだけ読むので全体をデコードしない。読めない形式は null になる
static bool IsLargeEnough(byte[] body, int minSide)
{
    using var codec = SKCodec.Create(new MemoryStream(body));
    return codec is not null && Math.Min(codec.Info.Width, codec.Info.Height) >= minSide;
}

static async Task<IBrowserContext> LaunchWithAutoInstall(Func<Task<IBrowserContext>> launch)
{
    try
    {
        return await launch();
    }
    catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
    {
        Console.WriteLine("Chromium が未インストールのため、インストールします...");
        var code = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (code != 0) throw;
        return await launch();
    }
}

static async Task WaitNetworkIdle(IPage page, int timeoutMs)
{
    // ポーリングし続けるサイトでは networkidle に到達しないため、タイムアウトは無視する
    try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = timeoutMs }); }
    catch (PlaywrightException) { }
}

static string SafeBaseName(string url)
{
    string name = "img";
    if (Uri.TryCreate(url, UriKind.Absolute, out var u))
    {
        var seg = Uri.UnescapeDataString(u.Segments.LastOrDefault() ?? "").Trim('/');
        var stem = Path.GetFileNameWithoutExtension(seg);
        if (!string.IsNullOrWhiteSpace(stem)) name = stem;
    }
    var invalid = Path.GetInvalidFileNameChars();
    var sb = new StringBuilder(name.Length);
    foreach (var c in name) sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
    var s = sb.ToString();
    return s.Length > 60 ? s[..60] : s;
}

static string Extension(string contentType, string url) => contentType switch
{
    "image/jpeg" or "image/jpg" or "image/pjpeg" => ".jpg",
    "image/png" => ".png",
    "image/webp" => ".webp",
    "image/gif" => ".gif",
    "image/avif" => ".avif",
    "image/svg+xml" => ".svg",
    "image/bmp" => ".bmp",
    "image/x-icon" or "image/vnd.microsoft.icon" => ".ico",
    "image/heic" => ".heic",
    "image/tiff" => ".tif",
    _ => Uri.TryCreate(url, UriKind.Absolute, out var u) && Path.GetExtension(u.AbsolutePath) is { Length: > 1 and <= 6 } e
            ? e.ToLowerInvariant()
            : ".bin",
};

static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

sealed class Stats
{
    public int Saved, Duplicate, Small, Failed;
}
