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
            // 実効上限は per-attempt timeout（下の AddTimeout）に委ねる。HttpClient 全体の既定100秒だと
            // 複数要求キュー＋レートゲート待機で全体が枯れて TaskCanceled になり再試行されないため無効化。
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        });
        // リトライ（外側）＋ per-attempt timeout（内側）。
        jq.AddResilienceHandler("jq-retry", b =>
        {
            // 標準の既定 ShouldHandle（HttpClientResiliencePredicates.IsTransient）を使用＝
            // 408/429/5xx・HttpRequestException・(下の)TimeoutRejectedException を transient 扱い。
            // 旧・手書き ShouldHandle は TaskCanceled/408 を取りこぼし輻輳中断を再試行できなかったため撤去。
            // ShouldRetryAfterHeader=true（helper 既定）で 429/503 の Retry-After を遵守。待機は retry 層＝
            // AddTimeout の外側で行われるため、Retry-After delta が per-attempt timeout(75s) を超えても
            // タイムアウトしない。レートゲート（JQuantsRateLimiter）は Retry-After を扱わず MinInterval
            // ゲートに専念する。
            b.AddStandardHttpRetry(5);
            // per-attempt timeout（retry の内側＝AddRetry の後に追加）。レートゲート待機
            // （最大 MinIntervalSeconds≈13秒）を内包する必要があるため既定10秒では短すぎる→75秒。
            // タイムアウト時は TimeoutRejectedException を投げ、上の retry が transient として再試行する。
            b.AddTimeout(TimeSpan.FromSeconds(75));
        });
        // レートゲート（最内）。jq-retry handler より後に登録するため retry/timeout の内側になり、
        // 初回・再試行を問わず全物理送信がここを通る（per-attempt timeout がこのゲート待機を内包）。
        jq.AddHttpMessageHandler<JQuantsRateLimitHandler>();

        // 差し替えロガー（Subscription-Key マスク）を DI から解決させるため事前登録。
        services.AddTransient<EdinetRedactingHttpLogger>();

        var edinet = services.AddHttpClient<EdinetClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<EdinetOptions>>().Value;
            // base がパス(/api/v2)を持つため、末尾スラッシュ必須＋相対パスで結合する
            // （末尾 '/' が無いと最後のセグメントが置換され /api/v2 が落ちる）。
            client.BaseAddress = new Uri(EnsureTrailingSlash(opt.BaseUrl));
            client.Timeout = TimeSpan.FromMinutes(2); // CSV(ZIP) 取得に余裕
        });
        // セキュリティ: EDINET は Subscription-Key を URL クエリで渡すため、IHttpClientFactory 既定の
        // リクエスト URI ロガー（Information で全URLを出力）が実鍵を漏らしうる。factory ロガーを除去し、
        // 鍵をマスクする差し替えロガーで HTTP 観測性（要求/応答/失敗）を保ちつつ漏洩経路を断つ。
        // 注: 根治はヘッダ化（クエリから鍵を外す）だが、EDINET v2(Azure APIM) のヘッダ受理名
        //   （Subscription-Key / Ocp-Apim-Subscription-Key）が公式未文書のため実機検証が必要（段階2課題）。
        // wrapHandlersPipeline:false でログハンドラを最内（resilience handler の内側）に置き、
        // 各物理試行（リトライ毎）で URI をマスクして記録する。
        edinet.RemoveAllLoggers();
        edinet.AddLogger<EdinetRedactingHttpLogger>(wrapHandlersPipeline: false);
        edinet.AddResilienceHandler("edinet-retry", b =>
        {
            // jq-retry と同じ標準既定（IsTransient＋Retry-After 遵守）へ寄せる＝408/TaskCanceled の
            // 取りこぼしと手書き ShouldHandle の横展開漏れを解消。per-attempt timeout は足さない：
            // EDINET はレートゲートが無く待機の内包動機が無い一方、CSV(ZIP) は大きく
            // HttpClient.Timeout=2分（全体予算, :73）を意図的に確保済みのため、per-attempt timeout を
            // 入れると正常 DL を切る回帰になる。retry はこの 2 分枠を共有する。
            b.AddStandardHttpRetry(3);
        });

        services.AddSingleton<EdinetCsvParser>();

        return services;
    }

    /// <summary>HTTP retry の標準既定（IsTransient＋Retry-After 遵守＋指数 backoff＋ジッタ）を構築する。
    /// per-attempt timeout やレートゲートは各呼び出し側で別途付与する（ここでは付けない）。</summary>
    private static void AddStandardHttpRetry(
        this ResiliencePipelineBuilder<HttpResponseMessage> b, int maxAttempts, TimeSpan? baseDelay = null)
        => b.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = maxAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = baseDelay ?? TimeSpan.FromSeconds(2),
            ShouldRetryAfterHeader = true,   // 既定 IsTransient ＋ Retry-After 遵守
        });

    private static string EnsureTrailingSlash(string url)
        => url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
}
