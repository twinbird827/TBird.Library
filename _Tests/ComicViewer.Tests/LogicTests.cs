using ComicViewer.Logic;
using NUnit.Framework;

namespace ComicViewer.Tests;

public class NaturalComparerTests
{
    private static int Cmp(string a, string b) => Math.Sign(NaturalComparer.Instance.Compare(a, b));

    [Test]
    public void NumberIsComparedByValue() => Assert.That(Cmp("2巻", "10巻"), Is.EqualTo(-1));

    [Test]
    public void SameValueFewerDigitsFirst()
    {
        Assert.That(Cmp("1", "01"), Is.EqualTo(-1));
        Assert.That(Cmp("01", "1"), Is.EqualTo(1));
    }

    [Test]
    public void FullWidthDigitEqualsAscii()
    {
        Assert.That(Cmp("２巻", "10巻"), Is.EqualTo(-1));
        Assert.That(Cmp("第２話", "第3話"), Is.EqualTo(-1));
    }

    [Test]
    public void IgnoresCaseBeforeOrdinalTiebreak()
    {
        Assert.That(Cmp("a2", "B1"), Is.EqualTo(-1));
        Assert.That(Cmp("A", "a"), Is.Not.EqualTo(0));
    }

    [Test]
    public void ShorterPrefixFirst() => Assert.That(Cmp("abc", "abc1"), Is.EqualTo(-1));

    [Test]
    public void PathComparesPerSegment()
    {
        var list = new List<string> { "a 2/1.jpg", "a/10.jpg", "a/2.jpg" };
        list.Sort(NaturalComparer.Path);
        Assert.That(list, Is.EqualTo(new[] { "a/2.jpg", "a/10.jpg", "a 2/1.jpg" }));
    }
}

public class DisplayUnitBuilderTests
{
    private static readonly DisplayUnitKind F = DisplayUnitKind.Full;

    [Test]
    public void PortraitSplitsWidePageRightThenLeft()
    {
        var units = DisplayUnitBuilder.Build(new[] { false, true, false }, landscapeScreen: false);
        Assert.That(units, Is.EqualTo(new[]
        {
            new DisplayUnit(F, 0),
            new DisplayUnit(DisplayUnitKind.RightHalf, 1),
            new DisplayUnit(DisplayUnitKind.LeftHalf, 1),
            new DisplayUnit(F, 2),
        }));
    }

    [Test]
    public void LandscapeSpreadsAroundWidePage()
    {
        // 1, 2, 3(横長), 4, 5 → [1-2] [3] [4-5]
        var units = DisplayUnitBuilder.Build(new[] { false, false, true, false, false }, landscapeScreen: true);
        Assert.That(units, Is.EqualTo(new[]
        {
            new DisplayUnit(DisplayUnitKind.Spread, 0, 1),
            new DisplayUnit(DisplayUnitKind.Single, 2),
            new DisplayUnit(DisplayUnitKind.Spread, 3, 4),
        }));
    }

    [Test]
    public void LandscapeOddLastPageIsSingle()
    {
        var units = DisplayUnitBuilder.Build(new[] { false, false, false }, landscapeScreen: true);
        Assert.That(units[^1], Is.EqualTo(new DisplayUnit(DisplayUnitKind.Single, 2)));
        Assert.That(units, Has.Count.EqualTo(2));
    }

    [Test]
    public void AllWide()
    {
        var wide = new[] { true, true };
        Assert.That(DisplayUnitBuilder.Build(wide, false), Has.Count.EqualTo(4));
        Assert.That(DisplayUnitBuilder.Build(wide, true),
            Is.EqualTo(new[] { new DisplayUnit(DisplayUnitKind.Single, 0), new DisplayUnit(DisplayUnitKind.Single, 1) }));
    }

    [Test]
    public void IndexOfPageFindsContainingUnit()
    {
        var units = DisplayUnitBuilder.Build(new[] { false, false, true, false, false }, landscapeScreen: true);
        Assert.That(DisplayUnitBuilder.IndexOfPage(units, 1), Is.EqualTo(0));
        Assert.That(DisplayUnitBuilder.IndexOfPage(units, 2), Is.EqualTo(1));
        Assert.That(DisplayUnitBuilder.IndexOfPage(units, 4), Is.EqualTo(2));

        var portrait = DisplayUnitBuilder.Build(new[] { true }, landscapeScreen: false);
        Assert.That(DisplayUnitBuilder.IndexOfUnit(portrait, new DisplayUnit(DisplayUnitKind.LeftHalf, 0)), Is.EqualTo(1));
    }
}

