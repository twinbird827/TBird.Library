namespace ComicViewer.Logic;

public enum DisplayUnitKind
{
    Full,
    RightHalf,
    LeftHalf,
    Single,
    Spread,
}

/// <summary>Spread の First は右側（若い方）の元ページ、Second は左側。</summary>
public readonly record struct DisplayUnit(DisplayUnitKind Kind, int First, int Second = -1)
{
    public int Representative => First;

    public bool Contains(int page) => First == page || Second == page;
}

public static class DisplayUnitBuilder
{
    public static List<DisplayUnit> Build(IReadOnlyList<bool> isWide, bool landscapeScreen)
    {
        var units = new List<DisplayUnit>();
        if (!landscapeScreen)
        {
            for (var i = 0; i < isWide.Count; i++)
            {
                if (isWide[i])
                {
                    units.Add(new DisplayUnit(DisplayUnitKind.RightHalf, i));
                    units.Add(new DisplayUnit(DisplayUnitKind.LeftHalf, i));
                }
                else
                {
                    units.Add(new DisplayUnit(DisplayUnitKind.Full, i));
                }
            }
            return units;
        }

        var p = 0;
        while (p < isWide.Count)
        {
            if (!isWide[p] && p + 1 < isWide.Count && !isWide[p + 1])
            {
                units.Add(new DisplayUnit(DisplayUnitKind.Spread, p, p + 1));
                p += 2;
            }
            else
            {
                units.Add(new DisplayUnit(DisplayUnitKind.Single, p));
                p++;
            }
        }
        return units;
    }

    /// <summary>元ページを含む最初の単位の位置。見つからなければ 0。</summary>
    public static int IndexOfPage(IReadOnlyList<DisplayUnit> units, int page)
    {
        for (var i = 0; i < units.Count; i++)
        {
            if (units[i].Contains(page)) return i;
        }
        return 0;
    }

    /// <summary>再構築後も同じ単位が残っていればそれを、無ければ同じ元ページを含む単位を返す。</summary>
    public static int IndexOfUnit(IReadOnlyList<DisplayUnit> units, DisplayUnit unit)
    {
        for (var i = 0; i < units.Count; i++)
        {
            if (units[i] == unit) return i;
        }
        return IndexOfPage(units, unit.Representative);
    }
}
