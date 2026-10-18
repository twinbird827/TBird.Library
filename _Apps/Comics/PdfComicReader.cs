using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using TBird.Core;
using Color = Android.Graphics.Color;

namespace ComicViewer.Comics;

public class PdfComicReader : TBirdObject, IComicReader
{
    private readonly ParcelFileDescriptor _descriptor;
    private readonly PdfRenderer _renderer;
    // PdfRenderer は同時に 1 ページしか開けない。
    private readonly SemaphoreSlim _gate = new(1, 1);

    private PdfComicReader(ParcelFileDescriptor descriptor, PdfRenderer renderer)
    {
        _descriptor = descriptor;
        _renderer = renderer;
        PageCount = renderer.PageCount;
    }

    public int PageCount { get; }

    /// <summary>パスワード付きなら Java.Lang.SecurityException。</summary>
    public static Task<PdfComicReader> OpenAsync(string localPath, CancellationToken ct) => Task.Run(() =>
    {
        var descriptor = ParcelFileDescriptor.Open(new Java.IO.File(localPath), ParcelFileMode.ReadOnly)
            ?? throw new FileNotFoundException(localPath);
        try
        {
            return new PdfComicReader(descriptor, new PdfRenderer(descriptor));
        }
        catch
        {
            descriptor.Close();
            throw;
        }
    }, ct);

    public Task<PageSize?> GetPageSizeAsync(int index, CancellationToken ct)
        => WithPageAsync<PageSize?>(index, page => new PageSize(page.Width, page.Height), ct);

    public Task<Bitmap> DecodeAsync(int index, int reqWidth, int reqHeight, CancellationToken ct) => WithPageAsync(index, page =>
    {
        var (w, h) = BitmapUtil.Fit(page.Width, page.Height, reqWidth, reqHeight);
        var bitmap = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888!);
        bitmap.EraseColor(Color.White);
        page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
        return bitmap;
    }, ct);

    private async Task<T> WithPageAsync<T>(int index, Func<PdfRenderer.Page, T> func, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await Task.Run(() =>
            {
                using var page = _renderer.OpenPage(index);
                try
                {
                    return func(page);
                }
                finally
                {
                    page.Close();
                }
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override void DisposeManagedResource()
    {
        _gate.Wait();
        try
        {
            _renderer.Close();
            _descriptor.Close();
        }
        finally
        {
            _gate.Release();
        }
        base.DisposeManagedResource();
    }
}
