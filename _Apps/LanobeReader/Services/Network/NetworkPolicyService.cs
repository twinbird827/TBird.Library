using LanobeReader.Models;
using TBird.Maui.Background;

namespace LanobeReader.Services.Network;

// [DI-LIFETIME: SINGLETON]
// AddTransient/AddScoped causes WifiConnected handler leak because
// INetworkPolicy is Singleton and accumulates re-publish handlers on each construction.
/// <summary>
/// MauiNetworkPolicy + SiteRateLimiter を組み合わせる薄いラッパー。
///
/// 公開シグネチャ (<see cref="GetStringAsync"/> / <see cref="IsOnline"/> /
/// <see cref="IsWifiConnected"/> / WifiConnected / WifiDisconnected) は現行と完全互換のため、
/// NarouApiService / KakuyomuApiService / BackgroundJobQueue の呼出側は無修正で済む。
///
/// 内部実装の変更:
/// - HttpClient + サイト別 SemaphoreSlim / Dictionary / リトライロジック → SiteRateLimiter へ委譲
/// - Connectivity.ConnectivityChanged 購読 → MauiNetworkPolicy (INetworkPolicy) へ委譲
/// - WifiConnected / WifiDisconnected は INetworkPolicy のイベントを再公開（消費者ゼロ化、
///   ただし [Obsolete] でビルド警告として明示）
/// </summary>
public class NetworkPolicyService
{
    private readonly INetworkPolicy _networkPolicy;
    private readonly SiteRateLimiter _siteRateLimiter;

    public NetworkPolicyService(INetworkPolicy networkPolicy, SiteRateLimiter siteRateLimiter)
    {
        _networkPolicy = networkPolicy;
        _siteRateLimiter = siteRateLimiter;

        // INetworkPolicy のイベントを再公開（外部消費者は現状ゼロだが、将来の ad-hoc 購読需要への保険）
#pragma warning disable CS0618
        _networkPolicy.WifiConnected += (s, e) => WifiConnected?.Invoke(s, e);
        _networkPolicy.WifiDisconnected += (s, e) => WifiDisconnected?.Invoke(s, e);
#pragma warning restore CS0618
    }

    [Obsolete("No active consumers. Subscribed by PriorityJobQueue via INetworkPolicy directly. Kept for future ad-hoc app-layer subscription needs.", error: false)]
    public event EventHandler? WifiConnected;

    [Obsolete("No active consumers. Subscribed by PriorityJobQueue via INetworkPolicy directly. Kept for future ad-hoc app-layer subscription needs.", error: false)]
    public event EventHandler? WifiDisconnected;

    public bool IsOnline => _networkPolicy.IsOnline;
    public bool IsWifiConnected => _networkPolicy.IsWifiConnected;

    /// <summary>
    /// 指定サイトに対して HTTP GET（文字列）を発行。直列化＋ディレイ＋transient リトライが自動適用される。
    /// 公開シグネチャは現行と完全一致。SiteType → siteKey 変換は GetApiKey() 経由。
    /// </summary>
    public Task<string> GetStringAsync(SiteType site, string url, CancellationToken ct = default)
        => _siteRateLimiter.GetStringAsync(site.GetApiKey(), url, ct);
}
