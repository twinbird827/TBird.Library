using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.App.Models;

/// <summary>登録シリーズ一覧（SCR-003）の 1 行。</summary>
public sealed class SeriesListItem
{
    public required Series Series { get; init; }
    public Book? LatestBook { get; init; }
    public int UnpurchasedCount { get; init; }

    public int Id => Series.Id;
    public string SeriesKey => Series.SeriesKey;
    public string MediaType => Series.MediaType;
    public string? ImageUrl => CoverImageUrl.ForDisplay(LatestBook?.ImageUrl, LatestBook?.ReleaseDate, DateTime.Now);

    public string AuthorDisplay { get; init; } = string.Empty;
    public string LatestReleaseInfo { get; init; } = string.Empty;
}
