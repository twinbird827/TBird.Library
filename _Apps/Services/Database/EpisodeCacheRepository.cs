using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class EpisodeCacheRepository
{
    private readonly SQLiteAsyncConnection _db;

    public EpisodeCacheRepository(DatabaseService dbService)
    {
        _db = dbService.Connection;
    }

    public Task<EpisodeCache?> GetByEpisodeIdAsync(int episodeId)
    {
        return _db.Table<EpisodeCache>()
            .FirstOrDefaultAsync(c => c.EpisodeId == episodeId)!;
    }

    public Task<int> InsertAsync(EpisodeCache cache)
    {
        return _db.InsertAsync(cache);
    }

    public async Task DeleteByNovelIdAsync(int novelId)
    {
        await _db.ExecuteAsync(
            "DELETE FROM episode_cache WHERE episode_id IN (SELECT id FROM episodes WHERE novel_id = ?)",
            novelId
        ).ConfigureAwait(false);
    }

    public Task DeleteAllAsync()
    {
        return _db.DeleteAllAsync<EpisodeCache>();
    }

    public async Task DeleteExpiredAsync(int cacheMonths)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-cacheMonths).ToString("o");
        await _db.ExecuteAsync(
            "DELETE FROM episode_cache WHERE cached_at < ?", cutoff
        ).ConfigureAwait(false);
    }
}
