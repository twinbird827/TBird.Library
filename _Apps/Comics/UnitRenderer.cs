using Android.Graphics;
using ComicViewer.Logic;

namespace ComicViewer.Comics;

public static class UnitRenderer
{
    /// <summary>表示単位を screenWidth × screenHeight（px）に収める 1 枚の Bitmap にする。</summary>
    public static async Task<Bitmap> RenderAsync(IComicReader reader, DisplayUnit unit, int screenWidth, int screenHeight, CancellationToken ct)
    {
        switch (unit.Kind)
        {
            case DisplayUnitKind.RightHalf:
            case DisplayUnitKind.LeftHalf:
                // 半分の幅が画面に収まる前提で解像度を決める。
                using (var page = await reader.DecodeAsync(unit.First, screenWidth * 2, screenHeight, ct).ConfigureAwait(false))
                {
                    var half = BitmapUtil.CropHalf(page, unit.Kind == DisplayUnitKind.RightHalf);
                    page.Recycle();
                    return half;
                }
            case DisplayUnitKind.Spread:
                using (var right = await reader.DecodeAsync(unit.First, screenWidth / 2, screenHeight, ct).ConfigureAwait(false))
                using (var left = await reader.DecodeAsync(unit.Second, screenWidth / 2, screenHeight, ct).ConfigureAwait(false))
                {
                    var spread = BitmapUtil.ComposeSpread(right, left);
                    right.Recycle();
                    left.Recycle();
                    return spread;
                }
            default:
                return await reader.DecodeAsync(unit.First, screenWidth, screenHeight, ct).ConfigureAwait(false);
        }
    }
}
