using System.Collections.Concurrent;
using Android.Graphics;
using TBird.Core;

namespace ComicViewer.Comics;

/// <summary>ページが画像ファイルの漫画（zip・フォルダ）の共通部分。</summary>
public abstract class ImageComicReader : TBirdObject, IComicReader
{
    private readonly ConcurrentDictionary<int, PageSize> _sizes = new();

    protected ImageComicReader(IReadOnlyList<string> pages) => Pages = pages;

    /// <summary>ページの画像パス（自然順）。</summary>
    public IReadOnlyList<string> Pages { get; }

    public int PageCount => Pages.Count;

    /// <summary>ページのストリームを read に渡す。read はバックグラウンドスレッドで呼ばれる。</summary>
    protected abstract Task<T> ReadPageAsync<T>(int index, Func<Stream, T> read, CancellationToken ct);

    public async Task<PageSize?> GetPageSizeAsync(int index, CancellationToken ct)
    {
        if (_sizes.TryGetValue(index, out var cached)) return cached;
        var size = await ReadPageAsync(index, BitmapUtil.DecodeBounds, ct).ConfigureAwait(false);
        if (size is { } s) _sizes[index] = s;
        return size;
    }

    public async Task<Bitmap> DecodeAsync(int index, int reqWidth, int reqHeight, CancellationToken ct)
    {
        var size = await GetPageSizeAsync(index, ct).ConfigureAwait(false) ?? throw new InvalidDataException($"Not an image: {Pages[index]}");
        var sample = BitmapUtil.SampleSize(size, reqWidth, reqHeight);
        return await ReadPageAsync(index, s => BitmapUtil.Decode(s, sample), ct).ConfigureAwait(false);
    }
}
