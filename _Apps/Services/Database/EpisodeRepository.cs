using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class EpisodeRepository
{
    private readonly SQLiteAsyncConnection _db;
    private readonly DatabaseService _dbService;

    public EpisodeRepository(DatabaseService dbService)
    {
        _dbService = dbService;
        _db = dbService.Connection;
    }

    private Task EnsureAsync() => _dbService.EnsureInitializedAsync();

    public async Task<List<Episode>> GetByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId)
            .OrderBy(e => e.EpisodeNo)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<Episode>> GetPagedByNovelIdAsync(int novelId, int page, int pageSize)
    {
        await EnsureAsync().ConfigureAwait(false);
        int offset = (page - 1) * pageSize;
        return await _db.QueryAsync<Episode>(
            "SELECT * FROM episodes WHERE novel_id = ? ORDER BY episode_no LIMIT ? OFFSET ?",
            novelId, pageSize, offset).ConfigureAwait(false);
    }

    public async Task<int> CountByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>().Where(e => e.NovelId == novelId).CountAsync().ConfigureAwait(false);
    }

    public async Task<int> CountUnreadByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>().Where(e => e.NovelId == novelId && e.IsRead == 0).CountAsync().ConfigureAwait(false);
    }

    public async Task<Episode?> GetByNovelAndEpisodeNoAsync(int novelId, int episodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .FirstOrDefaultAsync(e => e.NovelId == novelId && e.EpisodeNo == episodeNo).ConfigureAwait(false);
    }

    public async Task<Episode?> GetByIdAsync(int id)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>().FirstOrDefaultAsync(e => e.Id == id).ConfigureAwait(false);
    }

    public async Task<int> GetMaxEpisodeNoAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(episode_no), 0) FROM episodes WHERE novel_id = ?", novelId).ConfigureAwait(false);
    }

    public async Task<Episode?> GetLastReadEpisodeAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsRead == 1)
            .OrderByDescending(e => e.EpisodeNo)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<Episode?> GetFirstUnreadEpisodeAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsRead == 0)
            .OrderBy(e => e.EpisodeNo)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task InsertAllAsync(IEnumerable<Episode> episodes)
    {
        await EnsureAsync().ConfigureAwait(false);
        await _db.InsertAllAsync(episodes).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Episode episode)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.UpdateAsync(episode).ConfigureAwait(false);
    }

    public async Task MarkAsReadAsync(int episodeId)
    {
        await EnsureAsync().ConfigureAwait(false);
        var now = DateTime.UtcNow.ToString("o");
        await _db.ExecuteAsync(
            "UPDATE episodes SET is_read = 1, read_at = ? WHERE id = ?", now, episodeId
        ).ConfigureAwait(false);
    }

    public async Task<bool> AreAllReadAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        var count = await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsRead == 0)
            .CountAsync().ConfigureAwait(false);
        return count == 0;
    }

    public async Task SetFavoriteAsync(int episodeId, bool favorite)
    {
        await EnsureAsync().ConfigureAwait(false);
        var now = favorite ? DateTime.UtcNow.ToString("o") : null;
        await _db.ExecuteAsync(
            "UPDATE episodes SET is_favorite = ?, favorited_at = ? WHERE id = ?",
            favorite ? 1 : 0, now, episodeId).ConfigureAwait(false);
    }

    public async Task<List<Episode>> GetFavoritesByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsFavorite == 1)
            .OrderBy(e => e.EpisodeNo)
            .ToListAsync().ConfigureAwait(false);
    }
}
