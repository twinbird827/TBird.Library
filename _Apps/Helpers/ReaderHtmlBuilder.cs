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
        var (bgHex, fgHex) = ReaderStyleResolver.ResolveThemeColors(state.BackgroundThemeIndex);
        var lh = ReaderStyleResolver.ResolveLineHeight(state.LineSpacingIndex);
        var fs = state.FontSizePx.ToString("0.##", inv);
        var lhs = lh.ToString("0.##", inv);

        var body = BuildBody(content);

        // NOTE: 下部 <script> の read-end / next-episode / prev-episode URI 発火は
        // ReaderPage.OnWebViewNavigating と連動しているため変更禁止。
        // $$$"""..."""（ドル 3 / クォート 3）: 補間マーカーは {{{var}}}。
        // JS の `}}` 連続（例: `;}}`）がリテラルとしてそのまま書けるよう 3 つにしている。
        return $$$"""
            <!doctype html><html lang="ja"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <style>
            :root{--reader-fs:{{{fs}}}px;--reader-lh:{{{lhs}}};--reader-bg:{{{bgHex}}};--reader-fg:{{{fgHex}}};}
            html,body{margin:0;padding:0;height:100%;overflow-y:hidden;}
            body{
              background:var(--reader-bg);color:var(--reader-fg);
              font-family:serif;
              writing-mode:vertical-rl;-webkit-writing-mode:vertical-rl;
              font-size:var(--reader-fs);line-height:var(--reader-lh);
              padding:16px;box-sizing:border-box;
              overflow-x:auto;overflow-y:hidden;touch-action:pan-x;overscroll-behavior-y:none;
              -webkit-tap-highlight-color:transparent;
            }
            p{margin:0 0 1em 0;text-indent:1em;}
            </style></head><body>
            {{{body}}}
            <script>
            (function(){var fired=false;function check(){if(fired)return;
              var el=document.scrollingElement||document.documentElement;
              var maxNeg=-(el.scrollWidth-el.clientWidth);
              if(el.scrollLeft<=maxNeg+10){fired=true;location.href='lanobe://read-end';}}
              window.addEventListener('scroll',check,{passive:true});setTimeout(check,100);})();
            (function(){var sx,sy,st;
              document.addEventListener('touchstart',function(e){
                sx=e.touches[0].clientX;sy=e.touches[0].clientY;st=Date.now();
              },{passive:true});
              document.addEventListener('touchend',function(e){
                var dx=e.changedTouches[0].clientX-sx;
                var dy=e.changedTouches[0].clientY-sy;
                var dt=Date.now()-st;
                if(dt>300)return;
                if(Math.abs(dy)>Math.abs(dx)&&Math.abs(dy)>80){
                  if(dy<0)location.href='lanobe://next-episode';
                  else location.href='lanobe://prev-episode';}
              },{passive:true});})();
            </script>
            </body></html>
            """;
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
