using LanobeReader.Models;

namespace LanobeReader.Services.Database;

public sealed record NovelWithUnread(
    Novel Novel,
    int UnreadCount,
    int ReadCount,
    int EpisodeCount);

public class NovelRepository(DatabaseService dbService, EpisodeCacheRepository cacheRepo)
{
    private sealed class NovelWithUnreadRow : Novel
    {
        [SQLite.Column("unread_count")]
        public int UnreadCount { get; set; }

        [SQLite.Column("read_count")]
        public int ReadCount { get; set; }

        [SQLite.Column("episode_count")]
        public int EpisodeCount { get; set; }
    }

    /// <summary>
    /// 更新チェック用に「最後にチェックした時刻が古い順(未チェック=null を最優先)」で全件取得する。
    /// SQLite では NULL が ASC で先頭に来るため、未チェックの小説が最優先で回る。
    /// 3分上限等で打ち切られても次回が続きから拾える (ラウンドロビン) ようにするため。
    /// </summary>
    public async Task<List<Novel>> GetAllForCheckAsync()
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.Table<Novel>()
            .OrderBy(n => n.LastCheckedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<NovelWithUnread>> GetAllWithUnreadCountAsync(string sortKey)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);

        // episodes 1 パス GROUP BY で episode_count / read_count / unread_count を一括集計。
        // (novel_id, is_read, episode_no) 複合インデックス (idx_episodes_novel_isread_epno, schema v4) が
        // covering index となる(旧 idx_episodes_novel_isread は v4 で DROP 済み・本索引が上位互換)。
        // 全 3 値を episodes から派生させることで「既読+未読=総話数」の不変条件を保証する。
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
            "  COALESCE(e.unread_count, 0) AS unread_count, " +
            "  COALESCE(e.read_count, 0) AS read_count, " +
            "  COALESCE(e.episode_count, 0) AS episode_count " +
            "FROM novels n " +
            "LEFT JOIN (" +
            "    SELECT " +
            "      novel_id, " +
            "      COUNT(*) AS episode_count, " +
            "      SUM(CASE WHEN is_read = 1 THEN 1 ELSE 0 END) AS read_count, " +
            "      SUM(CASE WHEN is_read = 0 THEN 1 ELSE 0 END) AS unread_count " +
            "    FROM episodes " +
            "    GROUP BY novel_id" +
            ") e ON e.novel_id = n.id ";

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

        var rows = await dbService.Connection.QueryAsync<NovelWithUnreadRow>(baseSql + orderBy)
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
            result.Add(new NovelWithUnread(novel, r.UnreadCount, r.ReadCount, r.EpisodeCount));
        }
        return result;
    }

    public async Task<Novel?> GetByIdAsync(int id)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.Table<Novel>().FirstOrDefaultAsync(n => n.Id == id).ConfigureAwait(false);
    }

    public async Task<HashSet<(int SiteType, string NovelId)>> GetExistingSiteNovelIdsAsync()
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        var novels = await dbService.Connection.Table<Novel>().ToListAsync().ConfigureAwait(false);
        return new HashSet<(int, string)>(novels.Select(n => (n.SiteType, n.NovelId)));
    }

    public async Task<Novel?> GetBySiteAndNovelIdAsync(int siteType, string novelId)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.Table<Novel>()
            .FirstOrDefaultAsync(n => n.SiteType == siteType && n.NovelId == novelId).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Novel novel)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.InsertAsync(novel).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Novel novel)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.UpdateAsync(novel).ConfigureAwait(false);
    }

    /// <summary>
    /// 更新チェックの結果を書き戻す。更新チェックが管理する列だけを書き(新着時のみ has_unconfirmed_update = 1)、
    /// 巡回中に行われたお気に入り切替・NEW 解除を巡回開始時の値へ巻き戻さない。
    /// </summary>
    public async Task UpdateCheckResultAsync(Novel novel, bool markUnconfirmed)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        var unconfirmed = markUnconfirmed ? ", has_unconfirmed_update = 1" : "";
        await dbService.Connection.ExecuteAsync(
            "UPDATE novels SET total_episodes = ?, last_updated_at = ?, is_completed = ?, author = ?, has_check_error = ?, last_checked_at = ?"
                + unconfirmed + " WHERE id = ?",
            novel.TotalEpisodes, novel.LastUpdatedAt, novel.IsCompleted, novel.Author, novel.HasCheckError, novel.LastCheckedAt, novel.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// 複数作品の last_checked_at 列のみを 1 トランザクションでまとめて更新する。更新チェックで状態に
    /// 変化が無い(新着なし・エラー状態も不変)作品の巡回タイムスタンプ前進に使い、管理列 UPDATE と
    /// 作品ごとの個別コミット(巡回1周＝作品数ぶんの書き込み)を避ける。
    /// </summary>
    public async Task UpdateLastCheckedAtBatchAsync(IReadOnlyList<(int Id, string Ts)> items)
    {
        if (items.Count == 0) return;
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        await dbService.Connection.RunInTransactionAsync(conn =>
        {
            foreach (var (id, ts) in items)
            {
                conn.Execute("UPDATE novels SET last_checked_at = ? WHERE id = ?", ts, id);
            }
        }).ConfigureAwait(false);
    }

    public async Task SetFavoriteAsync(int novelId, bool favorite)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        var now = favorite ? DateTime.UtcNow.ToString("o") : null;
        await dbService.Connection.ExecuteAsync(
            "UPDATE novels SET is_favorite = ?, favorited_at = ? WHERE id = ?",
            favorite, now, novelId).ConfigureAwait(false);
    }

    public async Task ClearUnconfirmedUpdateAsync(int novelId)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        await dbService.Connection.ExecuteAsync(
            "UPDATE novels SET has_unconfirmed_update = 0 WHERE id = ?", novelId).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int novelId)
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        await dbService.Connection.RunInTransactionAsync(conn =>
        {
            cacheRepo.DeleteByNovelIdSync(conn, novelId);
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
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        await dbService.Connection.RunInTransactionAsync(conn =>
        {
            var rows = conn.Query<Novel>(
                "SELECT * FROM novels WHERE site_type = ? AND novel_id = ?",
                siteType, novelId);
            foreach (var n in rows)
            {
                cacheRepo.DeleteByNovelIdSync(conn, n.Id);
                conn.Execute("DELETE FROM episodes WHERE novel_id = ?", n.Id);
                conn.Execute("DELETE FROM novels WHERE id = ?", n.Id);
            }
        }).ConfigureAwait(false);
    }

    public async Task<int> CountAsync()
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.Table<Novel>().CountAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 未確認の更新を持つ小説数を COUNT クエリで取得する(全件ロードを避ける)。
    /// OEM ランチャーの数字バッジ用。
    /// </summary>
    public async Task<int> CountUnconfirmedAsync()
    {
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        return await dbService.Connection.Table<Novel>()
            .Where(n => n.HasUnconfirmedUpdate)
            .CountAsync().ConfigureAwait(false);
    }
}
