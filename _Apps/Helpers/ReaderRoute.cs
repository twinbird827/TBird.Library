namespace LanobeReader.Helpers;

public static class ReaderRoute
{
    // siteType を int で受けるのは、SiteType enum を補間すると数値でなく名前になり ReaderViewModel が解釈できないため。
    public static string Build(int novelId, int episodeId, int siteType, string siteNovelId)
        => $"reader?novelId={novelId}&episodeId={episodeId}&siteType={siteType}&siteNovelId={siteNovelId}";
}
