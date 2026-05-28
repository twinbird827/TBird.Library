using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Maui.Web;

namespace TBird.Maui.Background;

/// <summary>
/// サイト別の HTTP GET 発行を直列化＋ディレイ＋transient リトライでゲートする共通サービス。
///
/// - 同一 siteKey へのリクエストは <see cref="SemaphoreSlim"/>(1,1) で直列化
/// - リクエスト間は <c>getDelayMs</c> delegate で取得するディレイを挿入
///   （リトライ予定の失敗も「サーバへ実際に投げた」扱いにし、次の試行まで同ディレイを尊重）
/// - transient な <see cref="HttpRequestException"/> (5xx / 408 / 429 / ステータスなし) は
///   最大 <c>maxAttempts</c> 回試行
/// - 4xx クライアントエラー、TaskCanceledException、ct のキャンセル要求はリトライしない
///
/// 内部の <c>_siteGates</c> / <c>_lastRequestAt</c> はコンストラクタ受領の siteKeys 全要素で
/// 事前初期化。未登録キーは <see cref="ArgumentException"/> で fail-fast（KeyNotFoundException
/// より DI/初期化漏れの原因が分かりやすい）。
/// </summary>
public sealed class SiteRateLimiter
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, SemaphoreSlim> _siteGates;
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestAt;
    private readonly Func<Task<int>> _getDelayMs;
    private readonly int _maxAttempts;
    private readonly int _retryDelayMs;

    public SiteRateLimiter(
        HttpClient httpClient,
        IEnumerable<string> siteKeys,
        Func<Task<int>> getDelayMs,
        int maxAttempts = 3,
        int retryDelayMs = 500)
    {
        _httpClient = httpClient;
        _getDelayMs = getDelayMs;
        _maxAttempts = maxAttempts;
        _retryDelayMs = retryDelayMs;
        _siteGates = new Dictionary<string, SemaphoreSlim>();
        _lastRequestAt = new ConcurrentDictionary<string, DateTime>();
        foreach (var key in siteKeys)
        {
            _siteGates[key] = new SemaphoreSlim(1, 1);
            _lastRequestAt[key] = DateTime.MinValue;
        }
    }

    public async Task<string> GetStringAsync(string siteKey, string url, CancellationToken ct = default)
    {
        if (!_siteGates.TryGetValue(siteKey, out var gate))
        {
            throw new ArgumentException(
                $"Unknown siteKey '{siteKey}'. Constructor must be initialized with all expected site keys.",
                nameof(siteKey));
        }

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                await EnforceDelayAsync(siteKey, ct).ConfigureAwait(false);
                try
                {
                    var result = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
                    _lastRequestAt[siteKey] = DateTime.UtcNow;
                    return result;
                }
                catch (HttpRequestException ex) when (attempt < _maxAttempts && TransientHttpErrorHelper.IsTransient(ex) && !ct.IsCancellationRequested)
                {
                    // リトライ予定の失敗も「サーバへ実際に投げた」扱いにし、次の試行まで
                    // request_delay_ms を尊重する（礼儀+連続失敗時の負荷抑制）。
                    _lastRequestAt[siteKey] = DateTime.UtcNow;
                    MessageService.Warn(
                        $"Transient failure [{siteKey}] {url} (attempt {attempt}/{_maxAttempts}): {ex.Message}");
                    await Task.Delay(_retryDelayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // ユーザ操作等によるキャンセルは異常ではないのでスタック付き ERROR を出さない。
                    throw;
                }
                catch (Exception ex)
                {
                    _lastRequestAt[siteKey] = DateTime.UtcNow;
                    HttpRequestFailureLogger.Log(siteKey, url, ex);
                    throw;
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnforceDelayAsync(string siteKey, CancellationToken ct)
    {
        var delayMs = await _getDelayMs().ConfigureAwait(false);
        var last = _lastRequestAt[siteKey];
        if (last == DateTime.MinValue) return;

        var elapsed = (DateTime.UtcNow - last).TotalMilliseconds;
        var remaining = delayMs - elapsed;
        if (remaining > 0)
        {
            await Task.Delay((int)remaining, ct).ConfigureAwait(false);
        }
    }
}
