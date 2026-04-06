using LanobeReader.Models;
using SQLite;

namespace LanobeReader.Services.Database;

public class AppSettingsRepository
{
    private readonly SQLiteAsyncConnection _db;

    public AppSettingsRepository(DatabaseService dbService)
    {
        _db = dbService.Connection;
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        var setting = await _db.FindAsync<AppSetting>(key).ConfigureAwait(false);
        return setting?.Value ?? defaultValue;
    }

    public async Task<int> GetIntValueAsync(string key, int defaultValue = 0)
    {
        var value = await GetValueAsync(key).ConfigureAwait(false);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task SetValueAsync(string key, string value)
    {
        var setting = await _db.FindAsync<AppSetting>(key).ConfigureAwait(false);
        if (setting is not null)
        {
            setting.Value = value;
            await _db.UpdateAsync(setting).ConfigureAwait(false);
        }
        else
        {
            await _db.InsertAsync(new AppSetting { Key = key, Value = value }).ConfigureAwait(false);
        }
    }
}
