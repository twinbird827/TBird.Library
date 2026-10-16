namespace NewReleaseChecker.Core.Models;

/// <summary>楽天Koboジャンル検索 API のジャンルノード（階層）。</summary>
public sealed class RakutenGenreNode
{
    public string KoboGenreId { get; init; } = string.Empty;
    public string GenreName { get; init; } = string.Empty;
    public int GenreLevel { get; init; }
    public IReadOnlyList<RakutenGenreNode> Children { get; init; } = Array.Empty<RakutenGenreNode>();
}
