using LanobeReader.Models;
using LanobeReader.Services.Database;

namespace LanobeReader.Services;

/// <summary>
/// 本文取得とキャッシュ可否(cacheable)契約を 1 箇所へ閉じ込めるファサード。Reader/Prefetch が
/// (content, cacheable) タプルを受け取り「cacheable のときだけ保存」を各自で守る規約を廃し、本クラス経由に
/// 一本化する(位置依存フォールバックで取得した誤話可能性のある本文を恒久キャッシュしない不変条件を中央化)。
/// </summary>
public class EpisodeContentService
{
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly INovelServiceFactory _serviceFactory;

    public EpisodeContentService(EpisodeCacheRepository cacheRepo, INovelServiceFactory serviceFactory)
    {
        _cacheRepo = cacheRepo;
        _serviceFactory = serviceFactory;
    }

    /// <summary>
    /// 本文をキャッシュ優先で取得する。命中時はそれを返す。未命中かつ networkAllowed=false なら null を返す
    /// (呼び出し元がオフライン UX を提示)。ネットワーク取得した本文は cacheable=true のときのみ永続化する。
    /// cacheable はここで消費し呼び出し元へは漏らさない。
    /// </summary>
    public async Task<string?> GetContentAsync(
        int episodeDbId, SiteType siteType, string siteNovelId, int episodeNo, string? siteEpisodeId,
        bool networkAllowed, CancellationToken ct = default)
    {
        var cached = await _cacheRepo.GetByEpisodeIdAsync(episodeDbId).ConfigureAwait(false);
        if (cached is not null) return cached.Content;
        if (!networkAllowed) return null;

        var service = _serviceFactory.GetService(siteType);
        var (content, cacheable) = await service
            .FetchEpisodeContentAsync(siteNovelId, episodeNo, siteEpisodeId, ct).ConfigureAwait(false);

        await _cacheRepo.UpsertIfCacheableAsync(episodeDbId, content, cacheable).ConfigureAwait(false);
        return content;
    }
}
