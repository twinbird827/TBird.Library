using ComicViewer.Models;
using ComicViewer.Sources;

namespace ComicViewer.Comics;

public static class ComicReaderFactory
{
    /// <summary>pdfLocalPath は SMB の pdf をキャッシュへ落としたファイル。ページ 0 枚なら InvalidDataException。</summary>
    public static async Task<IComicReader> OpenAsync(IFileSource source, ComicRef comic, string? pdfLocalPath, CancellationToken ct)
    {
        IComicReader reader = comic.Kind switch
        {
            ComicKind.Zip => await ZipComicReader.OpenAsync(await source.OpenReadAsync(comic.Path, ct).ConfigureAwait(false), ct).ConfigureAwait(false),
            ComicKind.Pdf => await PdfComicReader.OpenAsync(
                pdfLocalPath ?? ((LocalFileSource)source).GetFullPath(comic.Path), ct).ConfigureAwait(false),
            ComicKind.ImageFolder => await FolderComicReader.OpenAsync(source, comic.Path, ct).ConfigureAwait(false),
            _ => await DirectImagesComicReader.OpenAsync(source, comic.TargetPath, ct).ConfigureAwait(false),
        };
        if (reader.PageCount == 0)
        {
            reader.Dispose();
            throw new InvalidDataException($"No pages: {comic.Path}");
        }
        return reader;
    }
}
