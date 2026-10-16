using Microsoft.Extensions.Options;
using NewReleaseChecker.Relay.Options;
using NewReleaseChecker.Relay.Services;

namespace NewReleaseChecker.Relay.Endpoints;

/// <summary>POST /api/kobo/genres — 楽天 Kobo ジャンル検索 API への透過プロキシ。</summary>
public static class KoboGenresEndpoint
{
    public static async Task HandleAsync(
        HttpContext httpContext,
        IRakutenProxy proxy,
        IOptions<RakutenOptions> options)
    {
        var query = await JsonQueryConverter.ReadAsync(httpContext);
        await proxy.ProxyAsync(options.Value.GenrePath, query, httpContext, httpContext.RequestAborted);
    }
}
