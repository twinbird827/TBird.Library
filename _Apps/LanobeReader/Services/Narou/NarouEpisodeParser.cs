using AngleSharp.Dom;

namespace LanobeReader.Services.Narou;

// 本文 HTML の解析を NarouApiService から切り出したもの。LanobeReader.Tests がこのファイル単体を
// リンク取り込みしてテストするため、AngleSharp と BCL 以外(LanobeReader の他の型・MAUI)に依存しない。
public static class NarouEpisodeParser
{
    public static string ExtractContent(IDocument document)
    {
        // 前書き・後書きも同じ .js-novel-text.p-novel__text を持ち本文より前に並ぶため、修飾クラスで除外する。
        var honbun = document.QuerySelector(".js-novel-text.p-novel__text:not(.p-novel__text--preface):not(.p-novel__text--afterword)");
        if (honbun is null)
        {
            throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
        }

        var paragraphs = honbun.QuerySelectorAll("p");
        var lines = paragraphs.Select(p => p.TextContent);
        return string.Join("\n", lines).Trim();
    }
}