public class TapZoneResolverTests
{
    [TestCase(0, 0, TapAction.NextComic)]
    [TestCase(100, 0, TapAction.ShowOverlay)]
    [TestCase(200, 0, TapAction.PrevComic)]
    [TestCase(0, 100, TapAction.NextUnit)]
    [TestCase(99.9, 199.9, TapAction.NextUnit)]
    [TestCase(299, 150, TapAction.PrevUnit)]
    [TestCase(0, 200, TapAction.LastUnit)]
    [TestCase(150, 299, TapAction.ShowOverlay)]
    [TestCase(200, 200, TapAction.FirstUnit)]
    public void ResolvesNineZones(double x, double y, TapAction expected)
    {
        Assert.That(TapZoneResolver.Resolve(x, y, 300, 300), Is.EqualTo(expected));
    }
}

public class CacheEvictionPlannerTests
{
    private static readonly CacheEntry[] Entries =
    {
        new("a", 40, 3),
        new("b", 40, 1),
        new("c", 40, 2),
    };

    [Test]
    public void NothingWhenUnderLimit() => Assert.That(CacheEvictionPlanner.Plan(Entries, 120, null), Is.Empty);

    [Test]
    public void DeletesOldestFirst() => Assert.That(CacheEvictionPlanner.Plan(Entries, 50, null), Is.EqualTo(new[] { "b", "c" }));

    [Test]
    public void KeepsOpenEntry() => Assert.That(CacheEvictionPlanner.Plan(Entries, 80, "b"), Is.EqualTo(new[] { "c" }));

    [Test]
    public void OpenEntryAloneOverLimitIsKept()
    {
        var entries = new[] { new CacheEntry("big", 500, 1), new CacheEntry("x", 10, 2) };
        Assert.That(CacheEvictionPlanner.Plan(entries, 100, "big"), Is.EqualTo(new[] { "x" }));
    }
}

public class EntryRulesTests
{
    [TestCase(".hidden", true)]
    [TestCase("__macosx", true)]
    [TestCase("THUMBS.DB", true)]
    [TestCase("a.jpg", false)]
    public void Ignored(string name, bool expected) => Assert.That(EntryRules.IsIgnored(name), Is.EqualTo(expected));

    [TestCase("a.JPG", true)]
    [TestCase("a.webp", true)]
    [TestCase("a.cbz", false)]
    public void Image(string name, bool expected) => Assert.That(EntryRules.IsImage(name), Is.EqualTo(expected));

    [TestCase("dir/a.zip", "ZIP")]
    [TestCase("dir/a.PDF", "PDF")]
    [TestCase("dir/*", "PIC")]
    [TestCase("dir/Vol.1", "PIC")]
    public void KindBadge(string path, string expected) => Assert.That(EntryRules.KindBadge(path), Is.EqualTo(expected));

    [Test]
    public void PathHelpers()
    {
        Assert.That(EntryRules.Combine("", "a"), Is.EqualTo("a"));
        Assert.That(EntryRules.Parent("a/b/c"), Is.EqualTo("a/b"));
        Assert.That(EntryRules.Parent("a"), Is.EqualTo(""));
        Assert.That(EntryRules.Name("a/b"), Is.EqualTo("b"));
        Assert.That(EntryRules.IsIgnoredPath("__MACOSX/a/b.jpg"), Is.True);
    }
}

public class TitleParserTests
{
    [TestCase("タイトル.zip", "タイトル", "")]
    [TestCase("[作者] タイトル.zip", "タイトル", "作者")]
    [TestCase("(作者) タイトル.zip", "タイトル", "作者")]
    [TestCase("【作者】タイトル.zip", "タイトル", "作者")]
    [TestCase("タイトル (1).zip", "タイトル (1)", "")]
    [TestCase("[作者] タイトル (1).zip", "タイトル (1)", "作者")]
    [TestCase("(成年コミック) [作者] タイトル.zip", "タイトル", "成年コミック×作者")]
    [TestCase("(C99) [サークル (作者)] タイトル.zip", "タイトル", "C99×サークル (作者)")]
    [TestCase("(作者) タイトル [おまけ].zip", "タイトル [おまけ]", "作者")]
    [TestCase("[作者]", "[作者]", "")]
    [TestCase("[作者 タイトル.zip", "[作者 タイトル", "")]
    [TestCase("[AAA] (BBB) CCC.zip", "CCC", "AAA×BBB")]
    [TestCase("[作者].zip", "[作者]", "")]
    [TestCase("タイトル.PDF", "タイトル", "")]
    [TestCase("Vol.1", "Vol.1", "")]
    [TestCase("[A] 中 [B] タイトル.zip", "中 [B] タイトル", "A")]
    public void Parse(string name, string expectedName, string expectedAuthor)
    {
        Assert.That(TitleParser.Parse(name), Is.EqualTo((expectedName, expectedAuthor)));
    }
}
