using Android.Graphics;

namespace ComicViewer.Comics;

public readonly record struct PageSize(int Width, int Height)
{
    public bool IsWide => Width > Height;
}

public interface IComicReader : IDisposable
{
    int PageCount { get; }

    /// <summary>取得できなければ null。</summary>
    Task<PageSize?> GetPageSizeAsync(int index, CancellationToken ct);

    /// <summary>reqWidth × reqHeight に縦横比を保って収めたときの解像度以上の Bitmap。</summary>
    Task<Bitmap> DecodeAsync(int index, int reqWidth, int reqHeight, CancellationToken ct);
}
