using LanobeReader.Models;

namespace LanobeReader.Services;

public interface INovelService
{
    SiteType SiteType { get; }
    Task<List<SearchResult>> SearchAsync(string keyword, string searchTarget, CancellationToken ct = default);
    Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default);
    Task<string> FetchEpisodeContentAsync(string novelId, int episodeNo, CancellationToken ct = default);
    Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default);
}
