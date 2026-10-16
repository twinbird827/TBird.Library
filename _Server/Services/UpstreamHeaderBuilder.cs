using NewReleaseChecker.Relay.Options;

namespace NewReleaseChecker.Relay.Services;

/// <summary>
/// 楽天 API への送信リクエストに、認証通過の核心となるヘッダ（Referer / Origin / User-Agent / Accept）を付与する。
/// 「許可されたWebサイト」に登録したドメインと Referer/Origin のドメインが一致することが認証通過の絶対条件。
/// </summary>
internal static class UpstreamHeaderBuilder
{
    private const string UserAgent = "NewReleaseChecker-Relay/1.0";

    public static void Apply(HttpRequestMessage request, RakutenOptions options)
    {
        var origin = options.OriginDomain.TrimEnd('/');

        // Referer は末尾スラッシュあり、Origin は末尾スラッシュなし（§3.2.3）
        request.Headers.Referrer = new Uri(origin + "/");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd("application/json");
    }
}
