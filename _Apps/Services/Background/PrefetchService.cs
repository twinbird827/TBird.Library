using LanobeReader.Helpers;
using LanobeReader.Services.Database;

namespace LanobeReader.Services.Background;

/// <summary>
/// 先読み（プリフェッチ）のエントリポイント。
/// 未キャッシュ話を BackgroundJobQueue に積むだけ。実通信は Queue 側で直列処理。
/// </summary>
public class PrefetchService
{
    private readonly BackgroundJobQueue _queue;
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly EpisodeCacheRepository _cacheRepo;

    public PrefetchService(
        BackgroundJobQueue queue,
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        EpisodeCacheRepository cacheRepo)
    {
        _queue = queue;
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _cacheRepo = cacheRepo;
    }

    /// <summary>
    /// 指定小説の全未キャッシュ話をキューイング。
    /// </summary>
    public async Task<int> EnqueueNovelAsync(int novelDbId, bool highPriority = false)
    {
        var novel = await _novelRepo.GetByIdAsync(novelDbId).ConfigureAwait(false);
        if (novel is null) return 0;

        var episodes = await _episodeRepo.GetByNovelIdAsync(novelDbId).ConfigureAwait(false);
        var cachedIds = await _cacheRepo.GetCachedEpisodeIdsAsync(novelDbId).ConfigureAwait(false);

        int enqueued = 0;
        foreach (var ep in episodes)
        {
            if (cachedIds.Contains(ep.Id)) continue;
            _queue.Enqueue(new PrefetchEpisodeJob
            {
                NovelDbId = novel.Id,
                EpisodeDbId = ep.Id,
                EpisodeNo = ep.EpisodeNo,
                SiteType = novel.SiteType,
                SiteNovelId = novel.NovelId,
                Priority = (highPriority || novel.IsFavorite) ? 1 : 0,
            });
            enqueued++;
        }
        LogHelper.Info(nameof(PrefetchService), $"Enqueued {enqueued} episodes for novel {novelDbId}");
        return enqueued;
    }

    /// <summary>
    /// 全登録小説の未読＆未キャッシュ話をキューイング。起動時に呼ぶ想定。
    /// </summary>
    public async Task EnqueueAllUnreadAsync()
    {
        var novels = await _novelRepo.GetAllAsync().ConfigureAwait(false);
        // お気に入りを先頭へ
        var ordered = novels.OrderByDescending(n => n.IsFavorite).ToList();
        foreach (var novel in ordered)
        {
            var episodes = await _episodeRepo.GetByNovelIdAsync(novel.Id).ConfigureAwait(false);
            var cachedIds = await _cacheRepo.GetCachedEpisodeIdsAsync(novel.Id).ConfigureAwait(false);

            foreach (var ep in episodes.Where(e => !e.IsRead))
            {
                if (cachedIds.Contains(ep.Id)) continue;
                _queue.Enqueue(new PrefetchEpisodeJob
                {
                    NovelDbId = novel.Id,
                    EpisodeDbId = ep.Id,
                    EpisodeNo = ep.EpisodeNo,
                    SiteType = novel.SiteType,
                    SiteNovelId = novel.NovelId,
                    Priority = novel.IsFavorite ? 1 : 0,
                });
            }
        }
    }
}
