using ComicViewer.Models;

namespace ComicViewer.Data;

public class PdfCacheRepository
{
    private readonly ComicViewerDatabase _db;

    public PdfCacheRepository(ComicViewerDatabase db) => _db = db;

    public async Task<PdfCache?> GetAsync(string sourceKey, string path)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Connection.Table<PdfCache>()
            .Where(x => x.SourceKey == sourceKey && x.Path == path).FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<List<PdfCache>> GetAllAsync()
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Connection.Table<PdfCache>().ToListAsync().ConfigureAwait(false);
    }

    public async Task UpsertAsync(PdfCache row)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.ExecuteAsync(
            "INSERT OR REPLACE INTO PdfCache (SourceKey, Path, Size, ModifiedAt, LocalFile, LastAccessedAt) VALUES (?, ?, ?, ?, ?, ?)",
            row.SourceKey, row.Path, row.Size, row.ModifiedAt, row.LocalFile, row.LastAccessedAt).ConfigureAwait(false);
    }

    public async Task TouchAsync(int id)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.ExecuteAsync("UPDATE PdfCache SET LastAccessedAt = ? WHERE Id = ?", ComicViewerDatabase.Now, id).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.DeleteAsync<PdfCache>(id).ConfigureAwait(false);
    }

    public async Task DeleteAllAsync()
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.DeleteAllAsync<PdfCache>().ConfigureAwait(false);
    }
}
