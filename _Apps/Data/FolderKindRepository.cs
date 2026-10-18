using ComicViewer.Models;

namespace ComicViewer.Data;

public class FolderKindRepository
{
    private readonly ComicViewerDatabase _db;

    public FolderKindRepository(ComicViewerDatabase db) => _db = db;

    public async Task<Dictionary<string, int>> GetChildrenAsync(string sourceKey, IEnumerable<string> paths)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        var set = paths.ToHashSet();
        var rows = await _db.Connection.Table<FolderKind>().Where(x => x.SourceKey == sourceKey).ToListAsync().ConfigureAwait(false);
        return rows.Where(x => set.Contains(x.Path)).ToDictionary(x => x.Path, x => x.Kind);
    }

    public async Task SetAsync(string sourceKey, string path, int kind)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        await _db.Connection.ExecuteAsync(
            "INSERT OR REPLACE INTO FolderKind (SourceKey, Path, Kind) VALUES (?, ?, ?)", sourceKey, path, kind).ConfigureAwait(false);
    }

    /// <summary>path 配下（path 自身は含まない）を削除する。ルート（空文字）なら読み込み元の全件。</summary>
    public async Task DeleteUnderAsync(string sourceKey, string path)
    {
        await _db.EnsureInitializedAsync().ConfigureAwait(false);
        if (path.Length == 0)
        {
            await _db.Connection.ExecuteAsync("DELETE FROM FolderKind WHERE SourceKey = ?", sourceKey).ConfigureAwait(false);
            return;
        }
        // LIKE はワイルドカード（% _）を含むフォルダ名で誤一致するため前方一致を substr で比べる。
        var prefix = path + "/";
        await _db.Connection.ExecuteAsync(
            "DELETE FROM FolderKind WHERE SourceKey = ? AND substr(Path, 1, ?) = ?", sourceKey, prefix.Length, prefix).ConfigureAwait(false);
    }
}
