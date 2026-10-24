namespace TradeAnalyzer.Data.External.JQuants;

/// <summary>
/// 物理 HTTP 送信ごとに <see cref="JQuantsRateLimiter"/> を通すための DelegatingHandler。
/// リトライ（resilience handler）より「内側」に配置することで、初回・再試行を問わず
/// 全ての実 HTTP 要求がレート上限の対象になる。
/// </summary>
public sealed class JQuantsRateLimitHandler : DelegatingHandler
{
    private readonly JQuantsRateLimiter _limiter;

    public JQuantsRateLimitHandler(JQuantsRateLimiter limiter) => _limiter = limiter;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => _limiter.ExecuteAsync(() => base.SendAsync(request, cancellationToken), cancellationToken);
}
