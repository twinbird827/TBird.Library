using ComicViewer.Models;

namespace ComicViewer.Data;

public class ReadingStateRepository
{
    private readonly ComicViewerDatabase _db;

    public ReadingStateRepository(ComicViewerDatabase db) => _db = db;

    public async Task<ReadingState?> GetAsync(string sourceKey, string path)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Connection.Table<ReadingState>()
            .Where(x => x.SourceKey == sourceKey && x.Path == path).FirstOrDefaultAsync().ConfigureAwait(false);
    }

    /// <summary>フォルダ一覧の状態表示用に、読み込み元の全行を Path 引きで返す。</summary>
    public async Task<Dictionary<string, ReadingState>> GetBySourceAsync(string sourceKey)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        var list = await _db.Connection.Table<ReadingState>().Where(x => x.SourceKey == sourceKey).ToListAsync().ConfigureAwait(false);
        return list.ToDictionary(x => x.Path);
    }

    public async Task<List<ReadingState>> GetHistoryAsync(int count)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Connection.Table<ReadingState>()
            .Where(x => x.LastOpenedAt != null).OrderByDescending(x => x.LastOpenedAt).Take(count).ToListAsync().ConfigureAwait(false);
    }

    public Task SaveProgressAsync(string sourceKey, string path, int pageIndex, int pageCount, bool markRead)
        => UpdateAsync(sourceKey, path, x =>
        {
            x.PageIndex = pageIndex;
            x.PageCount = pageCount;
            if (markRead) x.IsRead = true;
        });

    public Task TouchOpenedAsync(string sourceKey, string path)
        => UpdateAsync(sourceKey, path, x => x.LastOpenedAt = ComicViewerDatabase.Now);

    public Task SetReadAsync(string sourceKey, string path, bool isRead)
        => UpdateAsync(sourceKey, path, x =>
        {
            x.IsRead = isRead;
            if (!isRead) x.PageIndex = 0;
        });

    public async Task RemoveFromHistoryAsync(int id)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.ExecuteAsync("UPDATE ReadingState SET LastOpenedAt = NULL WHERE Id = ?", id).ConfigureAwait(false);
    }

    public async Task ClearHistoryAsync()
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.ExecuteAsync("UPDATE ReadingState SET LastOpenedAt = NULL").ConfigureAwait(false);
    }

    private async Task UpdateAsync(string sourceKey, string path, Action<ReadingState> update)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.RunInTransactionAsync(conn =>
        {
            var row = conn.Table<ReadingState>().FirstOrDefault(x => x.SourceKey == sourceKey && x.Path == path)
                ?? new ReadingState { SourceKey = sourceKey, Path = path };
            update(row);
            if (row.Id == 0) conn.Insert(row);
            else conn.Update(row);
        }).ConfigureAwait(false);
    }
}
