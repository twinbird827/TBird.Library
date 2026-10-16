using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.Data.Database;

/// <summary>Series テーブルの Repository。各メソッド先頭で EnsureInitializedAsync を呼ぶ規約。</summary>
public sealed class SeriesRepository : ISeriesRepository
{
    private readonly NewReleaseDatabase _db;

    public SeriesRepository(NewReleaseDatabase db) => _db = db;

    public async Task<IReadOnlyList<Series>> GetAllAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Connection.Table<Series>().ToListAsync();
    }

    public async Task<Series?> GetAsync(int id)
    {
        await _db.EnsureInitializedAsync();
        return await _db.Connection.FindAsync<Series>(id);
    }

    public async Task<int> InsertAsync(Series series)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.InsertAsync(series);
        return series.Id; // InsertAsync 後に PK が採番される
    }

    public async Task UpdateAsync(Series series)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.UpdateAsync(series);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.DeleteAsync<Series>(id);
    }

    public async Task<IReadOnlyList<Series>> GetCheckTargetsAsync(int max)
    {
        await _db.EnsureInitializedAsync();
        // LastCheckedAt が NULL を最優先、次いで古い順
        return await _db.Connection.QueryAsync<Series>(
            "SELECT * FROM Series ORDER BY (LastCheckedAt IS NULL) DESC, LastCheckedAt ASC LIMIT ?", max);
    }

    public async Task TouchLastCheckedAsync(int seriesId, string isoNow)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.ExecuteAsync("UPDATE Series SET LastCheckedAt = ? WHERE Id = ?", isoNow, seriesId);
    }
}
