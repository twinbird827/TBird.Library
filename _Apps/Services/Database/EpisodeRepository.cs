using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class EpisodeRepository
{
    private readonly SQLiteAsyncConnection _db;

    public EpisodeRepository(DatabaseService dbService)
    {
        _db = dbService.Connection;
    }

    public Task<List<Episode>> GetByNovelIdAsync(int novelId)
    {
        return _db.Table<Episode>()
            .Where(e => e.NovelId == novelId)
            .OrderBy(e => e.EpisodeNo)
            .ToListAsync();
    }

    public Task<List<Episode>> GetPagedByNovelIdAsync(int novelId, int page, int pageSize)
    {
        int offset = (page - 1) * pageSize;
        return _db.QueryAsync<Episode>(
            "SELECT * FROM episodes WHERE novel_id = ? ORDER BY episode_no LIMIT ? OFFSET ?",
            novelId, pageSize, offset);
    }

    public Task<int> CountByNovelIdAsync(int novelId)
    {
        return _db.Table<Episode>().Where(e => e.NovelId == novelId).CountAsync();
    }

    public Task<int> CountUnreadByNovelIdAsync(int novelId)
    {
        return _db.Table<Episode>().Where(e => e.NovelId == novelId && e.IsRead == 0).CountAsync();
    }

    public Task<Episode?> GetByNovelAndEpisodeNoAsync(int novelId, int episodeNo)
    {
        return _db.Table<Episode>()
            .FirstOrDefaultAsync(e => e.NovelId == novelId && e.EpisodeNo == episodeNo)!;
    }

    public Task<Episode?> GetByIdAsync(int id)
    {
        return _db.Table<Episode>().FirstOrDefaultAsync(e => e.Id == id)!;
    }

    public Task<int> GetMaxEpisodeNoAsync(int novelId)
    {
        return _db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(episode_no), 0) FROM episodes WHERE novel_id = ?", novelId);
    }

    public Task<Episode?> GetLastReadEpisodeAsync(int novelId)
    {
        return _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsRead == 1)
            .OrderByDescending(e => e.EpisodeNo)
            .FirstOrDefaultAsync()!;
    }

    public Task<Episode?> GetFirstUnreadEpisodeAsync(int novelId)
    {
        return _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsRead == 0)
            .OrderBy(e => e.EpisodeNo)
            .FirstOrDefaultAsync()!;
    }

    public async Task InsertAllAsync(IEnumerable<Episode> episodes)
    {
        await _db.InsertAllAsync(episodes).ConfigureAwait(false);
    }

    public Task<int> UpdateAsync(Episode episode)
    {
        return _db.UpdateAsync(episode);
    }

    public async Task MarkAsReadAsync(int episodeId)
    {
        var now = DateTime.UtcNow.ToString("o");
        await _db.ExecuteAsync(
            "UPDATE episodes SET is_read = 1, read_at = ? WHERE id = ?", now, episodeId
        ).ConfigureAwait(false);
    }

    public Task<bool> AreAllReadAsync(int novelId)
    {
        return _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsRead == 0)
            .CountAsync()
            .ContinueWith(t => t.Result == 0);
    }
}
