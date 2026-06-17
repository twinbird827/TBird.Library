using LanobeReader.Helpers;
using LanobeReader.Models;
using SQLite;
using TBird.Core;
using TBird.Maui.DB;

namespace LanobeReader.Services.Database;

public class DatabaseService : SqliteDatabaseBase
{
    private const int CURRENT_SCHEMA_VERSION = 4;

    public DatabaseService()
        : base(Path.Combine(FileSystem.AppDataDirectory, "lanobereader.db"), CURRENT_SCHEMA_VERSION)
    {
    }

    protected override async Task CreateTablesAsync(SQLiteAsyncConnection conn)
    {
        // 0. 並行アクセス対策。前面UI・背景更新チェック(FGS/Worker)・プリフェッチが同一接続へ
        //    書き込むため、競合時の即時 "database is locked" を抑止する。WAL は DB ファイルに
        //    永続化され読取と書込の並行性を上げ、busy_timeout は競合時に最大 5 秒待機する。
        //    冪等(毎起動の再適用は無害)。
        await conn.ExecuteAsync("PRAGMA journal_mode=WAL").ConfigureAwait(false);
        await conn.ExecuteAsync("PRAGMA busy_timeout=5000").ConfigureAwait(false);

        // 1. CreateTable は冪等。v0 の新規インストール時の初期化も兼ねる。
        await conn.CreateTableAsync<Novel>().ConfigureAwait(false);
        await conn.CreateTableAsync<Episode>().ConfigureAwait(false);
        await conn.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
        await conn.CreateTableAsync<AppSetting>().ConfigureAwait(false);

        // 2. 既存カラム追加（新規カラムの後方互換）
        await EnsureColumnAsync(conn, "novels", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "novels", "favorited_at", "TEXT NULL").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "novels", "last_checked_at", "TEXT NULL").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "episodes", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "episodes", "favorited_at", "TEXT NULL").ConfigureAwait(false);

        // 3. novels の UNIQUE 制約（v1 時点で既に整備済みなので再適用するだけ）
        await conn.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_novels_site_novel ON novels (site_type, novel_id)"
        ).ConfigureAwait(false);
    }

    protected override async Task SeedAsync(SQLiteAsyncConnection conn)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.CACHE_MONTHS]          = SettingsKeys.DEFAULT_CACHE_MONTHS.ToString(),
            [SettingsKeys.UPDATE_INTERVAL_HOURS] = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString(),
            [SettingsKeys.FONT_SIZE_SP]          = SettingsKeys.DEFAULT_FONT_SIZE_SP.ToString(),
            [SettingsKeys.BACKGROUND_THEME]      = SettingsKeys.DEFAULT_BACKGROUND_THEME.ToString(),
            [SettingsKeys.LINE_SPACING]          = SettingsKeys.DEFAULT_LINE_SPACING.ToString(),
            [SettingsKeys.EPISODES_PER_PAGE]     = SettingsKeys.DEFAULT_EPISODES_PER_PAGE.ToString(),
            [SettingsKeys.PREFETCH_ENABLED]      = SettingsKeys.DEFAULT_PREFETCH_ENABLED.ToString(),
            [SettingsKeys.REQUEST_DELAY_MS]      = SettingsKeys.DEFAULT_REQUEST_DELAY_MS.ToString(),
            [SettingsKeys.VERTICAL_WRITING]      = SettingsKeys.DEFAULT_VERTICAL_WRITING.ToString(),
            [SettingsKeys.NOVEL_SORT_KEY]        = SettingsKeys.DEFAULT_NOVEL_SORT_KEY,
            [SettingsKeys.LAST_SCHEDULED_HOURS]  = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString(),
            [SettingsKeys.AUTO_MARK_READ_ENABLED] = SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED.ToString(),
        };

        foreach (var (key, value) in defaults)
        {
            var existing = await conn.FindAsync<AppSetting>(key).ConfigureAwait(false);
            if (existing is null)
            {
                await conn.InsertAsync(new AppSetting { Key = key, Value = value }).ConfigureAwait(false);
            }
        }
    }

    protected override IReadOnlyList<IMigration> GetMigrations()
        => new IMigration[] { new MigrateToV2(), new MigrateToV3(), new MigrateToV4() };

    protected override async Task<int> ReadSchemaVersionAsync(SQLiteAsyncConnection conn)
    {
        try
        {
            var row = await conn.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
            if (row is null) return 1; // 未設定は v1 扱い（既存リリースは v1 で動作してきた）
            return int.TryParse(row.Value, out var v) ? v : 1;
        }
        catch
        {
            return 1;
        }
    }

    protected override async Task WriteSchemaVersionAsync(SQLiteAsyncConnection conn, int version)
    {
        var existing = await conn.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
        if (existing is null)
        {
            await conn.InsertAsync(
                new AppSetting { Key = "schema_version", Value = version.ToString() }
            ).ConfigureAwait(false);
        }
        else
        {
            existing.Value = version.ToString();
            await conn.UpdateAsync(existing).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// v1 → v2: episodes(novel_id, episode_no) を UNIQUE 化。
    /// 既存の非UNIQUEインデックス idx_episodes_novel_episode を DROP してから
    /// 重複レコードを除去し、UNIQUE インデックスを貼り直す。
    /// 重複の episode_cache も連鎖削除。
    /// </summary>
    private class MigrateToV2 : IMigration
    {
        public int FromVersion => 1;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_episodes_novel_episode")
                    .ConfigureAwait(false);

                var dupCount = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM (" +
                    "  SELECT novel_id, episode_no FROM episodes " +
                    "  GROUP BY novel_id, episode_no HAVING COUNT(*) > 1" +
                    ")"
                ).ConfigureAwait(false);

                if (dupCount > 0)
                {
                    MessageService.Warn(
                        $"[MigrateToV2] Found {dupCount} duplicate (novel_id, episode_no) groups. Deduping.");

                    // 孤立 cache 先 → episodes 後（FK なしのため手動順序管理）
                    await conn.ExecuteAsync(
                        "DELETE FROM episode_cache WHERE episode_id IN (" +
                        "  SELECT id FROM episodes WHERE id NOT IN (" +
                        "    SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                        "  )" +
                        ")"
                    ).ConfigureAwait(false);

                    var deleted = await conn.ExecuteAsync(
                        "DELETE FROM episodes WHERE id NOT IN (" +
                        "  SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                        ")"
                    ).ConfigureAwait(false);
                    MessageService.Info($"[MigrateToV2] Deleted {deleted} duplicate episode rows.");
                }

                await conn.ExecuteAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_novel_episode " +
                    "ON episodes (novel_id, episode_no)"
                ).ConfigureAwait(false);

                MessageService.Info("[MigrateToV2] Done.");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV2] Failed: {ex.Message}");
                throw; // 上位 (SqliteDatabaseBase) で WriteSchemaVersionAsync を skip させるため再送出
            }
        }
    }

    /// <summary>
    /// v2 → v3: episodes(novel_id, is_read) に複合インデックスを追加。
    /// NovelRepository.GetAllWithUnreadCountAsync の集計サブクエリ
    /// (episode_count / read_count / unread_count を 1 パスで GROUP BY) の
    /// covering index として機能する。
    /// </summary>
    private class MigrateToV3 : IMigration
    {
        public int FromVersion => 2;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_episodes_novel_isread " +
                    "ON episodes (novel_id, is_read)"
                ).ConfigureAwait(false);
                MessageService.Info("[MigrateToV3] Done.");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV3] Failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// v3 → v4: 更新チェック系クエリ向けのインデックスを整備。
    /// - episodes(novel_id, is_read, episode_no): GetDeepLinkTargetEpisodeIdsAsync の
    ///   相関サブクエリ MIN/MAX(episode_no) をインデックス端のシークで解決する covering index。
    ///   (novel_id, is_read) の上位互換のため旧 idx_episodes_novel_isread は DROP する。
    /// - novels(last_checked_at): GetAllForCheckAsync の「最終チェック古い順」ソートを索引化する
    ///   (NULL=未チェックが先頭に来るラウンドロビン順)。
    /// </summary>
    private class MigrateToV4 : IMigration
    {
        public int FromVersion => 3;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_episodes_novel_isread")
                    .ConfigureAwait(false);
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_episodes_novel_isread_epno " +
                    "ON episodes (novel_id, is_read, episode_no)"
                ).ConfigureAwait(false);
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_novels_last_checked " +
                    "ON novels (last_checked_at)"
                ).ConfigureAwait(false);
                MessageService.Info("[MigrateToV4] Done.");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV4] Failed: {ex.Message}");
                throw;
            }
        }
    }
}
