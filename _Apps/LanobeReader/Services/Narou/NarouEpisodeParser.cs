using AngleSharp.Dom;
using LanobeReader.Helpers;

namespace LanobeReader.Services.Narou;

// 本文 HTML の解析を NarouApiService から切り出したもの。LanobeReader.Tests がこのファイル単体を
// リンク取り込みしてテストするため、AngleSharp・BCL・EpisodeContentFormat 以外(LanobeReader の他の型・MAUI)に依存しない。
public static class NarouEpisodeParser
{
    // 挿絵の src はプロトコル相対(//...)のため、なろうの URL を基準に絶対 URL へ解決する。
    private static readonly Uri BaseUri = new("https://ncode.syosetu.com/");

    public static string ExtractContent(IDocument document)
    {
        var lines = new List<string>();
        var hasBody = false;
        // 前書き・本文・後書きは同じ .js-novel-text.p-novel__text を持つので、修飾クラスで区画を見分ける。
        foreach (var section in document.QuerySelectorAll(".p-novel__body > .js-novel-text.p-novel__text"))
        {
            if (section.ClassList.Contains("p-novel__text--preface"))
            {
                lines.Add(EpisodeContentFormat.PrefaceStart);
            }
            else if (section.ClassList.Contains("p-novel__text--afterword"))
            {
                lines.Add(EpisodeContentFormat.AfterwordStart);
            }
            else
            {
                lines.Add(EpisodeContentFormat.BodyStart);
                hasBody = true;
            }

            foreach (var p in section.QuerySelectorAll("p"))
            {
                var images = p.QuerySelectorAll("img");
                if (images.Length == 0)
                {
                    lines.Add(p.TextContent);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(p.TextContent)) lines.Add(p.TextContent);
                foreach (var img in images)
                {
                    if (img.GetAttribute("src") is { Length: > 0 } src && Uri.TryCreate(BaseUri, src, out var url))
                    {
                        lines.Add(EpisodeContentFormat.ImagePrefix + url.AbsoluteUri);
                    }
                }
            }
        }

        if (!hasBody)
        {
            throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
        }

        return string.Join("\n", lines).Trim();
    }
}
