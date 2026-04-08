using System.Text;

namespace LanobeReader.Helpers;

/// <summary>
/// 縦書き WebView 用の HTML 文字列を生成するビルダー。
/// </summary>
public static class ReaderHtmlBuilder
{
    public static string Build(string content, double fontSizePx, double lineHeight, int backgroundTheme)
    {
        var (bg, fg) = backgroundTheme switch
        {
            1 => ("#121212", "#E0E0E0"),
            2 => ("#F5E6C8", "#3E2C1C"),
            _ => ("#FFFFFF", "#212121"),
        };

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<style>");
        sb.Append("html,body{margin:0;padding:0;height:100%;}");
        sb.Append($"body{{background:{bg};color:{fg};font-family:serif;");
        sb.Append($"writing-mode:vertical-rl;-webkit-writing-mode:vertical-rl;");
        sb.Append($"font-size:{fontSizePx.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}px;");
        sb.Append($"line-height:{lineHeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)};");
        sb.Append("padding:16px;box-sizing:border-box;overflow-x:auto;overflow-y:hidden;");
        sb.Append("-webkit-tap-highlight-color:transparent;}");
        sb.Append("p{margin:0 0 1em 0;text-indent:1em;}");
        sb.Append("</style></head><body>");

        foreach (var line in content.Split('\n'))
        {
            sb.Append("<p>");
            sb.Append(System.Net.WebUtility.HtmlEncode(line));
            sb.Append("</p>");
        }

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
