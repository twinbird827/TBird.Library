using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class NovelRepository
{
    private readonly SQLiteAsyncConnection _db;

    public NovelRepository(DatabaseService dbService)
    {
        _db = dbService.Connection;
    }

    public Task<List<Novel>> GetAllAsync()
    {
        return _db.Table<Novel>().OrderByDescending(n => n.LastUpdatedAt).ToListAsync();
    }

    public Task<Novel?> GetByIdAsync(int id)
    {
        return _db.Table<Novel>().FirstOrDefaultAsync(n => n.Id == id)!;
    }

    public Task<Novel?> GetBySiteAndNovelIdAsync(int siteType, string novelId)
    {
        return _db.Table<Novel>()
            .FirstOrDefaultAsync(n => n.SiteType == siteType && n.NovelId == novelId)!;
    }

    public Task<int> InsertAsync(Novel novel)
    {
        return _db.InsertAsync(novel);
    }

    public Task<int> UpdateAsync(Novel novel)
    {
        return _db.UpdateAsync(novel);
    }

    public async Task DeleteAsync(int novelId)
    {
        // CASCADE: episode_cache → episodes → novels
        var episodes = await _db.Table<Episode>().Where(e => e.NovelId == novelId).ToListAsync().ConfigureAwait(false);
        foreach (var ep in episodes)
        {
            await _db.ExecuteAsync("DELETE FROM episode_cache WHERE episode_id = ?", ep.Id).ConfigureAwait(false);
        }
        await _db.ExecuteAsync("DELETE FROM episodes WHERE novel_id = ?", novelId).ConfigureAwait(false);
        await _db.DeleteAsync<Novel>(novelId).ConfigureAwait(false);
    }

    public Task<int> CountAsync()
    {
        return _db.Table<Novel>().CountAsync();
    }
}
