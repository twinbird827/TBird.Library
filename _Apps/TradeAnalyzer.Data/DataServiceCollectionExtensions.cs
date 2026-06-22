using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using TradeAnalyzer.Data.External.Edinet;
using TradeAnalyzer.Data.External.JQuants;
using TradeAnalyzer.Data.Options;

namespace TradeAnalyzer.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>永続層・外部APIクライアントを DI 登録する。</summary>
    public static IServiceCollection AddTradeAnalyzerData(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("TradeDb") ?? "Data Source=trade.db";
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(conn));

        services.Configure<JQuantsOptions>(config.GetSection(JQuantsOptions.SectionName));
        services.Configure<EdinetOptions>(config.GetSection(EdinetOptions.SectionName));

        // レート制御状態は跨いで共有する必要があるため singleton。
        services.AddSingleton<JQuantsRateLimiter>();
        services.AddTransient<JQuantsRateLimitHandler>();

        var jq = services.AddHttpClient<JQuantsClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<JQuantsOptions>>().Value;
            client.BaseAddress = new Uri(opt.BaseUrl);
            if (!string.IsNullOrWhiteSpace(opt.ApiKey))
                client.DefaultRequestHeaders.Add("x-api-key", opt.ApiKey);
        });
        // リトライ（外側）。タイムアウト戦略は付けない＝レートゲートの待機が打ち切られないように。
        jq.AddResilienceHandler("jq-retry", b =>
        {
            b.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500),
            });
        });
        // レートゲート（内側）。初回・再試行を問わず全物理送信がここを通る。
        jq.AddHttpMessageHandler<JQuantsRateLimitHandler>();

        var edinet = services.AddHttpClient<EdinetClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<EdinetOptions>>().Value;
            // base がパス(/api/v2)を持つため、末尾スラッシュ必須＋相対パスで結合する
            // （末尾 '/' が無いと最後のセグメントが置換され /api/v2 が落ちる）。
            client.BaseAddress = new Uri(EnsureTrailingSlash(opt.BaseUrl));
            client.Timeout = TimeSpan.FromMinutes(2); // CSV(ZIP) 取得に余裕
        });
        edinet.AddResilienceHandler("edinet-retry", b =>
        {
            b.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500),
            });
        });

        services.AddSingleton<EdinetCsvParser>();

        return services;
    }

    private static string EnsureTrailingSlash(string url)
        => url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
}
