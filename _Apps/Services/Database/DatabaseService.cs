using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _connection;

    public DatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lanobereader.db");
        _connection = new SQLiteAsyncConnection(dbPath);
    }

    public SQLiteAsyncConnection Connection => _connection;

    public async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<Novel>().ConfigureAwait(false);
        await _connection.CreateTableAsync<Episode>().ConfigureAwait(false);
        await _connection.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
        await _connection.CreateTableAsync<AppSetting>().ConfigureAwait(false);

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
