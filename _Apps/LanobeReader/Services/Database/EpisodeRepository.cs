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
        // ORM (Table<T>().Where().OrderBy().ToListAsync()) は LINQ 式木 → SQL コンパイルの
        // オーバーヘッドが乗るため、長尺小説 (1500+ 話) では raw SQL が体感で速い。
        // GetPagedByNovelIdAsync と同じ列順 / 列セットを維持。
        return await _db.QueryAsync<Episode>(
            "SELECT id, novel_id, episode_no, chapter_name, title, " +
            "is_read, read_at, published_at, is_favorite, favorited_at " +
            "FROM episodes WHERE novel_id = ? ORDER BY episode_no",
            novelId).ConfigureAwait(false);
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
            // episodes(novel_id, episode_no) に一意制約は無く、重複 episode_no 行があると
            // 同一 novel に複数行が返りうる。最小 id を採用して遷移先を決定的にする
            // (行順依存で通知タップ先がブレるのを防ぐ)。
            foreach (var r in rows)
            {
                if (!result.TryGetValue(r.NovelId, out var existing) || r.Id < existing)
                {
                    result[r.NovelId] = r.Id;
                }
            }
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
