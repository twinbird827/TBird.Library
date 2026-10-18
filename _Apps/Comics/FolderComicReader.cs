using ComicViewer.Logic;
using ComicViewer.Sources;

namespace ComicViewer.Comics;

/// <summary>配下が画像だけのフォルダ（サブフォルダ含む）。</summary>
public class FolderComicReader : ImageComicReader
{
    private readonly IFileSource _source;

    protected FolderComicReader(IFileSource source, IReadOnlyList<string> pages) : base(pages) => _source = source;

    public static async Task<FolderComicReader> OpenAsync(IFileSource source, string path, CancellationToken ct)
    {
        var images = new List<string>();
        await CollectAsync(source, path, images, ct).ConfigureAwait(false);
        images.Sort((a, b) => NaturalComparer.Path.Compare(a[(path.Length)..], b[(path.Length)..]));
        return new FolderComicReader(source, images);
    }

    private static async Task CollectAsync(IFileSource source, string path, List<string> images, CancellationToken ct)
    {
        foreach (var entry in await source.ListAsync(path, ct).ConfigureAwait(false))
        {
            if (EntryRules.IsIgnored(entry.Name)) continue;
            var child = EntryRules.Combine(path, entry.Name);
            if (entry.IsDirectory) await CollectAsync(source, child, images, ct).ConfigureAwait(false);
            else if (EntryRules.IsImage(entry.Name)) images.Add(child);
        }
    }

    protected override async Task<T> ReadPageAsync<T>(int index, Func<Stream, T> read, CancellationToken ct)
    {
        await using var stream = await _source.OpenReadAsync(Pages[index], ct).ConfigureAwait(false);
        return await Task.Run(() => read(stream), ct).ConfigureAwait(false);
    }
}

/// <summary>漫画でないフォルダの直下の画像だけをまとめた仮想の 1 冊。</summary>
public class DirectImagesComicReader : FolderComicReader
{
    private DirectImagesComicReader(IFileSource source, IReadOnlyList<string> pages) : base(source, pages)
    {
    }

    public static new async Task<DirectImagesComicReader> OpenAsync(IFileSource source, string folderPath, CancellationToken ct)
    {
        var images = (await source.ListAsync(folderPath, ct).ConfigureAwait(false))
            .Where(x => !x.IsDirectory && !EntryRules.IsIgnored(x.Name) && EntryRules.IsImage(x.Name))
            .Select(x => x.Name)
            .OrderBy(x => x, NaturalComparer.Instance)
            .Select(x => EntryRules.Combine(folderPath, x))
            .ToList();
        return new DirectImagesComicReader(source, images);
    }
}
