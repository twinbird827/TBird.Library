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

    // 複数の raw SQL クエリで使う episodes の列リスト。列追加時の更新漏れ(列順・列セットのズレ)を
    // 防ぐため一元化する。Episode のプロパティ([Column] 属性)へ列名でマップされる。
    private const string EpisodeColumns =
        "id, novel_id, episode_no, chapter_name, title, " +
        "is_read, read_at, published_at, is_favorite, favorited_at, site_episode_id";

    public async Task<List<Episode>> GetByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        // ORM (Table<T>().Where().OrderBy().ToListAsync()) は LINQ 式木 → SQL コンパイルの
        // オーバーヘッドが乗るため、長尺小説 (1500+ 話) では raw SQL が体感で速い。
        // GetPagedByNovelIdAsync と同じ列順 / 列セットを維持。
        return await _db.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} " +
            "FROM episodes WHERE novel_id = ? ORDER BY episode_no",
            novelId).ConfigureAwait(false);
    }

    public async Task<List<Episode>> GetPagedByNovelIdAsync(int novelId, int page, int pageSize)
    {
        await EnsureAsync().ConfigureAwait(false);
        int offset = (page - 1) * pageSize;
        return await _db.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} " +
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
            $"SELECT {EpisodeColumns} " +
            "FROM episodes WHERE novel_id = ? AND episode_no < ? " +
            "ORDER BY episode_no DESC LIMIT 1",
            novelId, currentEpisodeNo).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<Episode?> GetNextEpisodeAsync(int novelId, int currentEpisodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var results = await _db.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} " +
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

    /// <summary>
    /// 複数小説のディープリンク先エピソード Id をまとめて解決する。各小説につき
    /// 「最初の未読話(最小 episode_no)」、未読が無ければ「最後に読んだ話(最大 episode_no)」。
    /// 通知ループでの作品ごと逐次クエリ(最大 2×N 往復)を 2 クエリに集約する。戻り値は novelId -> episodeId。
    /// 該当話が無い小説はキーを持たない(呼び出し側で 0 フォールバック)。
    /// </summary>
    public async Task<Dictionary<int, int>> GetDeepLinkTargetEpisodeIdsAsync(IReadOnlyList<int> novelIds)
    {
        var result = new Dictionary<int, int>();
        if (novelIds.Count == 0) return result;
        await EnsureAsync().ConfigureAwait(false);

        // 各小説の最初の未読話(最小 episode_no)。
        await ResolveTargetEpisodesAsync(novelIds, isRead: 0, useMin: true, result).ConfigureAwait(false);

        // 未読が無い小説は最後に読んだ話(最大 episode_no)へフォールバック。
        var remaining = novelIds.Where(id => !result.ContainsKey(id)).ToList();
        if (remaining.Count > 0)
        {
            await ResolveTargetEpisodesAsync(remaining, isRead: 1, useMin: false, result).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// novelIds の各小説について is_read=<paramref name="isRead"/> の話の中で episode_no が
    /// 最小(<paramref name="useMin"/>=true)/最大の話 Id を解決し <paramref name="result"/> へ詰める。
    /// SQLite の変数上限(既定 999)に達しないよう IN 句の引数をチャンク分割して照会する。
    /// </summary>
    private async Task ResolveTargetEpisodesAsync(
        IReadOnlyList<int> novelIds, int isRead, bool useMin, Dictionary<int, int> result)
    {
        const int ChunkSize = 900;
        // isRead(0/1) と agg(MIN/MAX) はコード由来のリテラルでユーザ入力ではないため SQL に直接埋めてよい。
        // 可変長の novelIds のみプレースホルダでパラメータ化する。
        var agg = useMin ? "MIN" : "MAX";
        for (int offset = 0; offset < novelIds.Count; offset += ChunkSize)
        {
            var chunk = novelIds.Skip(offset).Take(ChunkSize).ToList();
            var placeholders = string.Join(",", chunk.Select(_ => "?"));
            var args = chunk.Cast<object>().ToArray();
            var rows = await _db.QueryAsync<EpisodeRef>(
                $"SELECT e.novel_id AS NovelId, e.id AS Id FROM episodes e " +
                $"WHERE e.is_read = {isRead} AND e.novel_id IN ({placeholders}) " +
                $"AND e.episode_no = (SELECT {agg}(episode_no) FROM episodes " +
                $"WHERE novel_id = e.novel_id AND is_read = {isRead})",
                args).ConfigureAwait(false);
            foreach (var r in rows) result[r.NovelId] = r.Id;
        }
    }

    private sealed class EpisodeRef
    {
        public int NovelId { get; set; }
        public int Id { get; set; }
    }

    public async Task InsertAllAsync(IEnumerable<Episode> episodes)
    {
        await EnsureAsync().ConfigureAwait(false);
        await _db.InsertAllAsync(episodes).ConfigureAwait(false);
    }

    /// <summary>
    /// site_episode_id が未設定の既存話(列追加前に保存された旧データ)へ、新鮮な TOC から導出した
    /// サイト話 ID を補完する。ドリフト(序盤話の削除/並べ替え)後の誤補完を避けるため、同一 episode_no の
    /// <b>タイトルが一致する話だけ</b>更新する(不一致=ドリフト疑い→触らない)。既に設定済みの話は更新しない。
    /// SiteEpisodeId を持つ話が新鮮リストに無い場合(Narou 等)は何もしない。best-effort。
    /// </summary>
    public async Task BackfillSiteEpisodeIdsAsync(int novelId, IReadOnlyList<Episode> freshEpisodes)
    {
        if (freshEpisodes.Count == 0 || freshEpisodes.All(e => string.IsNullOrEmpty(e.SiteEpisodeId))) return;
        await EnsureAsync().ConfigureAwait(false);

        var existing = await GetByNovelIdAsync(novelId).ConfigureAwait(false);
        var byNo = existing
            .Where(e => string.IsNullOrEmpty(e.SiteEpisodeId)) // 未補完の話のみ対象
            .ToDictionary(e => e.EpisodeNo);

        var updates = new List<(string siteId, int id)>();
        foreach (var fresh in freshEpisodes)
        {
            if (string.IsNullOrEmpty(fresh.SiteEpisodeId)) continue;
            if (!byNo.TryGetValue(fresh.EpisodeNo, out var db)) continue;
            if (db.Title != fresh.Title) continue; // タイトル不一致=ドリフト疑い→誤補完しない
            updates.Add((fresh.SiteEpisodeId!, db.Id));
        }
        if (updates.Count == 0) return;

        await _db.RunInTransactionAsync(conn =>
        {
            foreach (var (siteId, id) in updates)
            {
                conn.Execute(
                    "UPDATE episodes SET site_episode_id = ? WHERE id = ? AND site_episode_id IS NULL",
                    siteId, id);
            }
        }).ConfigureAwait(false);
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
