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
            "SELECT id, novel_id, episode_no, chapter_name, title, " +
            "is_read, read_at, published_at, is_favorite, favorited_at " +
            "FROM episodes WHERE novel_id = ? ORDER BY episode_no LIMIT ? OFFSET ?",
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
        return await _db.Table<Episode>().Where(e => e.NovelId == novelId && !e.IsRead).CountAsync().ConfigureAwait(false);
    }

    public async Task<Episode?> GetByNovelAndEpisodeNoAsync(int novelId, int episodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .FirstOrDefaultAsync(e => e.NovelId == novelId && e.EpisodeNo == episodeNo).ConfigureAwait(false);
    }

    public async Task<Episode?> GetPreviousEpisodeAsync(int novelId, int currentEpisodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var results = await _db.QueryAsync<Episode>(
            "SELECT id, novel_id, episode_no, chapter_name, title, " +
            "is_read, read_at, published_at, is_favorite, favorited_at " +
            "FROM episodes WHERE novel_id = ? AND episode_no < ? " +
            "ORDER BY episode_no DESC LIMIT 1",
            novelId, currentEpisodeNo).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<Episode?> GetNextEpisodeAsync(int novelId, int currentEpisodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var results = await _db.QueryAsync<Episode>(
            "SELECT id, novel_id, episode_no, chapter_name, title, " +
            "is_read, read_at, published_at, is_favorite, favorited_at " +
            "FROM episodes WHERE novel_id = ? AND episode_no > ? " +
            "ORDER BY episode_no ASC LIMIT 1",
            novelId, currentEpisodeNo).ConfigureAwait(false);
        return results.FirstOrDefault();
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
            .Where(e => e.NovelId == novelId && e.IsRead)
            .OrderByDescending(e => e.EpisodeNo)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<Episode?> GetFirstUnreadEpisodeAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && !e.IsRead)
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

    /// <summary>
    /// 読了点 (episode_no) を境に既読状態を一括更新する。
    /// 1..N: is_read=1（既存 read_at は COALESCE で保持、未設定なら now を入れる）
    /// N+1..max: is_read=0、read_at=NULL に巻き戻し
    /// 過去話を再読した場合は意図的に N+1 以降を未読化する仕様（ユーザ承認済み）。
    /// </summary>
    public async Task SetReadStateUpToAsync(int novelId, int episodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var now = DateTime.UtcNow.ToString("o");

        await _db.RunInTransactionAsync(conn =>
        {
            conn.Execute(
                "UPDATE episodes SET is_read = 1, read_at = COALESCE(read_at, ?) " +
                "WHERE novel_id = ? AND episode_no <= ?",
                now, novelId, episodeNo);

            conn.Execute(
                "UPDATE episodes SET is_read = 0, read_at = NULL " +
                "WHERE novel_id = ? AND episode_no > ?",
                novelId, episodeNo);
        }).ConfigureAwait(false);
    }

    public async Task<bool> AreAllReadAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        var count = await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && !e.IsRead)
            .CountAsync().ConfigureAwait(false);
        return count == 0;
    }

    public async Task SetFavoriteAsync(int episodeId, bool favorite)
    {
        await EnsureAsync().ConfigureAwait(false);
        var now = favorite ? DateTime.UtcNow.ToString("o") : null;
        await _db.ExecuteAsync(
            "UPDATE episodes SET is_favorite = ?, favorited_at = ? WHERE id = ?",
            favorite, now, episodeId).ConfigureAwait(false);
    }

    public async Task<List<Episode>> GetFavoritesByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await _db.Table<Episode>()
            .Where(e => e.NovelId == novelId && e.IsFavorite)
            .OrderBy(e => e.EpisodeNo)
            .ToListAsync().ConfigureAwait(false);
    }
}
