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

    // cacheable 不変条件(誤話本文を恒久キャッシュしない)を迂回されないよう private 化する。外部からの
    // キャッシュ書き込みは必ず UpsertIfCacheableAsync 経由とし、保存判定の入口を 1 つに固定する。
    private async Task<int> InsertAsync(EpisodeCache cache)
    {
        await EnsureAsync().ConfigureAwait(false);
        // episode_id は UNIQUE。Reader の直読みとバックグラウンド先読み(BackgroundJobQueue)が同一話を
        // 同時に挿入しうる(各々 GetByEpisodeIdAsync で miss を確認してから Insert する check-then-insert の
        // レース)。"OR IGNORE" で衝突時は何もせず冪等化し、UNIQUE 例外で Reader 読込が失敗したり、
        // 先読みキューの連続失敗ブレーカーが誤作動して先読み全体が停止するのを防ぐ(先に入った内容を温存)。
        return await _db.InsertAsync(cache, "OR IGNORE").ConfigureAwait(false);
    }

    /// <summary>
    /// 取得本文を「キャッシュ可なら」永続化する。cacheable=false(位置依存フォールバックで取得した誤話可能性
    /// のある本文)は破棄する不変条件を 1 箇所へ固定する。EpisodeCache の構築/タイムスタンプもここへ集約。
    /// </summary>
    public Task UpsertIfCacheableAsync(int episodeId, string content, bool cacheable)
    {
        if (!cacheable) return Task.CompletedTask;
        return InsertAsync(new EpisodeCache
        {
            EpisodeId = episodeId,
            Content = content,
            CachedAt = DateTime.UtcNow.ToString("o"),
        });
    }

    public async Task DeleteByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        await _db.ExecuteAsync(
            "DELETE FROM episode_cache WHERE episode_id IN (SELECT id FROM episodes WHERE novel_id = ?)",
            novelId
        ).ConfigureAwait(false);
    }

    internal void DeleteByNovelIdSync(SQLiteConnection conn, int novelId)
    {
        conn.Execute(
            "DELETE FROM episode_cache WHERE episode_id IN (SELECT id FROM episodes WHERE novel_id = ?)",
            novelId);
    }

    public async Task DeleteAllAsync()
    {
        await EnsureAsync().ConfigureAwait(false);
        await _db.DeleteAllAsync<EpisodeCache>().ConfigureAwait(false);
    }

    public async Task<HashSet<int>> GetCachedEpisodeIdsAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        // INNER JOIN は JOIN プランニングのオーバーヘッドが乗るため、IN サブクエリに置き換える。
        // 内側のサブクエリは idx_episodes_novel_episode (novel_id, episode_no) を使った
        // index-only スキャンで id 集合を取得、外側は episode_cache の PK lookup。
        var rows = await _db.QueryAsync<CachedIdRow>(
            "SELECT episode_id AS EpisodeId FROM episode_cache " +
            "WHERE episode_id IN (SELECT id FROM episodes WHERE novel_id = ?)",
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
