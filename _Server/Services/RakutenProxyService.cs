using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using NewReleaseChecker.Relay.Options;

namespace NewReleaseChecker.Relay.Services;

/// <summary>
/// 透過プロキシ本体。クライアント由来のクエリにサーバー保持の楽天認証情報を上書き付与し、
/// 上流ヘッダを添えて楽天 API へ GET 転送、ステータス・Content-Type・本文をそのまま透過する。
/// </summary>
public sealed class RakutenProxyService : IRakutenProxy
{
    /// <summary>クライアントが送ってきても無視し、必ずサーバー保持値で上書きする認証系キー。</summary>
    private static readonly string[] ReservedKeys = { "applicationId", "accessKey", "affiliateId" };

    private readonly HttpClient _httpClient;
    private readonly RakutenOptions _options;
    private readonly ILogger<RakutenProxyService> _logger;

    public RakutenProxyService(
        HttpClient httpClient,
        IOptions<RakutenOptions> options,
        ILogger<RakutenProxyService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProxyAsync(
        string upstreamPath,
        IDictionary<string, string?> queryFromClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var url = BuildUpstreamUrl(upstreamPath, queryFromClient);

        // クライアント切断（伝播）と上流タイムアウト（504）を区別するため、RequestAborted にリンクした
        // トークンへ 15 秒の CancelAfter を合成する（HttpClient.Timeout は使わない。区別できなくなるため）。
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted, ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.UpstreamTimeoutSeconds));
        var linkedToken = timeoutCts.Token;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            UpstreamHeaderBuilder.Apply(request, _options);

            using var upstream = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, linkedToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Proxy {Path} -> {Status} ({ElapsedMs}ms)",
                upstreamPath, (int)upstream.StatusCode, stopwatch.ElapsedMilliseconds);

            // 楽天 API が 4xx/5xx を返してもそのまま透過する（500 でラップしない）。
            await WriteUpstreamResponseAsync(upstream, httpContext, stopwatch.ElapsedMilliseconds, linkedToken);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // クライアント切断: ASP.NET Core に処理させる（レスポンスを書かない）。
            throw;
        }
        catch (OperationCanceledException)
        {
            // こちらの 15 秒 CancelAfter が発火 = 上流タイムアウト。
            _logger.LogError("Upstream timeout for {Path} after {ElapsedMs}ms", upstreamPath, stopwatch.ElapsedMilliseconds);
            await WriteErrorAsync(httpContext, StatusCodes.Status504GatewayTimeout, "upstream_timeout");
        }
        catch (HttpRequestException ex)
        {
            // DNS / TLS / 接続断 等。
            _logger.LogError(ex, "Upstream unreachable for {Path}", upstreamPath);
            await WriteErrorAsync(httpContext, StatusCodes.Status502BadGateway, "upstream_unreachable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error proxying {Path}", upstreamPath);
            await WriteErrorAsync(httpContext, StatusCodes.Status500InternalServerError, "internal");
        }
    }

    /// <summary>
    /// 上流 GET URL を組み立てる。認証系キーはクライアント値を無視し、サーバー保持値で必ず上書き付与する。
    /// 各値は個別に URL エンコード（sort の "+releaseDate" を "%2BreleaseDate" にし、上流での '+'→空白変換を防ぐ）。
    /// </summary>
    private string BuildUpstreamUrl(string upstreamPath, IDictionary<string, string?> queryFromClient)
    {
        var sb = new StringBuilder();
        sb.Append(_options.UpstreamBaseUrl.TrimEnd('/'));
        sb.Append(upstreamPath);

        var first = true;
        void Append(string key, string value)
        {
            sb.Append(first ? '?' : '&');
            first = false;
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value)); // 値部分のみエンコード
        }

        foreach (var kv in queryFromClient)
        {
            if (ReservedKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase)) continue; // 認証注入を防ぐ
            if (kv.Value is null) continue;
            Append(kv.Key, kv.Value);
        }

        // サーバー保持の認証情報を必ず付与（両方必須）。
        Append("applicationId", _options.ApplicationId);
        Append("accessKey", _options.AccessKey);

        return sb.ToString();
    }

    /// <summary>
    /// 上流レスポンスを許可リスト方式で透過する。Content-Type / （無効化していない）Content-Encoding /
    /// Retry-After のみ明示転送し、Set-Cookie / Transfer-Encoding / Connection / Server / Date 等は転送しない。
    /// </summary>
    private static async Task WriteUpstreamResponseAsync(
        HttpResponseMessage upstream, HttpContext httpContext, long elapsedMs, CancellationToken ct)
    {
        var response = httpContext.Response;
        response.StatusCode = (int)upstream.StatusCode;

        if (upstream.Content.Headers.ContentType is { } contentType)
        {
            response.ContentType = contentType.ToString();
        }
        if (upstream.Content.Headers.ContentEncoding.Count > 0)
        {
            // 自動展開は無効なので、上流の圧縮本文と Content-Encoding をセットで透過する。
            response.Headers.ContentEncoding = string.Join(", ", upstream.Content.Headers.ContentEncoding);
        }
        if (upstream.Headers.RetryAfter is { } retryAfter)
        {
            // 楽天が 429（上限超過）で返す Retry-After を透過し、クライアントがスロットル窓を尊重できるようにする。
            response.Headers.RetryAfter = retryAfter.ToString();
        }

        // デバッグ用の中継処理時間（任意ヘッダ）。Content-Length は付与せず ASP.NET Core / IIS に再計算させる。
        response.Headers["X-Relay-Elapsed-Ms"] = elapsedMs.ToString();

        await upstream.Content.CopyToAsync(response.Body, ct);
    }

    private static Task WriteErrorAsync(HttpContext httpContext, int statusCode, string error)
    {
        if (httpContext.Response.HasStarted)
        {
            return Task.CompletedTask; // 本文書き込み中に失敗した場合は上書きできない
        }
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(new { error });
    }
}
