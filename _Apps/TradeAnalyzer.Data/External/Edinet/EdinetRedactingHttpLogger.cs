using System.Text.RegularExpressions;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;

namespace TradeAnalyzer.Data.External.Edinet;

/// <summary>
/// EDINET HttpClient 用の差し替えロガー。リクエスト URI の Subscription-Key をマスクして
/// 要求/応答/失敗を記録する。factory 既定ロガー（生 URI を出力＝鍵漏洩経路）を
/// <c>RemoveAllLoggers()</c> で除去した上で、本ロガーを <c>AddLogger</c> で差し替える。
/// </summary>
/// <remarks>
/// クラスは public（別アセンブリ TradeAnalyzer.Worker の SelfTest が <see cref="Redact"/> を参照するため）。
/// 状態を持たないため <see cref="LogRequestStart"/> は null(context) を返し、経過時間は framework から受け取る。
/// IHttpClientLogger 契約: ログ中の未捕捉例外はリクエスト自体を失敗させるため、各メソッドは
/// 正規表現置換と ILogger 呼出のみで実質 throw しない（防御的 try-catch は入れない）。
/// </remarks>
public class EdinetRedactingHttpLogger : IHttpClientLogger
{
    private readonly ILogger<EdinetRedactingHttpLogger> _logger;

    // マスク対象は URL 組立と同じ定数（EdinetClient.SubscriptionKeyParam）から構築し、キー名の単一真実源を保つ。
    // [^&]* で次クエリ越え（貪欲マッチ）を防ぐ。値は EscapeDataString 済みのため & を含まない。
    private static readonly Regex SubscriptionKeyPattern =
        new(Regex.Escape(EdinetClient.SubscriptionKeyParam) + "=[^&]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public EdinetRedactingHttpLogger(ILogger<EdinetRedactingHttpLogger> logger)
    {
        _logger = logger;
    }

    public object? LogRequestStart(HttpRequestMessage request)
    {
        _logger.LogInformation("EDINET HTTP 開始 {Method} {Uri}", request.Method, Redact(request.RequestUri));
        return null;
    }

    public void LogRequestStop(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed)
    {
        _logger.LogInformation(
            "EDINET HTTP 完了 {Method} {Uri} -> {Status} ({Elapsed}ms)",
            request.Method, Redact(request.RequestUri), (int)response.StatusCode, elapsed.TotalMilliseconds);
    }

    public void LogRequestFailed(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed)
    {
        _logger.LogError(
            exception,
            "EDINET HTTP 失敗 {Method} {Uri} -> {Status} ({Elapsed}ms)",
            request.Method, Redact(request.RequestUri),
            response is null ? "(なし)" : ((int)response.StatusCode).ToString(),
            elapsed.TotalMilliseconds);
    }

    /// <summary>URI 文字列中の <c>Subscription-Key=&lt;値&gt;</c> の値をマスクする。null は空文字を返す（純粋関数・no-throw）。</summary>
    public static string Redact(Uri? uri)
        => uri is null
            ? string.Empty
            : SubscriptionKeyPattern.Replace(uri.ToString(), EdinetClient.SubscriptionKeyParam + "=***");
}
