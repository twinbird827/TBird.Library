namespace NewReleaseChecker.Core.Abstractions;

/// <summary>楽天Kobo検索 API への検索条件。</summary>
public sealed record RakutenSearchQuery
{
    public string? Keyword { get; init; }
    public string? Author { get; init; }
    public string? Title { get; init; }
    public string? PublisherName { get; init; }

    /// <summary>ジャンル絞り込み（koboGenreId）。</summary>
    public string? KoboGenreId { get; init; }

    /// <summary>除外キーワード（NGKeyword）。検索結果からこのキーワードを含む商品を除外する。半角スペース区切りで複数可。</summary>
    public string? NGKeyword { get; init; }

    /// <summary>"standard" / "+releaseDate" / "-releaseDate" / "sales" など。</summary>
    public string? Sort { get; init; }

    /// <summary>0=通常, 1=予約販売。null=指定なし。</summary>
    public int? SalesType { get; init; }

    public int Hits { get; init; } = 30;
    public int Page { get; init; } = 1;
}
