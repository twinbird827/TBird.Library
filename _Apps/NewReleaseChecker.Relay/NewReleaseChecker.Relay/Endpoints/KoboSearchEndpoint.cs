using Microsoft.Extensions.Options;
using NewReleaseChecker.Relay.Options;
using NewReleaseChecker.Relay.Services;

namespace NewReleaseChecker.Relay.Endpoints;

/// <summary>POST /api/kobo/search — 楽天 Kobo 電子書籍検索 API への透過プロキシ。</summary>
public static class KoboSearchEndpoint
{
    public static async Task HandleAsync(
        HttpContext httpContext,
        IRakutenProxy proxy,
        IOptions<RakutenOptions> options)
    {
        var query = await JsonQueryConverter.ReadAsync(httpContext);
        await proxy.ProxyAsync(options.Value.SearchPath, query, httpContext, httpContext.RequestAborted);
    }
}
