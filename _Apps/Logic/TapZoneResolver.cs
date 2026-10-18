namespace ComicViewer.Logic;

public enum TapAction
{
    ShowOverlay,
    NextUnit,
    PrevUnit,
    FirstUnit,
    LastUnit,
    NextComic,
    PrevComic,
}

public static class TapZoneResolver
{
    // 右開き固定: 左側が「進む」、右側が「戻る」。
    private static readonly TapAction[,] Zones =
    {
        { TapAction.NextComic, TapAction.ShowOverlay, TapAction.PrevComic },
        { TapAction.NextUnit, TapAction.ShowOverlay, TapAction.PrevUnit },
        { TapAction.LastUnit, TapAction.ShowOverlay, TapAction.FirstUnit },
    };

    public static TapAction Resolve(double x, double y, double width, double height)
    {
        return Zones[Index(y, height), Index(x, width)];
    }

    private static int Index(double v, double length)
    {
        if (v < length / 3) return 0;
        return v < length * 2 / 3 ? 1 : 2;
    }
}
