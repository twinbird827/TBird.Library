using LanobeReader.Models;

namespace LanobeReader.Services;

public interface INovelService
{
    SiteType SiteType { get; }
    Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default);
    Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default);
    // siteEpisodeId: DB に永続化されたサイト側エピソード ID(あれば)。位置依存解決を避けるために使う。
    // 未保存(旧データ)や episode_no で直接 URL を組めるサイト(Narou)では null/未使用でよい。
    Task<string> FetchEpisodeContentAsync(string novelId, int episodeNo, string? siteEpisodeId, CancellationToken ct = default);
    Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default);
}
