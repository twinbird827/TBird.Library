using Android.Graphics;
using ComicViewer.Comics;
using ComicViewer.Models;
using ComicViewer.Sources;
using TBird.Core;

namespace ComicViewer.Services;

public class ThumbnailService
{
    private const int MaxSide = 200;
    private const int LocalWorkers = 4;

    private readonly PdfCacheService _pdfCache;

    public ThumbnailService(PdfCacheService pdfCache) => _pdfCache = pdfCache;

    public static string GetPath(ComicRef comic)
        => System.IO.Path.Combine(PdfCacheService.ThumbDirectory, PdfCacheService.Hash(comic.SourceKey, comic.Path, comic.Size, comic.ModifiedAt) + ".jpg");

    /// <summary>
    /// items のうちサムネイル未生成のものを、表示中の先頭（firstVisible）以降を優先して作る。
    /// 生成できたら completed(index, ファイルパス) を呼ぶ。items の null は対象外（フォルダ）。
    /// </summary>
    public async Task RunAsync(IFileSource source, IReadOnlyList<ComicRef?> items, Func<int> firstVisible, Action<int, string> completed, CancellationToken ct)
    {
        Directory.CreateDirectory(PdfCacheService.ThumbDirectory);
        var pending = new SortedSet<int>(Enumerable.Range(0, items.Count).Where(i => items[i] is not null));
        int? Next()
        {
            lock (pending)
            {
                if (pending.Count == 0) return null;
                var from = Math.Max(0, firstVisible());
                var view = pending.GetViewBetween(from, int.MaxValue);
                var i = view.Count > 0 ? view.Min : pending.Min;
                pending.Remove(i);
                return i;
            }
        }

        async Task WorkAsync()
        {
            while (!ct.IsCancellationRequested && Next() is { } index)
            {
                var comic = items[index]!;
                try
                {
                    var file = GetPath(comic);
                    if (File.Exists(file) || await CreateAsync(source, comic, file, ct).ConfigureAwait(false))
                    {
                        completed(index, file);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    MessageService.Warn($"Thumbnail failed: {comic.Path} {ex.Message}");
                }
            }
        }

        // SMB は接続ロックで直列化されるため並列にしても速くならない。
        var workers = source.IsRemote ? 1 : LocalWorkers;
        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(WorkAsync, ct))).ConfigureAwait(false);
    }

    private async Task<bool> CreateAsync(IFileSource source, ComicRef comic, string file, CancellationToken ct)
    {
        string? pdfPath = null;
        if (comic.Kind == ComicKind.Pdf && source.IsRemote)
        {
            // 全体ダウンロードを避けるため、キャッシュ済みのときだけ作る。
            pdfPath = await _pdfCache.FindAsync(comic).ConfigureAwait(false);
            if (pdfPath is null) return false;
        }

        using var reader = await ComicReaderFactory.OpenAsync(source, comic, pdfPath, ct).ConfigureAwait(false);
        var decoded = await reader.DecodeAsync(0, MaxSide, MaxSide, ct).ConfigureAwait(false);
        var scaled = BitmapUtil.ScaleToFit(decoded, MaxSide, MaxSide);
        try
        {
            var part = file + ".part";
            await using (var stream = new FileStream(part, FileMode.Create, FileAccess.Write))
            {
                scaled.Compress(Bitmap.CompressFormat.Jpeg!, 80, stream);
            }
            File.Move(part, file, overwrite: true);
            return true;
        }
        finally
        {
            if (!ReferenceEquals(scaled, decoded)) scaled.Recycle();
            decoded.Recycle();
        }
    }
}
