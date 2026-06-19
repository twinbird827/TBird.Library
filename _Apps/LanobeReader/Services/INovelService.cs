using LanobeReader.Models;

namespace LanobeReader.Services;

public interface INovelService
{
    SiteType SiteType { get; }
    Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default);
    Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default);
    // siteEpisodeId: DB に永続化されたサイト側エピソード ID(あれば)。位置依存解決を避けるために使う。
    // 未保存(旧データ)や episode_no で直接 URL を組めるサイト(Narou)では null/未使用でよい。
    // 戻り値 cacheable: 取得本文を永続キャッシュ(episode_cache)してよいか。安定 ID が陳腐化して位置依存
    // フォールバックで取得した本文はドリフトで誤話の可能性があり、かつ安定 ID が残置されるため backfill の
    // キャッシュ破棄でも訂正されない。これを恒久キャッシュしないよう false を返す(呼び出し側はキャッシュを抑止)。
    Task<(string content, bool cacheable)> FetchEpisodeContentAsync(string novelId, int episodeNo, string? siteEpisodeId, CancellationToken ct = default);
    Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default);
}
