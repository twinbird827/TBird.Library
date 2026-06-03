namespace NewReleaseChecker.Core.Models;

/// <summary>
/// 楽天Kobo検索 API の 1 巻分の結果（正規化前）。Data 層の DTO から詰め替えて Core に渡す。
/// SalesDate は API の生文字列（未正規化）。ReleaseDateParser で ISO8601 に変換する。
/// </summary>
public sealed class RakutenBook
{
    public string ItemNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string? Publisher { get; init; }
    public string? SalesDate { get; init; }
    public string? ImageUrl { get; init; }
    public string? ItemUrl { get; init; }
    public string? AffiliateUrl { get; init; }
    public string? Caption { get; init; }
    public string? Isbn { get; init; }
    public string? KoboGenreId { get; init; }
}
