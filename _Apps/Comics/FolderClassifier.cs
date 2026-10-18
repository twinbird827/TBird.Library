using ComicViewer.Logic;
using ComicViewer.Sources;

namespace ComicViewer.Comics;

public static class FolderClassifier
{
    public const int Folder = 0;
    public const int ImageComic = 1;

    /// <summary>配下（再帰）が画像だけで 1 枚以上なら ImageComic。画像以外を見つけた時点で打ち切る。</summary>
    public static async Task<int> ClassifyAsync(IFileSource source, string path, CancellationToken ct)
    {
        var images = await CountImagesAsync(source, path, ct).ConfigureAwait(false);
        return images > 0 ? ImageComic : Folder;
    }

    /// <summary>画像以外のファイルがあれば -1。</summary>
    private static async Task<int> CountImagesAsync(IFileSource source, string path, CancellationToken ct)
    {
        var count = 0;
        var children = new List<string>();
        foreach (var entry in await source.ListAsync(path, ct).ConfigureAwait(false))
        {
            if (EntryRules.IsIgnored(entry.Name)) continue;
            if (entry.IsDirectory) children.Add(EntryRules.Combine(path, entry.Name));
            else if (EntryRules.IsImage(entry.Name)) count++;
            else return -1;
        }
        foreach (var child in children)
        {
            var n = await CountImagesAsync(source, child, ct).ConfigureAwait(false);
            if (n < 0) return -1;
            count += n;
        }
        return count;
    }
}
