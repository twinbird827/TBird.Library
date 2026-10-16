using LanobeReader.Models;
using TBird.Maui.Background;

namespace LanobeReader.Services.Network;

/// <summary>
/// MauiNetworkPolicy + SiteRateLimiter を組み合わせる薄いラッパー。
/// </summary>
public class NetworkPolicyService(INetworkPolicy networkPolicy, SiteRateLimiter siteRateLimiter)
{
    public bool IsOnline => networkPolicy.IsOnline;

    /// <summary>
    /// 指定サイトに対して HTTP GET（文字列）を発行。直列化＋ディレイ＋transient リトライが自動適用される。
    /// SiteType → siteKey 変換は GetApiKey() 経由。
    /// </summary>
    public Task<string> GetStringAsync(SiteType site, string url, CancellationToken ct = default)
        => siteRateLimiter.GetStringAsync(site.GetApiKey(), url, ct);
}
