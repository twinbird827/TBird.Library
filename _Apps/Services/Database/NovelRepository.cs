using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public sealed record NovelWithUnread(Novel Novel, int UnreadCount);

public class NovelRepository
{
    private sealed class NovelWithUnreadRow : Novel
    {
        [SQLite.Column("unread_count")]
        public int UnreadCount { get; set; }
    }

    private readonly SQLiteAsyncConnection _db;
    private readonly DatabaseService _dbService;
    private readonly EpisodeCacheRepository _cacheRepo;

    public NovelRepository(DatabaseService dbService, EpisodeCacheRepository cacheRepo)
    {
        _dbService = dbService;
        _db = dbService.Connection;
        _cacheRepo = cacheRepo;
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
                "SELECT n.id, n.site_type, n.novel_id, n.title, n.author, " +
                "n.total_episodes, n.is_completed, n.last_updated_at, " +
                "n.registered_at, n.has_unconfirmed_update, n.has_check_error, " +
                "n.is_favorite, n.favorited_at " +
                "FROM novels n " +
                "ORDER BY (SELECT COUNT(*) FROM episodes e WHERE e.novel_id = n.id AND e.is_read = 0) DESC, n.last_updated_at DESC"
            ).ConfigureAwait(false),
            "favorite_first" => await _db.QueryAsync<Novel>(
                "SELECT id, site_type, novel_id, title, author, " +
                "total_episodes, is_completed, last_updated_at, " +
                "registered_at, has_unconfirmed_update, has_check_error, " +
                "is_favorite, favorited_at " +
                "FROM novels ORDER BY is_favorite DESC, last_updated_at DESC"
            ).ConfigureAwait(false),
            _ => await _db.Table<Novel>().OrderByDescending(n => n.LastUpdatedAt).ToListAsync().ConfigureAwait(false),
        };
    }

    public async Task<List<NovelWithUnread>> GetAllWithUnreadCountAsync(string sortKey)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);

        const string baseSql =
            "SELECT " +
            "  n.id, " +
            "  n.site_type, " +
            "  n.novel_id, " +
            "  n.title, " +
            "  n.author, " +
            "  n.total_episodes, " +
            "  n.is_completed, " +
            "  n.last_updated_at, " +
            "  n.registered_at, " +
            "  n.has_unconfirmed_update, " +
            "  n.has_check_error, " +
            "  n.is_favorite, " +
            "  n.favorited_at, " +
            "  COALESCE(u.cnt, 0) AS unread_count " +
            "FROM novels n " +
            "LEFT JOIN (" +
            "    SELECT novel_id, COUNT(*) AS cnt " +
            "    FROM episodes " +
            "    WHERE is_read = 0 " +
            "    GROUP BY novel_id" +
            ") u ON u.novel_id = n.id ";

        string orderBy = sortKey switch
        {
            "updated_asc"     => "ORDER BY n.last_updated_at ASC",
            "title_asc"       => "ORDER BY n.title ASC",
            "title_desc"      => "ORDER BY n.title DESC",
            "author_asc"      => "ORDER BY n.author ASC",
            "registered_desc" => "ORDER BY n.registered_at DESC",
            "unread_desc"     => "ORDER BY unread_count DESC, n.last_updated_at DESC",
            "favorite_first"  => "ORDER BY n.is_favorite DESC, n.last_updated_at DESC",
            _                 => "ORDER BY n.last_updated_at DESC",
        };

        var rows = await _db.QueryAsync<NovelWithUnreadRow>(baseSql + orderBy)
            .ConfigureAwait(false);

        var result = new List<NovelWithUnread>(rows.Count);
        foreach (var r in rows)
        {
            var novel = new Novel
            {
                Id = r.Id,
                SiteType = r.SiteType,
                NovelId = r.NovelId,
                Title = r.Title,
                Author = r.Author,
                TotalEpisodes = r.TotalEpisodes,
                IsCompleted = r.IsCompleted,
                LastUpdatedAt = r.LastUpdatedAt,
                RegisteredAt = r.RegisteredAt,
                HasUnconfirmedUpdate = r.HasUnconfirmedUpdate,
                HasCheckError = r.HasCheckError,
                IsFavorite = r.IsFavorite,
                FavoritedAt = r.FavoritedAt,
            };
            result.Add(new NovelWithUnread(novel, r.UnreadCount));
        }
        return result;
    }

    public async Task<Novel?> GetByIdAsync(int id)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Table<Novel>().FirstOrDefaultAsync(n => n.Id == id).ConfigureAwait(false);
    }

    public async Task<HashSet<(int SiteType, string NovelId)>> GetExistingSiteNovelIdsAsync()
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        var novels = await _db.Table<Novel>().ToListAsync().ConfigureAwait(false);
        return new HashSet<(int, string)>(novels.Select(n => (n.SiteType, n.NovelId)));
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
            favorite, now, novelId).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int novelId)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.RunInTransactionAsync(conn =>
        {
            _cacheRepo.DeleteByNovelIdSync(conn, novelId);
            conn.Execute("DELETE FROM episodes WHERE novel_id = ?", novelId);
            conn.Execute("DELETE FROM novels WHERE id = ?", novelId);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// (site_type, novel_id) で Novel を補償削除。
    /// SearchViewModel.RegisterAsync の Insert 成功後ネットワーク失敗時に使用。
    /// </summary>
    public async Task DeleteBySiteAndNovelIdAsync(int siteType, string novelId)
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.RunInTransactionAsync(conn =>
        {
            var rows = conn.Query<Novel>(
                "SELECT * FROM novels WHERE site_type = ? AND novel_id = ?",
                siteType, novelId);
            foreach (var n in rows)
            {
                _cacheRepo.DeleteByNovelIdSync(conn, n.Id);
                conn.Execute("DELETE FROM episodes WHERE novel_id = ?", n.Id);
                conn.Execute("DELETE FROM novels WHERE id = ?", n.Id);
            }
        }).ConfigureAwait(false);
    }

    public async Task<int> CountAsync()
    {
        await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Table<Novel>().CountAsync().ConfigureAwait(false);
    }
}
