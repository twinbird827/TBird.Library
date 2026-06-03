using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NewReleaseChecker.Relay.Options;

namespace NewReleaseChecker.Relay.Middleware;

/// <summary>
/// 全 /api/* リクエストで X-Relay-Auth ヘッダを共有シークレットと定数時間比較する認証ミドルウェア。
/// /healthz は素通し。失敗時は 401 を返し、送信値はログに残さない（IP・パスのみ記録）。
/// </summary>
public sealed class SharedSecretAuthMiddleware
{
    private const string HeaderName = "X-Relay-Auth";

    private readonly RequestDelegate _next;
    private readonly RelayAuthOptions _options;
    private readonly ILogger<SharedSecretAuthMiddleware> _logger;

    public SharedSecretAuthMiddleware(
        RequestDelegate next,
        IOptions<RelayAuthOptions> options,
        ILogger<SharedSecretAuthMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // /healthz は認証不要（死活監視用）。
        if (context.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !IsValid(provided.ToString()))
        {
            _logger.LogWarning(
                "Unauthorized request to {Path} from {RemoteIp}",
                context.Request.Path, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        await _next(context);
    }

    private bool IsValid(string headerValue)
    {
        // 共有シークレット未設定（空）の場合は常に拒否する。空のまま起動すると length 0==0 で
        // 空ヘッダが FixedTimeEquals([],[])=true を通過してしまうため、ここで明示的に弾く（多重防御）。
        // 本来は起動時の ValidateOnStart で停止するが、設定漏れでも認証突破を起こさない保険。
        if (string.IsNullOrEmpty(_options.SharedSecret)) return false;

        var headerBytes = Encoding.UTF8.GetBytes(headerValue);
        var secretBytes = Encoding.UTF8.GetBytes(_options.SharedSecret);

        // 長さが異なる場合 FixedTimeEquals は使えないので先に弾く（長さ自体は秘匿情報ではない）。
        return headerBytes.Length == secretBytes.Length &&
               CryptographicOperations.FixedTimeEquals(headerBytes, secretBytes);
    }
}
