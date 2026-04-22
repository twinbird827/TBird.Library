using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;

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
            LogHelper.Warn(nameof(UpdateCheckService), "Update check already running, skipping");
            return [];
        }

        try
        {
            var novels = await _novelRepo.GetAllAsync().ConfigureAwait(false);
            var updates = new List<(Novel, int)>();
            var failedIds = new HashSet<int>();

            foreach (var novel in novels)
            {
                if (ct.IsCancellationRequested) break;

                // Skip novels with unconfirmed updates
                if (novel.HasUnconfirmedUpdate) continue;

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
                            await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);

                            updates.Add((novel, newEpisodes.Count));

                            // Enqueue newly-added episodes for background prefetch (Wi-Fi gated)
                            if (_jobQueue is not null)
                            {
                                var inserted = await _episodeRepo.GetByNovelIdAsync(novel.Id).ConfigureAwait(false);
                                foreach (var ep in inserted.Where(e => e.EpisodeNo > currentMaxEpisode))
                                {
                                    _jobQueue.Enqueue(new PrefetchEpisodeJob
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
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    LogHelper.Warn(nameof(UpdateCheckService), $"Failed to check {novel.Title}: {ex.Message}");
                    novel.HasCheckError = true;
                    await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
                    failedIds.Add(novel.Id);
                    continue;
                }
            }

            // Reset error flags on success
            foreach (var novel in novels.Where(n => n.HasCheckError && !failedIds.Contains(n.Id)))
            {
                novel.HasCheckError = false;
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
