using NewReleaseChecker.Core.Services;
using NUnit.Framework;

namespace NewReleaseChecker.Core.Tests;

public class CoverImageUrlTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 9, 30, 0);
    private const string Url = "https://example.com/cover.jpg";

    [Test]
    public void ReleasedBeforeToday_ReturnsOriginalUrl()
        => Assert.That(CoverImageUrl.ForDisplay(Url, "2026-10-14", Now), Is.EqualTo(Url));

    [Test]
    public void ReleaseToday_AppendsDateQuery()
        => Assert.That(CoverImageUrl.ForDisplay(Url, "2026-10-15", Now), Is.EqualTo(Url + "?d=20261015"));

    [Test]
    public void ReleaseUnknown_AppendsDateQuery()
        => Assert.That(CoverImageUrl.ForDisplay(Url, null, Now), Is.EqualTo(Url + "?d=20261015"));

    [Test]
    public void UrlWithQuery_AppendsWithAmpersand()
        => Assert.That(CoverImageUrl.ForDisplay(Url + "?_ex=200x200", null, Now), Is.EqualTo(Url + "?_ex=200x200&d=20261015"));

    [Test]
    public void EmptyUrl_ReturnsEmpty()
        => Assert.That(CoverImageUrl.ForDisplay("", null, Now), Is.EqualTo(""));
}
