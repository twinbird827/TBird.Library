using System.Globalization;
using System.Text;

namespace LanobeReader.Helpers;

/// <summary>
/// 縦書き WebView 用の HTML を生成するビルダー。
/// スタイル値は CSS カスタムプロパティ（--reader-fs 等）に切り出してあり、
/// 将来 JS から :root の値を書き換えることでライブ反映が可能な構造。
/// </summary>
public static class ReaderHtmlBuilder
{
    public static string Build(string content, ReaderCssState state)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(content.Length + 1024);

        sb.Append("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<style>");
        var (bgHex, fgHex) = ReaderStyleResolver.ResolveThemeColors(state.BackgroundThemeIndex);
        var lh = ReaderStyleResolver.ResolveLineHeight(state.LineSpacingIndex);
        sb.Append(":root{");
        sb.Append($"--reader-fs:{state.FontSizePx.ToString("0.##", inv)}px;");
        sb.Append($"--reader-lh:{lh.ToString("0.##", inv)};");
        sb.Append($"--reader-bg:{bgHex};");
        sb.Append($"--reader-fg:{fgHex};");
        sb.Append("}");
        sb.Append("html,body{margin:0;padding:0;height:100%;}");
        sb.Append("body{");
        sb.Append("background:var(--reader-bg);color:var(--reader-fg);");
        sb.Append("font-family:serif;");
        sb.Append("writing-mode:vertical-rl;-webkit-writing-mode:vertical-rl;");
        sb.Append("font-size:var(--reader-fs);");
        sb.Append("line-height:var(--reader-lh);");
        sb.Append("padding:16px;box-sizing:border-box;overflow-x:auto;overflow-y:hidden;");
        sb.Append("-webkit-tap-highlight-color:transparent;");
        sb.Append("}");
        sb.Append("p{margin:0 0 1em 0;text-indent:1em;}");
        sb.Append("</style></head><body>");

        foreach (var line in content.Split('\n'))
        {
            sb.Append("<p>");
            sb.Append(System.Net.WebUtility.HtmlEncode(line));
            sb.Append("</p>");
        }

        // 既存の read-end 検知 JS（lanobe://read-end URI 発火）。
        // OnWebViewNavigating 側と連動しているため変更禁止。
        sb.Append("<script>");
        sb.Append("(function(){var fired=false;function check(){if(fired)return;");
        sb.Append("var el=document.scrollingElement||document.documentElement;");
        sb.Append("var maxNeg=-(el.scrollWidth-el.clientWidth);");
        sb.Append("if(el.scrollLeft<=maxNeg+10){fired=true;location.href='lanobe://read-end';}}");
        sb.Append("window.addEventListener('scroll',check,{passive:true});setTimeout(check,100);})();");
        sb.Append("</script>");
        sb.Append("</body></html>");

        return sb.ToString();
    }
}
