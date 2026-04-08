using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class NovelRepository
{
    private readonly SQLiteAsyncConnection _db;
    private readonly DatabaseService _dbService;

    public NovelRepository(DatabaseService dbService)
    {
        _dbService = dbService;
        _db = dbService.Connection;
    }

    public Task<List<Novel>> GetAllAsync()
    {
        return GetAllAsync("updated_desc");
    }

    public async Task<List<Novel>> GetAllAsync(string sortKey)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return sortKey switch
        {
            "updated_asc" => await _db.Table<Novel>().OrderBy(n => n.LastUpdatedAt).ToListAsync().ConfigureAwait(false),
            "title_asc" => await _db.Table<Novel>().OrderBy(n => n.Title).ToListAsync().ConfigureAwait(false),
            "title_desc" => await _db.Table<Novel>().OrderByDescending(n => n.Title).ToListAsync().ConfigureAwait(false),
            "author_asc" => await _db.Table<Novel>().OrderBy(n => n.Author).ToListAsync().ConfigureAwait(false),
            "registered_desc" => await _db.Table<Novel>().OrderByDescending(n => n.RegisteredAt).ToListAsync().ConfigureAwait(false),
            "unread_desc" => await _db.QueryAsync<Novel>(
                "SELECT n.* FROM novels n " +
                "ORDER BY (SELECT COUNT(*) FROM episodes e WHERE e.novel_id = n.id AND e.is_read = 0) DESC, n.last_updated_at DESC"
            ).ConfigureAwait(false),
            "favorite_first" => await _db.QueryAsync<Novel>(
                "SELECT * FROM novels ORDER BY is_favorite DESC, last_updated_at DESC"
            ).ConfigureAwait(false),
            _ => await _db.Table<Novel>().OrderByDescending(n => n.LastUpdatedAt).ToListAsync().ConfigureAwait(false),
        };
    }

    public async Task<Novel?> GetByIdAsync(int id)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Table<Novel>().FirstOrDefaultAsync(n => n.Id == id).ConfigureAwait(false);
    }

    public async Task<Novel?> GetBySiteAndNovelIdAsync(int siteType, string novelId)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Table<Novel>()
            .FirstOrDefaultAsync(n => n.SiteType == siteType && n.NovelId == novelId).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Novel novel)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.InsertAsync(novel).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Novel novel)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.UpdateAsync(novel).ConfigureAwait(false);
    }

    public async Task SetFavoriteAsync(int novelId, bool favorite)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        var now = favorite ? DateTime.UtcNow.ToString("o") : null;
        await _db.ExecuteAsync(
            "UPDATE novels SET is_favorite = ?, favorited_at = ? WHERE id = ?",
            favorite ? 1 : 0, now, novelId).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int novelId)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        // CASCADE: episode_cache → episodes → novels
        var episodes = await _db.Table<Episode>().Where(e => e.NovelId == novelId).ToListAsync().ConfigureAwait(false);
        foreach (var ep in episodes)
        {
            await _db.ExecuteAsync("DELETE FROM episode_cache WHERE episode_id = ?", ep.Id).ConfigureAwait(false);
        }
        await _db.ExecuteAsync("DELETE FROM episodes WHERE novel_id = ?", novelId).ConfigureAwait(false);
        await _db.DeleteAsync<Novel>(novelId).ConfigureAwait(false);
    }

    public async Task<int> CountAsync()
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Table<Novel>().CountAsync().ConfigureAwait(false);
    }
}
