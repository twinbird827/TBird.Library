using Microsoft.Extensions.Options;
using TradeAnalyzer.Data.Options;

namespace TradeAnalyzer.Data.External.JQuants;

/// <summary>
/// J-Quants Free のレート上限（5 req/分＝プラン全体）を守るためのプロセス内ゲート。
/// 全要求を SemaphoreSlim(1) で直列化し、直近送信からの最低間隔を強制する。
/// リトライも実 HTTP 要求として上限に算入されるため、このゲートは「実際の送信ごと」に
/// 通す必要がある（<see cref="JQuantsRateLimitHandler"/> を最内ハンドラに置く）。
/// 状態（直近送信時刻）を跨いで保持するため singleton で登録する。
/// </summary>
public sealed class JQuantsRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minInterval;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastSend = DateTimeOffset.MinValue;

    public JQuantsRateLimiter(IOptions<JQuantsOptions> options, TimeProvider? timeProvider = null)
    {
        var sec = Math.Max(1, options.Value.MinIntervalSeconds);
        _minInterval = TimeSpan.FromSeconds(sec);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 最低間隔（MinInterval）を満たすまで待機してから <paramref name="send"/> を実行する。
    /// 責務は「全送信を直列化し最低間隔を強制する」レートゲートのみ。
    /// 429 の Retry-After 遵守は標準 retry（HttpRetryStrategyOptions.ShouldRetryAfterHeader）へ委ねる
    /// ＝ Retry-After 待機を per-attempt timeout の外側で行わせ、レートゲート（timeout 内側）に持ち込まない。
    /// </summary>
    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var wait = _minInterval - (_timeProvider.GetUtcNow() - _lastSend);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, _timeProvider, ct).ConfigureAwait(false);

            var response = await send().ConfigureAwait(false);
            _lastSend = _timeProvider.GetUtcNow();
            return response;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
