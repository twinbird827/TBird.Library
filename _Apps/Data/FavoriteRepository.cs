using ComicViewer.Models;

namespace ComicViewer.Data;

public class FavoriteRepository
{
    private readonly ComicViewerDatabase _db;

    public FavoriteRepository(ComicViewerDatabase db) => _db = db;

    public async Task<List<Favorite>> GetAllAsync()
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        return await _db.Connection.Table<Favorite>().OrderBy(x => x.CreatedAt).ToListAsync().ConfigureAwait(false);
    }

    /// <summary>登録済みなら false。</summary>
    public async Task<bool> AddAsync(string sourceKey, string path, string name)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        var exists = await _db.Connection.Table<Favorite>()
            .Where(x => x.SourceKey == sourceKey && x.Path == path).CountAsync().ConfigureAwait(false);
        if (exists > 0) return false;
        await _db.Connection.InsertAsync(new Favorite
        {
            SourceKey = sourceKey,
            Path = path,
            Name = name,
            CreatedAt = ComicViewerDatabase.Now,
        }).ConfigureAwait(false);
        return true;
    }

    public async Task DeleteAsync(int id)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.DeleteAsync<Favorite>(id).ConfigureAwait(false);
    }
}
