using System.Text.Json;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using TBird.Maui.Background;

namespace NewReleaseChecker.Data.Api;

/// <summary>
/// 楽天Kobo電子書籍検索 API / ジャンル検索 API クライアント（IRakutenApiClient 実装）。
/// 楽天へは直接アクセスせず、自宅の中継サーバー（NewReleaseChecker.Relay）経由で POST する。
/// applicationId / accessKey / Referer 等はすべて中継サーバー側で付与されるため、本クライアントは保持しない。
/// 共有シークレット（X-Relay-Auth）は HttpClient のデフォルトヘッダで付与される（MauiProgram 参照）。
/// HTTP は SiteRateLimiter 経由で行い、「1 リクエストごとに 1 秒以上」のレート制限を担保する。
/// </summary>
public sealed class RakutenKoboApiClient : IRakutenApiClient
{
    /// <summary>SiteRateLimiter に事前登録する siteKey。MauiProgram の生成時と一致させること。</summary>
    public const string SiteKey = "relay-kobo";

    // 中継サーバー（透過プロキシ）のエンドポイント。GET ではなく POST(JSON 本文) で叩く。
    private const string RelayBaseUrl = "https://kaz.server-on.net:49443";
    private const string SearchEndpoint = RelayBaseUrl + "/api/kobo/search";
    private const string GenreEndpoint = RelayBaseUrl + "/api/kobo/genres";

    // 1 シリーズキー検索で取得する最大ページ数（30 件/ページ）。長編シリーズの取りこぼし対策。
    private const int MaxPages = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    private readonly SiteRateLimiter _rateLimiter;

    public RakutenKoboApiClient(SiteRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
    }

    public async Task<IReadOnlyList<RakutenBook>> SearchByKeywordAsync(string keyword, CancellationToken ct = default)
    {
        var all = new List<RakutenBook>();
        for (int page = 1; page <= MaxPages; page++)
        {
            var items = await SearchAsync(
                new RakutenSearchQuery { Keyword = keyword, Sort = "+releaseDate", Hits = 30, Page = page }, ct);
            all.AddRange(items);
            if (items.Count < 30) break; // 最終ページに到達
        }
        return all;
    }

    public async Task<IReadOnlyList<RakutenBook>> SearchAsync(RakutenSearchQuery query, CancellationToken ct = default)
    {
        var json = await _rateLimiter.PostJsonAsync(SiteKey, SearchEndpoint, BuildSearchBody(query), ct);

        var resp = JsonSerializer.Deserialize<KoboSearchResponse>(json, JsonOptions);
        var list = new List<RakutenBook>();
        if (resp?.Items != null)
        {
            foreach (var w in resp.Items)
            {
                if (w.Item is { } it && !string.IsNullOrEmpty(it.ItemNumber))
                {
                    list.Add(Map(it));
                }
            }
        }
        return list;
    }

    public async Task<RakutenGenreNode> GetGenreAsync(string koboGenreId, CancellationToken ct = default)
    {
        var json = await _rateLimiter.PostJsonAsync(SiteKey, GenreEndpoint, BuildGenreBody(koboGenreId), ct);

        var resp = JsonSerializer.Deserialize<KoboGenreResponse>(json, JsonOptions);
        var children = new List<RakutenGenreNode>();
        if (resp?.Children != null)
        {
            foreach (var c in resp.Children)
            {
                if (c.Child is { } node)
                {
                    children.Add(new RakutenGenreNode
                    {
                        KoboGenreId = node.KoboGenreId ?? string.Empty,
                        GenreName = node.KoboGenreName ?? string.Empty,
                        GenreLevel = ParseLevel(node.KoboGenreLevel),
                    });
                }
            }
        }
        return new RakutenGenreNode
        {
            KoboGenreId = resp?.Current?.KoboGenreId ?? koboGenreId,
            GenreName = resp?.Current?.KoboGenreName ?? string.Empty,
            GenreLevel = ParseLevel(resp?.Current?.KoboGenreLevel),
            Children = children,
        };
    }

    private static RakutenBook Map(KoboItem it) => new()
    {
        ItemNumber = it.ItemNumber ?? string.Empty,
        Title = it.Title ?? string.Empty,
        Author = it.Author,
        Publisher = it.PublisherName,
        SalesDate = it.SalesDate,
        ImageUrl = it.LargeImageUrl ?? it.MediumImageUrl ?? it.SmallImageUrl,
        ItemUrl = it.ItemUrl,
        AffiliateUrl = it.AffiliateUrl,
        Caption = it.ItemCaption,
        Isbn = it.Isbn,
        KoboGenreId = it.KoboGenreId,
    };

    private static int ParseLevel(string? level) => int.TryParse(level, out var n) ? n : 0;

    /// <summary>
    /// 中継サーバーへ渡す検索パラメータ JSON を組み立てる（キー名・値は楽天Kobo電子書籍検索APIと 1:1 対応）。
    /// applicationId / accessKey / affiliateId / Referer 等はサーバー側で付与するため含めない。
    /// formatVersion は指定しない（既定の Items[].Item ラップ構造を維持＝DTO と整合させるため）。
    /// </summary>
    private static string BuildSearchBody(RakutenSearchQuery q)
    {
        var body = new Dictionary<string, object?> { ["format"] = "json" };
        if (!string.IsNullOrEmpty(q.Keyword)) body["keyword"] = q.Keyword;
        if (!string.IsNullOrEmpty(q.Title)) body["title"] = q.Title;
        if (!string.IsNullOrEmpty(q.Author)) body["author"] = q.Author;
        if (!string.IsNullOrEmpty(q.PublisherName)) body["publisherName"] = q.PublisherName;
        if (!string.IsNullOrEmpty(q.KoboGenreId)) body["koboGenreId"] = q.KoboGenreId;
        if (!string.IsNullOrEmpty(q.Sort)) body["sort"] = q.Sort;
        if (q.SalesType is { } st) body["salesType"] = st;
        body["hits"] = q.Hits;
        body["page"] = q.Page;
        return JsonSerializer.Serialize(body);
    }

    /// <summary>中継サーバーへ渡すジャンル検索パラメータ JSON を組み立てる。</summary>
    private static string BuildGenreBody(string koboGenreId)
    {
        var body = new Dictionary<string, object?>
        {
            ["format"] = "json",
            ["koboGenreId"] = koboGenreId,
        };
        return JsonSerializer.Serialize(body);
    }
}
