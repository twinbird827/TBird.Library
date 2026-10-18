using ComicViewer.Logic;
using ComicViewer.Models;

namespace ComicViewer.Data;

public class SmbSourceRepository
{
    private readonly ComicViewerDatabase _db;

    public SmbSourceRepository(ComicViewerDatabase db) => _db = db;

    public async Task<List<SmbSource>> GetAllAsync()
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        var list = await _db.Connection.Table<SmbSource>().ToListAsync().ConfigureAwait(false);
        return list.OrderBy(x => x.Name, NaturalComparer.Instance).ToList();
    }

    public async Task<SmbSource?> GetAsync(int id)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Connection.FindAsync<SmbSource>(id).ConfigureAwait(false);
    }

    public async Task SaveAsync(SmbSource source)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        if (source.Id == 0) await _db.Connection.InsertAsync(source).ConfigureAwait(false);
        else await _db.Connection.UpdateAsync(source).ConfigureAwait(false);
    }

    /// <summary>SMB 行と、同じ SourceKey を持つ全テーブルの行を削除する。</summary>
    public async Task DeleteAsync(SmbSource source)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        var key = source.SourceKey;
        await _db.Connection.RunInTransactionAsync(conn =>
        {
            conn.Delete<SmbSource>(source.Id);
            conn.Execute("DELETE FROM Favorite WHERE SourceKey = ?", key);
            conn.Execute("DELETE FROM ReadingState WHERE SourceKey = ?", key);
            conn.Execute("DELETE FROM FolderKind WHERE SourceKey = ?", key);
            conn.Execute("DELETE FROM PdfCache WHERE SourceKey = ?", key);
        }).ConfigureAwait(false);
    }
}
