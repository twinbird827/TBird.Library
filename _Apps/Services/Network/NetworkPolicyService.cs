using System.Collections.Concurrent;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Database;
using Microsoft.Maui.Networking;

namespace LanobeReader.Services.Network;

/// <summary>
/// サイト別の HTTP リクエスト発行を直列化＋ディレイ＋Wi-Fi検出でゲートする共通サービス。
/// - 同一サイトへのリクエストは SemaphoreSlim(1,1) で直列化
/// - リクエスト間は request_delay_ms（既定800ms）のディレイを挿入
/// - Wi-Fi接続状態の取得と変化通知も提供（Prefetch用途）
/// </summary>
public class NetworkPolicyService
{
    private readonly HttpClient _httpClient;
    private readonly AppSettingsRepository _settingsRepo;

    private readonly Dictionary<SiteType, SemaphoreSlim> _siteGates = new()
    {
        [SiteType.Narou] = new SemaphoreSlim(1, 1),
        [SiteType.Kakuyomu] = new SemaphoreSlim(1, 1),
    };

    private readonly ConcurrentDictionary<SiteType, DateTime> _lastRequestAt = new()
    {
        [SiteType.Narou] = DateTime.MinValue,
        [SiteType.Kakuyomu] = DateTime.MinValue,
    };

    public NetworkPolicyService(HttpClient httpClient, AppSettingsRepository settingsRepo)
    {
        _httpClient = httpClient;
        _settingsRepo = settingsRepo;

        try
        {
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }
        catch (Exception ex)
        {
            LogHelper.Warn(nameof(NetworkPolicyService), $"Failed to hook ConnectivityChanged: {ex.Message}");
        }
    }

    public event EventHandler? WifiConnected;
    public event EventHandler? WifiDisconnected;

    public bool IsOnline
    {
        get
        {
            try { return Connectivity.Current.NetworkAccess == NetworkAccess.Internet; }
            catch { return true; }
        }
    }

    public bool IsWifiConnected
    {
        get
        {
            try
            {
                return Connectivity.Current.NetworkAccess == NetworkAccess.Internet
                    && Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
            }
            catch { return false; }
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var isWifi = e.NetworkAccess == NetworkAccess.Internet
            && e.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
        if (isWifi) WifiConnected?.Invoke(this, EventArgs.Empty);
        else WifiDisconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 指定サイトに対して HTTP GET（文字列）を発行。直列化＋ディレイが自動で適用される。
    /// </summary>
    public async Task<string> GetStringAsync(SiteType site, string url, CancellationToken ct = default)
    {
        var gate = _siteGates[site];
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnforceDelayAsync(site, ct).ConfigureAwait(false);
            var result = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
            _lastRequestAt[site] = DateTime.UtcNow;
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnforceDelayAsync(SiteType site, CancellationToken ct)
    {
        var delayMs = await GetDelayMsAsync().ConfigureAwait(false);
        var last = _lastRequestAt[site];
        if (last == DateTime.MinValue) return;

        var elapsed = (DateTime.UtcNow - last).TotalMilliseconds;
        var remaining = delayMs - elapsed;
        if (remaining > 0)
        {
            await Task.Delay((int)remaining, ct).ConfigureAwait(false);
        }
    }

    private async Task<int> GetDelayMsAsync()
    {
        try
        {
            var v = await _settingsRepo.GetIntValueAsync(SettingsKeys.REQUEST_DELAY_MS, 800).ConfigureAwait(false);
            return Math.Clamp(v, 100, 5000);
        }
        catch { return 800; }
    }
}
