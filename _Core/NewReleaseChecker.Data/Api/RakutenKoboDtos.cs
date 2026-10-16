using System.Text.Json.Serialization;

namespace NewReleaseChecker.Data.Api;

// 楽天Kobo電子書籍検索 API のレスポンス DTO。
// レスポンス形は { "Items": [ { "Item": { ... } } ], ... }。

internal sealed class KoboSearchResponse
{
    [JsonPropertyName("Items")]
    public List<KoboItemWrapper>? Items { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }
}

internal sealed class KoboItemWrapper
{
    [JsonPropertyName("Item")]
    public KoboItem? Item { get; set; }
}

internal sealed class KoboItem
{
    [JsonPropertyName("itemNumber")] public string? ItemNumber { get; set; }
    [JsonPropertyName("isbn")] public string? Isbn { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("publisherName")] public string? PublisherName { get; set; }
    [JsonPropertyName("salesDate")] public string? SalesDate { get; set; }
    [JsonPropertyName("itemUrl")] public string? ItemUrl { get; set; }
    [JsonPropertyName("affiliateUrl")] public string? AffiliateUrl { get; set; }
    [JsonPropertyName("smallImageUrl")] public string? SmallImageUrl { get; set; }
    [JsonPropertyName("mediumImageUrl")] public string? MediumImageUrl { get; set; }
    [JsonPropertyName("largeImageUrl")] public string? LargeImageUrl { get; set; }
    [JsonPropertyName("itemCaption")] public string? ItemCaption { get; set; }
    [JsonPropertyName("koboGenreId")] public string? KoboGenreId { get; set; }
}

// 楽天Koboジャンル検索 API のレスポンス DTO。
internal sealed class KoboGenreResponse
{
    [JsonPropertyName("current")] public KoboGenreNodeDto? Current { get; set; }
    [JsonPropertyName("children")] public List<KoboGenreChildWrapper>? Children { get; set; }
}

internal sealed class KoboGenreChildWrapper
{
    [JsonPropertyName("child")] public KoboGenreNodeDto? Child { get; set; }
}

internal sealed class KoboGenreNodeDto
{
    [JsonPropertyName("koboGenreId")] public string? KoboGenreId { get; set; }
    [JsonPropertyName("koboGenreName")] public string? KoboGenreName { get; set; }
    [JsonPropertyName("koboGenreLevel")] public string? KoboGenreLevel { get; set; }
}
