using System;
using System.Text;
using TBird.Core;

namespace TBird.Maui.Web;

/// <summary>
/// HTTP リクエスト失敗時の詳細ログ出力。
/// <see cref="System.Net.Http.HttpRequestException"/> の Message が "Connection failure" 等の
/// 抽象的な文字列だけだと Android 側の真の原因 (UnknownHostException / SSLHandshakeException /
/// EOFException 等) が見えないため、InnerException チェーンを最大 5 段まで吐く。
/// </summary>
public static class HttpRequestFailureLogger
{
    public static void Log(string siteKey, string url, Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append("Request failed [").Append(siteKey).Append("] ").AppendLine(url);
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
            for (int i = 0; i < System.Math.Min(3, lines.Length); i++)
            {
                sb.Append("    ").AppendLine(lines[i].TrimEnd());
            }
        }
        MessageService.Error(sb.ToString());
    }
}
