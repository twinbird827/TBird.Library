using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using NewReleaseChecker.Relay.Endpoints;
using NewReleaseChecker.Relay.Middleware;
using NewReleaseChecker.Relay.Options;
using NewReleaseChecker.Relay.Services;

var builder = WebApplication.CreateBuilder(args);

// 設定読み込み: Secrets を必須にして配置漏れを起動時に検出する（サイレントに壊れた状態で動かさない）。
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Secrets.json", optional: false, reloadOnChange: false);

// 設定（IOptions パターン）。
builder.Services.Configure<RakutenOptions>(builder.Configuration.GetSection(RakutenOptions.SectionName));

// 共有シークレットは非空を起動時に強制する（appsettings.Secrets.json の節が空/欠落のまま起動して
// 空ヘッダ認証突破が起きるのを防ぐ。存在チェックだけでなく値の非空も検証）。
builder.Services
    .AddOptions<RelayAuthOptions>()
    .Bind(builder.Configuration.GetSection(RelayAuthOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SharedSecret),
        "RelayAuth:SharedSecret が未設定です。appsettings.Secrets.json に共有シークレットを設定してください。")
    .ValidateOnStart();

// リバースプロキシ（IIS ARR / nginx 等）が前段にいる場合、X-Forwarded-For から実クライアント IP を
// 復元する。既定では loopback のみ信頼（同一ホスト上のプロキシは追加設定不要、外部からの X-Forwarded-For
// 偽装は無視）。loopback 以外のプロキシは appsettings の "ForwardedHeaders:KnownProxies"（IP の配列）で
// 明示的に信頼する。X-Forwarded-For を復元しないと FixedWindow が単一バケツに潰れて DoS/総当たり抑制が無効化する。
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var ip in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(ip, out var addr)) options.KnownProxies.Add(addr);
    }
});

var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
    ?? new RateLimitOptions();

// 透過プロキシは型付き HttpClient 1 本で登録（既定 Transient）。AddSingleton は併用しない（DNS 滞留回避）。
builder.Services.AddHttpClient<IRakutenProxy, RakutenProxyService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // 本文を無加工で透過するため自動展開は無効のまま（上流の Content-Encoding をそのまま転送する）。
        AutomaticDecompression = DecompressionMethods.None,
    });

// 受信側レート制限（クライアントIP単位の FixedWindow）。認証より前に評価する（暴発・総当たりを入口で抑える）。
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    });
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter =
            rateLimitOptions.WindowSeconds.ToString(CultureInfo.InvariantCulture);
        if (!context.HttpContext.Response.HasStarted)
        {
            await context.HttpContext.Response.WriteAsJsonAsync(new { error = "rate_limited" }, ct);
        }
    };
});

var app = builder.Build();

// ミドルウェア順: 転送ヘッダ復元 → ルーティング → レート制限 → 認証 → エンドポイント。
// ForwardedHeaders はレート制限が RemoteIpAddress を参照する前に実クライアント IP を復元する必要があるため最前段に置く。
app.UseForwardedHeaders();
// /healthz の DisableRateLimiting を効かせるため UseRateLimiter の前に UseRouting でエンドポイントを解決する。
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<SharedSecretAuthMiddleware>();

app.MapGet("/healthz", HealthEndpoint.Handle).DisableRateLimiting();
app.MapPost("/api/kobo/search", KoboSearchEndpoint.HandleAsync);
app.MapPost("/api/kobo/genres", KoboGenresEndpoint.HandleAsync);

app.Run();
