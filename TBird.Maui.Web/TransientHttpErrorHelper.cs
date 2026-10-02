using System.Net.Http;

namespace TBird.Maui.Web;

/// <summary>
/// HTTP リクエスト失敗の transient/非 transient 判定。
/// 4xx クライアントエラーは恒久的（リトライ不要）、5xx / 408 / 429 とステータスなし
/// （DNS / SSL / ソケット層の失敗）は transient とみなす。
/// </summary>
public static class TransientHttpErrorHelper
{
    public static bool IsTransient(HttpRequestException ex)
    {
        if (ex.StatusCode is { } code)
        {
            var n = (int)code;
            return n >= 500 || n == 408 || n == 429;
        }
        return true;
    }
}
