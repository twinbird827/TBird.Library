using LanobeReader.Models;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using TBird.Core;
using TBird.Maui.Background;

namespace LanobeReader.Services;

public class UpdateCheckService
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly INovelServiceFactory _serviceFactory;
    private readonly BackgroundJobQueue? _jobQueue;

    public UpdateCheckService(
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        INovelServiceFactory serviceFactory,
        BackgroundJobQueue? jobQueue = null)
    {
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _serviceFactory = serviceFactory;
        _jobQueue = jobQueue;
    }

    public async Task<List<(Novel novel, int newEpisodeCount)>> CheckAllAsync(CancellationToken ct = default)
    {
        if (!await _semaphore.WaitAsync(0, ct).ConfigureAwait(false))
        {
            MessageService.Warn("Update check already running, skipping");
            return [];
        }

        try
        {
            // 「最後にチェックした時刻が古い順(未チェック=null 優先)」で回す。3分上限(shortService)
            // 等で打ち切られても、次回が続きから拾える (ラウンドロビン) ようにするため。
            var novels = await _novelRepo.GetAllForCheckAsync().ConfigureAwait(false);
            var updates = new List<(Novel, int)>();

            foreach (var novel in novels)
            {
                if (ct.IsCancellationRequested) break;

                // 取得自体は常に行う。HasUnconfirmedUpdate=true の小説をスキップすると、
                // ユーザがアプリを開かない限り更新追跡から脱落する問題があったため (H-2)。
                // 通知は notificationId=novel.Id で上書き表示されるため重複通知にはならない。

                var cancelled = false;
                try
                {
                    var service = _serviceFactory.GetService((SiteType)novel.SiteType);
                    var (totalEpisodes, lastUpdatedAt, isCompleted, author) = await service.FetchNovelInfoAsync(novel.NovelId, ct).ConfigureAwait(false);

                    var currentMaxEpisode = await _episodeRepo.GetMaxEpisodeNoAsync(novel.Id).ConfigureAwait(false);

                    if (totalEpisodes > currentMaxEpisode)
                    {
                        // Fetch new episodes
                        var allEpisodes = await service.FetchEpisodeListAsync(novel.NovelId, ct).ConfigureAwait(false);
                        var newEpisodes = allEpisodes
                            .Where(e => e.EpisodeNo > currentMaxEpisode)
                            .Select(e => { e.NovelId = novel.Id; return e; })
                            .ToList();

                        if (newEpisodes.Count > 0)
                        {
                            await _episodeRepo.InsertAllAsync(newEpisodes).ConfigureAwait(false);

                            novel.TotalEpisodes = totalEpisodes;
                            novel.LastUpdatedAt = lastUpdatedAt ?? DateTime.UtcNow.ToString("o");
                            novel.HasUnconfirmedUpdate = true;
                            novel.IsCompleted = isCompleted;
                            if (!string.IsNullOrEmpty(author) && string.IsNullOrEmpty(novel.Author))
                            {
                                novel.Author = author;
                            }

                            updates.Add((novel, newEpisodes.Count));

                            // Enqueue newly-added episodes for background prefetch (Wi-Fi gated)
                            if (_jobQueue is not null)
                            {
                                var inserted = await _episodeRepo.GetByNovelIdAsync(novel.Id).ConfigureAwait(false);
                                foreach (var ep in inserted.Where(e => e.EpisodeNo > currentMaxEpisode))
                                {
                                    await _jobQueue.EnqueueAsync(new PrefetchEpisodeJob
                                    {
                                        NovelDbId = novel.Id,
                                        EpisodeDbId = ep.Id,
                                        EpisodeNo = ep.EpisodeNo,
                                        SiteType = novel.SiteType,
                                        SiteNovelId = novel.NovelId,
                                    }, novel.IsFavorite ? JobPriority.High : JobPriority.Normal).ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    novel.HasCheckError = false; // 成功時はエラーフラグを解除
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
                {
                    if (ct.IsCancellationRequested)
                    {
                        // 打ち切り(3分上限等)。この作品は LastCheckedAt を更新せず、
                        // 次回この作品から再開できるようにループを抜ける。
                        cancelled = true;
                    }
                    else
                    {
                        MessageService.Warn($"Failed to check {novel.Title}: {ex.Message}");
                        novel.HasCheckError = true;
                    }
                }

                if (cancelled) break;

                // 成功・失敗いずれも LastCheckedAt を更新して 1 回だけ永続化し、
                // ラウンドロビンを前進させる(失敗作品も後ろへ回し、特定作品で詰まらせない)。
                novel.LastCheckedAt = DateTime.UtcNow.ToString("o");
                await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
            }

            return updates;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
