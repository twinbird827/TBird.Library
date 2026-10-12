namespace LanobeReader.Helpers;

public static class ReaderRoute
{
    // siteType を int で受けるのは、SiteType enum を補間すると数値でなく名前になり ReaderViewModel が解釈できないため。
    public static string Build(int novelId, int episodeId, int siteType, string siteNovelId)
        => $"reader?novelId={novelId}&episodeId={episodeId}&siteType={siteType}&siteNovelId={siteNovelId}";

    // リーダーを上に積む前提の目次ルート。EpisodeListViewModel は resumeAnchor の値でなくキーの有無で戻り時のアンカー復帰を決める。
    public static string BuildEpisodesWithAnchor(int novelId)
        => $"episodes?novelId={novelId}&resumeAnchor=1";
}
