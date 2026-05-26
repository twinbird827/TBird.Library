using System.Collections.Concurrent;
using System.Text;
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
    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 500;

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
            catch (Exception ex)
            {
                LogHelper.Warn(nameof(NetworkPolicyService), $"IsOnline check failed: {ex.Message}");
                return false;
            }
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
    /// transient な HttpRequestException（SSL ストリーム破損 / 5xx / 408 / 429 等）は
    /// 最大 2 回リトライする（合計 3 回試行）。試行間は最小 500ms 待機し、さらに
    /// サイト別 request_delay_ms を尊重するため実効的な間隔はそれより長くなることがある。
    /// 4xx クライアントエラー、TaskCanceledException、外部 ct のキャンセル要求はリトライしない。
    /// </summary>
    public async Task<string> GetStringAsync(SiteType site, string url, CancellationToken ct = default)
    {
        var gate = _siteGates[site];
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                await EnforceDelayAsync(site, ct).ConfigureAwait(false);
                try
                {
                    var result = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
                    _lastRequestAt[site] = DateTime.UtcNow;
                    return result;
                }
                catch (HttpRequestException ex) when (attempt < MaxAttempts && IsTransient(ex) && !ct.IsCancellationRequested)
                {
                    // リトライ予定の失敗も「サーバへ実際に投げた」扱いにし、次の試行まで
                    // request_delay_ms を尊重する（礼儀+連続失敗時の負荷抑制）。
                    _lastRequestAt[site] = DateTime.UtcNow;
                    LogHelper.Warn(nameof(NetworkPolicyService),
                        $"Transient failure [{site}] {url} (attempt {attempt}/{MaxAttempts}): {ex.Message}");
                    await Task.Delay(RetryDelayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // ユーザ操作等によるキャンセルは異常ではないのでスタック付き ERROR を出さない。
                    throw;
                }
                catch (Exception ex)
                {
                    _lastRequestAt[site] = DateTime.UtcNow;
                    LogRequestFailure(site, url, ex);
                    throw;
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    // 4xx クライアントエラーは恒久的なので再試行不要。
    // ステータスなし（DNS/SSL/ソケット層の失敗）と 5xx/408/429 は transient とみなす。
    private static bool IsTransient(HttpRequestException ex)
    {
        if (ex.StatusCode is { } code)
        {
            var n = (int)code;
            return n >= 500 || n == 408 || n == 429;
        }
        return true;
    }

    // HttpRequestException.Message が "Connection failure" 等の抽象的な文字列だけだと
    // Android 側の真の原因 (UnknownHostException / SSLHandshakeException / EOFException 等) が見えない。
    // InnerException チェーンを最大 5 段まで logcat に吐いて切り分けを可能にする。
    private static void LogRequestFailure(SiteType site, string url, Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append("Request failed [").Append(site).Append("] ").AppendLine(url);
        var cur = ex;
        int depth = 0;
        while (cur is not null && depth < 5)
        {
            sb.Append("  [").Append(depth).Append("] ")
              .Append(cur.GetType().FullName).Append(": ").AppendLine(cur.Message);
            cur = cur.InnerException;
            depth++;
        }
        var st = ex.StackTrace;
        if (!string.IsNullOrEmpty(st))
        {
            sb.AppendLine("  Stack (top 3):");
            var lines = st.Split('\n');
            for (int i = 0; i < Math.Min(3, lines.Length); i++)
            {
                sb.Append("    ").AppendLine(lines[i].TrimEnd());
            }
        }
        LogHelper.Error(nameof(NetworkPolicyService), sb.ToString());
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
        var v = await _settingsRepo.GetIntValueAsync(SettingsKeys.REQUEST_DELAY_MS, SettingsKeys.DEFAULT_REQUEST_DELAY_MS).ConfigureAwait(false);
        return Math.Clamp(v, SettingsKeys.MIN_REQUEST_DELAY_MS, SettingsKeys.MAX_REQUEST_DELAY_MS);
    }
}
