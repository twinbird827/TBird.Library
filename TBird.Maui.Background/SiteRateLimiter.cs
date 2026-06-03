using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
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

    /// <summary>
    /// サイト別ゲート＋ディレイ＋transient リトライ越しに HTTP GET し、本文文字列を返す。
    /// </summary>
    public Task<string> GetStringAsync(string siteKey, string url, CancellationToken ct = default)
        => SendAsync(siteKey, url, c => _httpClient.GetStringAsync(url, c), ct);

    /// <summary>
    /// サイト別ゲート＋ディレイ＋transient リトライ越しに JSON 本文を POST し、応答本文文字列を返す。
    /// 認証ヘッダ等は <see cref="HttpClient.DefaultRequestHeaders"/> 側で付与する想定（GET と同じ HttpClient を共用）。
    /// 非 2xx 応答は <see cref="HttpStatusCode"/> 付きの <see cref="HttpRequestException"/> として例外化し、
    /// GET と同じく transient（5xx / 408 / 429）はリトライ、それ以外は呼出側へ伝播する。
    /// 失敗時はサーバが返したエラー本文（認証失敗・上流エラー説明等）を例外メッセージに含める
    /// （Android では <see cref="HttpRequestException.Message"/> が抽象的なため、原因究明性を確保する）。
    /// <para>
    /// ⚠️ transient リトライは同一 <paramref name="jsonBody"/> を再送する。検索・参照系のように
    /// 同じ本文を複数回受け取っても副作用が重複しない冪等なエンドポイントにのみ使うこと。
    /// 状態を変更する非冪等エンドポイントでは二重適用が起こりうる（冪等性は呼出側の責任）。
    /// </para>
    /// </summary>
    public Task<string> PostJsonAsync(string siteKey, string url, string jsonBody, CancellationToken ct = default)
        => SendAsync(siteKey, url, async c =>
        {
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, c).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // EnsureSuccessStatusCode() は本文を読まず throw するため、中継サーバが返すエラー JSON が失われる。
                // 本文を読んで例外メッセージに含めつつ、StatusCode を引き継いだ HttpRequestException を投げて
                // transient 判定（TransientHttpErrorHelper.IsTransient）を従来どおり機能させる。
                var error = await response.Content.ReadAsStringAsync(c).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"POST {url} failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Truncate(error)}",
                    null,
                    response.StatusCode);
            }
            return await response.Content.ReadAsStringAsync(c).ConfigureAwait(false);
        }, ct);

    /// <summary>
    /// HTTP 送信を siteKey 単位で直列化し、ディレイ挿入＋transient リトライでゲートする共通処理。
    /// <paramref name="send"/> が実際の HTTP 呼び出し（GET / POST 等）を行う。
    /// </summary>
    private async Task<string> SendAsync(
        string siteKey, string url, Func<CancellationToken, Task<string>> send, CancellationToken ct)
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
                    var result = await send(ct).ConfigureAwait(false);
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

    /// <summary>POST 失敗時の例外メッセージへ載せるエラー本文を上限長で切り詰める（ログ肥大化防止）。</summary>
    private const int MaxErrorBodyChars = 1000;

    private static string Truncate(string s)
        => string.IsNullOrEmpty(s) || s.Length <= MaxErrorBodyChars
            ? s
            : s.Substring(0, MaxErrorBodyChars) + "…(truncated)";
}
