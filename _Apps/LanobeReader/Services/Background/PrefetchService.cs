using LanobeReader.Services.Database;
using TBird.Core;
using TBird.Maui.Background;

namespace LanobeReader.Services.Background;

/// <summary>
/// 先読み（プリフェッチ）のエントリポイント。
/// 未キャッシュ話を BackgroundJobQueue に積むだけ。実通信は Queue 側で直列処理。
/// </summary>
public class PrefetchService
{
    private readonly BackgroundJobQueue _queue;
    private readonly EpisodeRepository _episodeRepo;

    public PrefetchService(
        BackgroundJobQueue queue,
        EpisodeRepository episodeRepo)
    {
        _queue = queue;
        _episodeRepo = episodeRepo;
    }

    /// <summary>
    /// 指定小説の全未キャッシュ話をキューイング。
    /// </summary>
    public async Task<int> EnqueueNovelAsync(int novelDbId, bool highPriority = false)
    {
        var targets = await _episodeRepo.GetUncachedTargetsAsync(novelDbId).ConfigureAwait(false);
        foreach (var t in targets)
        {
            await EnqueueAsync(t, highPriority || t.IsFavorite).ConfigureAwait(false);
        }
        MessageService.Info($"Enqueued {targets.Count} episodes for novel {novelDbId}");
        return targets.Count;
    }

    /// <summary>
    /// 全登録小説の未読＆未キャッシュ話をキューイング。起動時に呼ぶ想定。
    /// お気に入り作品の話が先頭に来る順で返るので、その順に積む。
    /// </summary>
    public async Task EnqueueAllUnreadAsync()
    {
        var targets = await _episodeRepo.GetUnreadUncachedTargetsAsync().ConfigureAwait(false);
        foreach (var t in targets)
        {
            await EnqueueAsync(t, t.IsFavorite).ConfigureAwait(false);
        }
        MessageService.Info($"Enqueued {targets.Count} unread episodes for all novels");
    }

    private Task EnqueueAsync(EpisodeRepository.PrefetchTarget t, bool highPriority) =>
        _queue.EnqueueAsync(new PrefetchEpisodeJob
        {
            NovelDbId = t.NovelDbId,
            EpisodeDbId = t.EpisodeDbId,
            EpisodeNo = t.EpisodeNo,
            SiteType = t.SiteType,
            SiteNovelId = t.SiteNovelId,
            SiteEpisodeId = t.SiteEpisodeId,
        }, highPriority ? JobPriority.High : JobPriority.Normal);
}
