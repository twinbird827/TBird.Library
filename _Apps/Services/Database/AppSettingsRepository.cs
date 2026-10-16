using System.Collections.Concurrent;
using LanobeReader.Models;

namespace LanobeReader.Services.Database;

public class AppSettingsRepository(DatabaseService dbService)
{
    private readonly ConcurrentDictionary<string, string> _cache = [];
    private volatile bool _loaded;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    /// <summary>アプリ起動時に1回呼び出す。全設定値をメモリキャッシュする。</summary>
    public async Task LoadAllAsync()
    {
        if (_loaded) return;
        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_loaded) return;
            await dbService.EnsureInitializedAsync().ConfigureAwait(false);
            var rows = await dbService.Connection.Table<AppSetting>().ToListAsync().ConfigureAwait(false);
            foreach (var r in rows) _cache[r.Key] = r.Value;
            _loaded = true;
        }
        finally { _loadGate.Release(); }
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        if (!_loaded) await LoadAllAsync().ConfigureAwait(false);
        return _cache.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public async Task<int> GetIntValueAsync(string key, int defaultValue = 0)
    {
        var value = await GetValueAsync(key).ConfigureAwait(false);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task SetValueAsync(string key, string value)
    {
        if (!_loaded) await LoadAllAsync().ConfigureAwait(false);
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);
        var setting = await dbService.Connection.FindAsync<AppSetting>(key).ConfigureAwait(false);
        if (setting is not null)
        {
            setting.Value = value;
            await dbService.Connection.UpdateAsync(setting).ConfigureAwait(false);
        }
        else
        {
            await dbService.Connection.InsertAsync(new AppSetting { Key = key, Value = value }).ConfigureAwait(false);
        }
        _cache[key] = value;
    }
}
