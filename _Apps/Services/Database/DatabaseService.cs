using LanobeReader.Helpers;
using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class DatabaseService
{
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
        await _connection.CreateTableAsync<Novel>().ConfigureAwait(false);
        await _connection.CreateTableAsync<Episode>().ConfigureAwait(false);
        await _connection.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
        await _connection.CreateTableAsync<AppSetting>().ConfigureAwait(false);

        // Simple migration: ensure new columns exist on pre-existing tables
        await EnsureColumnAsync("novels", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync("novels", "favorited_at", "TEXT NULL").ConfigureAwait(false);
        await EnsureColumnAsync("episodes", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync("episodes", "favorited_at", "TEXT NULL").ConfigureAwait(false);

        // composite index for episodes (novel_id, episode_no)
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS idx_episodes_novel_episode ON episodes (novel_id, episode_no)"
        ).ConfigureAwait(false);

        // unique constraint on novels (site_type, novel_id)
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_novels_site_novel ON novels (site_type, novel_id)"
        ).ConfigureAwait(false);

        await SeedSettingsAsync().ConfigureAwait(false);
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
            ["cache_months"] = "3",
            ["update_interval_hours"] = "6",
            ["font_size_sp"] = "16",
            ["background_theme"] = "0",
            ["line_spacing"] = "1",
            ["episodes_per_page"] = "50",
            ["prefetch_enabled"] = "1",
            ["request_delay_ms"] = "800",
            ["vertical_writing"] = "0",
            ["novel_sort_key"] = "updated_desc",
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
}
