using System.Net;
using System.Text;

namespace LanobeReader.Helpers;

// 本文キャッシュ(1 行 1 段落のテキスト)に混ぜるマーカー行の形式と、その HTML 化。
// マーカー行は U+0001 で始まり、マーカーを含まない本文(既存キャッシュ・カクヨム)はそのまま全行本文として読める。
// LanobeReader.Tests がリンク取り込みしてテストするため、BCL 以外に依存しない。
public static class EpisodeContentFormat
{
    private const char Marker = '\u0001';

    public const string PrefaceStart = "\u0001preface";
    public const string BodyStart = "\u0001body";
    public const string AfterwordStart = "\u0001afterword";

    // 後ろに画像の絶対 URL が続く。
    public const string ImagePrefix = "\u0001img:";

    public static string ToHtml(string content)
    {
        var sb = new StringBuilder(content.Length + 256);
        var inSection = false;
        foreach (var line in content.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!line.StartsWith(Marker))
            {
                sb.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
                continue;
            }

            switch (line)
            {
                case PrefaceStart:
                case BodyStart:
                case AfterwordStart:
                    if (inSection) sb.Append("</div>");
                    inSection = line != BodyStart;
                    if (inSection) sb.Append(line == PrefaceStart ? "<div class=\"preface\">" : "<div class=\"afterword\">");
                    break;
                default:
                    // 画像以外の未知のマーカー行と、http(s) 以外の URL は出力しない。
                    if (line.StartsWith(ImagePrefix)
                        && Uri.TryCreate(line[ImagePrefix.Length..], UriKind.Absolute, out var url)
                        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
                    {
                        sb.Append("<img src=\"").Append(WebUtility.HtmlEncode(url.AbsoluteUri)).Append("\" alt=\"挿絵\">");
                    }
                    break;
            }
        }
        if (inSection) sb.Append("</div>");
        return sb.ToString();
    }
}
