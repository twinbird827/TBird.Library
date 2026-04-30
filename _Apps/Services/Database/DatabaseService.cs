using LanobeReader.Helpers;
using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class DatabaseService
{
    private const int CURRENT_SCHEMA_VERSION = 2;

    private readonly SQLiteAsyncConnection _connection;
    private Task? _initTask;
    private readonly object _initLock = new();

    public DatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lanobereader.db");
        _connection = new SQLiteAsyncConnection(dbPath);
    }

    public SQLiteAsyncConnection Connection => _connection;

    /// <summary>
    /// 初回のみ実際の初期化を行う。複数箇所から呼ばれても1回しか走らない。
    /// </summary>
    public Task EnsureInitializedAsync()
    {
        lock (_initLock)
        {
            return _initTask ??= InitializeInternalAsync();
        }
    }

    public Task InitializeAsync() => EnsureInitializedAsync();

    private async Task InitializeInternalAsync()
    {
        // 1. CreateTable は冪等なので先に走らせる（v0 の新規インストール時の初期化も兼ねる）
        await _connection.CreateTableAsync<Novel>().ConfigureAwait(false);
        await _connection.CreateTableAsync<Episode>().ConfigureAwait(false);
        await _connection.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
        await _connection.CreateTableAsync<AppSetting>().ConfigureAwait(false);

        // 2. 既存カラム追加（新規カラムの後方互換）
        await EnsureColumnAsync("novels", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync("novels", "favorited_at", "TEXT NULL").ConfigureAwait(false);
        await EnsureColumnAsync("episodes", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync("episodes", "favorited_at", "TEXT NULL").ConfigureAwait(false);

        // 3. novels の UNIQUE 制約（v1 時点で既に整備済みなので再適用するだけ）
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_novels_site_novel ON novels (site_type, novel_id)"
        ).ConfigureAwait(false);

        // 4. 既定設定のシード
        await SeedSettingsAsync().ConfigureAwait(false);

        // 5. schema_version を読み、必要なマイグレーションを順番に適用
        var currentVersion = await GetSchemaVersionAsync().ConfigureAwait(false);
        if (currentVersion < CURRENT_SCHEMA_VERSION)
        {
            LogHelper.Info(nameof(DatabaseService),
                $"Schema migration: v{currentVersion} → v{CURRENT_SCHEMA_VERSION}");
            try
            {
                await MigrateAsync(currentVersion).ConfigureAwait(false);
                await SetSchemaVersionAsync(CURRENT_SCHEMA_VERSION).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // migration 失敗時は version を上げずに継続。次回起動で再試行される。
                LogHelper.Warn(nameof(DatabaseService),
                    $"Schema migration failed, will retry next launch: {ex.Message}");
            }
        }
    }

    private async Task EnsureColumnAsync(string table, string column, string ddlSuffix)
    {
        try
        {
            var cols = await _connection.QueryAsync<PragmaColumnInfo>($"PRAGMA table_info({table})").ConfigureAwait(false);
            if (cols.Any(c => string.Equals(c.name, column, StringComparison.OrdinalIgnoreCase))) return;
            await _connection.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {ddlSuffix}").ConfigureAwait(false);
            LogHelper.Info(nameof(DatabaseService), $"Added column {table}.{column}");
        }
        catch (Exception ex)
        {
            LogHelper.Warn(nameof(DatabaseService), $"EnsureColumnAsync {table}.{column} failed: {ex.Message}");
        }
    }

    private class PragmaColumnInfo
    {
        public int cid { get; set; }
        public string name { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public int notnull { get; set; }
        public string? dflt_value { get; set; }
        public int pk { get; set; }
    }

    private async Task SeedSettingsAsync()
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
        };

        foreach (var (key, value) in defaults)
        {
            var existing = await _connection.FindAsync<AppSetting>(key).ConfigureAwait(false);
            if (existing is null)
            {
                await _connection.InsertAsync(new AppSetting { Key = key, Value = value }).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> GetSchemaVersionAsync()
    {
        try
        {
            var row = await _connection.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
            if (row is null) return 1; // 未設定は v1 扱い（既存リリースは v1 で動作してきた）
            return int.TryParse(row.Value, out var v) ? v : 1;
        }
        catch
        {
            return 1;
        }
    }

    private async Task SetSchemaVersionAsync(int version)
    {
        var existing = await _connection.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
        if (existing is null)
        {
            await _connection.InsertAsync(
                new AppSetting { Key = "schema_version", Value = version.ToString() }
            ).ConfigureAwait(false);
        }
        else
        {
            existing.Value = version.ToString();
            await _connection.UpdateAsync(existing).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// スキーマ version を fromVersion から CURRENT_SCHEMA_VERSION まで順次引き上げる。
    /// 新しい migration を追加する場合は、対応する if 分岐を足し、CURRENT_SCHEMA_VERSION を +1 すること。
    /// </summary>
    private async Task MigrateAsync(int fromVersion)
    {
        if (fromVersion < 2)
        {
            await MigrateToV2Async().ConfigureAwait(false);
        }
        // 今後のバージョンは↓に追加
        // if (fromVersion < 3) await MigrateToV3Async().ConfigureAwait(false);
    }

    /// <summary>
    /// v1 → v2: episodes(novel_id, episode_no) を UNIQUE 化。
    /// 既存の非UNIQUEインデックス idx_episodes_novel_episode を DROP してから
    /// 重複レコードを除去し、UNIQUE インデックスを貼り直す。
    /// 重複の episode_cache も連鎖削除。
    /// </summary>
    private async Task MigrateToV2Async()
    {
        try
        {
            await _connection.ExecuteAsync("DROP INDEX IF EXISTS idx_episodes_novel_episode")
                .ConfigureAwait(false);

            var dupCount = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM (" +
                "  SELECT novel_id, episode_no FROM episodes " +
                "  GROUP BY novel_id, episode_no HAVING COUNT(*) > 1" +
                ")"
            ).ConfigureAwait(false);

            if (dupCount > 0)
            {
                LogHelper.Warn(nameof(DatabaseService),
                    $"[MigrateToV2] Found {dupCount} duplicate (novel_id, episode_no) groups. Deduping.");

                // 孤立 cache 先 → episodes 後（FK なしのため手動順序管理）
                await _connection.ExecuteAsync(
                    "DELETE FROM episode_cache WHERE episode_id IN (" +
                    "  SELECT id FROM episodes WHERE id NOT IN (" +
                    "    SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                    "  )" +
                    ")"
                ).ConfigureAwait(false);

                var deleted = await _connection.ExecuteAsync(
                    "DELETE FROM episodes WHERE id NOT IN (" +
                    "  SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                    ")"
                ).ConfigureAwait(false);
                LogHelper.Info(nameof(DatabaseService),
                    $"[MigrateToV2] Deleted {deleted} duplicate episode rows.");
            }

            await _connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_novel_episode " +
                "ON episodes (novel_id, episode_no)"
            ).ConfigureAwait(false);

            LogHelper.Info(nameof(DatabaseService), "[MigrateToV2] Done.");
        }
        catch (Exception ex)
        {
            LogHelper.Warn(nameof(DatabaseService), $"[MigrateToV2] Failed: {ex.Message}");
            throw; // 上位 (InitializeInternalAsync) で SetSchemaVersion を skip させるため再送出
        }
    }
}
