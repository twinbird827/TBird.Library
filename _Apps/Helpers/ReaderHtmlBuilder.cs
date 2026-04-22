using System.Globalization;
using System.Text;

namespace LanobeReader.Helpers;

/// <summary>
/// 縦書き WebView 用の HTML を生成するビルダー。
/// テンプレート本体は Resources/Html/ReaderTemplate.html に EmbeddedResource として格納され、
/// 初回アクセス時に 1 回だけ読み込んで static キャッシュし、以降はプレースホルダー置換のみ行う。
/// スタイル値は CSS カスタムプロパティ（--reader-fs 等）に切り出してあり、
/// 将来 JS から :root の値を書き換えることでライブ反映が可能な構造。
/// </summary>
public static class ReaderHtmlBuilder
{
    // csproj の EmbeddedResource で LogicalName=%(Filename)%(Extension) を指定しているため、
    // リソース ID はファイル名そのまま。
    private const string TemplateResourceName = "ReaderTemplate.html";

    private static readonly string _template = LoadTemplate();

    private static string LoadTemplate()
    {
        var assembly = typeof(ReaderHtmlBuilder).Assembly;
        using var stream = assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource not found: {TemplateResourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string Build(string content, ReaderCssState state)
    {
        var inv = CultureInfo.InvariantCulture;
        var (bgHex, fgHex) = ReaderStyleResolver.ResolveThemeColors(state.BackgroundThemeIndex);
        var lh = ReaderStyleResolver.ResolveLineHeight(state.LineSpacingIndex);
        var fs = state.FontSizePx.ToString("0.##", inv);
        var lhs = lh.ToString("0.##", inv);

        return _template
            .Replace("__FS__", fs)
            .Replace("__LH__", lhs)
            .Replace("__BG__", bgHex)
            .Replace("__FG__", fgHex)
            .Replace("__BODY__", BuildBody(content));
    }

    private static string BuildBody(string content)
    {
        var sb = new StringBuilder(content.Length + 256);
        foreach (var line in content.ReplaceLineEndings("\n").Split('\n'))
        {
            sb.Append("<p>");
            sb.Append(System.Net.WebUtility.HtmlEncode(line));
            sb.Append("</p>");
        }
        return sb.ToString();
    }
}
