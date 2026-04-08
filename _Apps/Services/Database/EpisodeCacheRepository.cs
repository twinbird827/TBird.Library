using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class EpisodeCacheRepository
{
    private readonly SQLiteAsyncConnection _db;
    private readonly DatabaseService _dbService;

    public EpisodeCacheRepository(DatabaseService dbService)
    {
        _dbService = dbService;
        _db = dbService.Connection;
    }

    private Task EnsureAsync() => _dbService.EnsureInitializedAsync();

    public async Task<EpisodeCache?> GetByEpisodeIdAsync(int episodeId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<EpisodeCache>()
            .FirstOrDefaultAsync(c => c.EpisodeId == episodeId).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(EpisodeCache cache)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.InsertAsync(cache).ConfigureAwait(false);
    }

    public async Task DeleteByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        await _db.ExecuteAsync(
            "DELETE FROM episode_cache WHERE episode_id IN (SELECT id FROM episodes WHERE novel_id = ?)",
            novelId
        ).ConfigureAwait(false);
    }

    public async Task DeleteAllAsync()
    {
        await EnsureAsync().ConfigureAwait(false);
        await _db.DeleteAllAsync<EpisodeCache>().ConfigureAwait(false);
    }

    public async Task<HashSet<int>> GetCachedEpisodeIdsAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        var rows = await _db.QueryAsync<CachedIdRow>(
            "SELECT c.episode_id AS EpisodeId FROM episode_cache c " +
            "INNER JOIN episodes e ON e.id = c.episode_id WHERE e.novel_id = ?",
            novelId).ConfigureAwait(false);
        return rows.Select(r => r.EpisodeId).ToHashSet();
    }

    private class CachedIdRow
    {
        public int EpisodeId { get; set; }
    }

    public async Task DeleteExpiredAsync(int cacheMonths)
    {
        await EnsureAsync().ConfigureAwait(false);
        var cutoff = DateTime.UtcNow.AddMonths(-cacheMonths).ToString("o");
        await _db.ExecuteAsync(
            "DELETE FROM episode_cache WHERE cached_at < ?", cutoff
        ).ConfigureAwait(false);
    }
}
