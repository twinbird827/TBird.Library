using Android.Graphics;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;
using Rect = Android.Graphics.Rect;

namespace ComicViewer.Comics;

public static class BitmapUtil
{
    public static PageSize? DecodeBounds(Stream stream)
    {
        var options = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeStream(stream, null, options);
        return options.OutWidth > 0 && options.OutHeight > 0 ? new PageSize(options.OutWidth, options.OutHeight) : null;
    }

    public static Bitmap Decode(Stream stream, int sampleSize)
    {
        var options = new BitmapFactory.Options { InSampleSize = sampleSize };
        return BitmapFactory.DecodeStream(stream, null, options) ?? throw new InvalidDataException("Decode failed");
    }

    /// <summary>表示領域に収めた解像度を下回らない最大の 2 のべき。</summary>
    public static int SampleSize(PageSize size, int reqWidth, int reqHeight)
    {
        var scale = Math.Min((double)reqWidth / size.Width, (double)reqHeight / size.Height);
        var sample = 1;
        while (sample * 2 <= 1 / scale) sample *= 2;
        return sample;
    }

    public static (int Width, int Height) Fit(int width, int height, int maxWidth, int maxHeight)
    {
        var scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
        return (Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
    }

    public static Bitmap ScaleToFit(Bitmap source, int maxWidth, int maxHeight)
    {
        var (w, h) = Fit(source.Width, source.Height, maxWidth, maxHeight);
        return w >= source.Width ? source : Bitmap.CreateScaledBitmap(source, w, h, true);
    }

    public static Bitmap CropHalf(Bitmap source, bool right)
    {
        var half = source.Width / 2;
        return right
            ? Bitmap.CreateBitmap(source, source.Width - half, 0, half, source.Height)
            : Bitmap.CreateBitmap(source, 0, 0, source.Width - half, source.Height);
    }

    /// <summary>右に right、左に left を高さを揃えて並べる。</summary>
    public static Bitmap ComposeSpread(Bitmap right, Bitmap left)
    {
        var height = Math.Max(right.Height, left.Height);
        var rw = right.Width * height / right.Height;
        var lw = left.Width * height / left.Height;
        var result = Bitmap.CreateBitmap(rw + lw, height, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(result);
        canvas.DrawColor(Color.Black);
        using var paint = new Paint(PaintFlags.FilterBitmap);
        canvas.DrawBitmap(left, null, new Rect(0, 0, lw, height), paint);
        canvas.DrawBitmap(right, null, new Rect(lw, 0, lw + rw, height), paint);
        return result;
    }
}
