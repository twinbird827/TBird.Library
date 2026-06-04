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
    private const string RelayBaseUrl = "https://kaz.server-on.net:60344";
    private const string SearchEndpoint = RelayBaseUrl + "/api/kobo/search";
    private const string GenreEndpoint = RelayBaseUrl + "/api/kobo/genres";

    // 1 シリーズキー検索で取得する最大ページ数（30 件/ページ）。長編シリーズの取りこぼし対策。
    private const int MaxPages = 4;

    // 追跡キーのトークン区切り。半角スペースに加え、全角スペース(U+3000)・タブも区切りとして扱う。
    private static readonly char[] KeywordSeparators = { ' ', '　', '\t' };

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
        // 追跡キーは半角スペース区切りで楽天APIの AND 検索（orFlag 既定=0）に乗せる。
        // 全角スペースは区切りとして扱われないため半角へ畳み込み、連続空白・前後空白も除去する。
        keyword = NormalizeKeyword(keyword);

        // 新刊・予約（＝最新の発売日）を確実に取得するため新しい順（-releaseDate）で取得する。
        // 古い順（+releaseDate）だと長編シリーズで最新巻が後方ページへ押し出され、MaxPages 打ち切りで取りこぼす。
        var all = new List<RakutenBook>();
        var totalPages = MaxPages;
        for (int page = 1; page <= MaxPages && page <= totalPages; page++)
        {
            var (items, pageCount) = await SearchRawAsync(
                new RakutenSearchQuery { Keyword = keyword, Sort = "-releaseDate", Hits = 30, Page = page }, ct);
            all.AddRange(items);
            // 応答の総ページ数で無駄なページ取得（レート制限下では各 1 秒以上）を抑える。
            if (pageCount > 0) totalPages = pageCount;
            if (items.Count < 30) break; // 最終ページに到達
        }
        return all;
    }

    public async Task<IReadOnlyList<RakutenBook>> SearchAsync(RakutenSearchQuery query, CancellationToken ct = default)
        => (await SearchRawAsync(query, ct)).Items;

    /// <summary>検索を実行し、商品リストと応答の総ページ数（pageCount）を返す。</summary>
    private async Task<(IReadOnlyList<RakutenBook> Items, int PageCount)> SearchRawAsync(
        RakutenSearchQuery query, CancellationToken ct = default)
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
        return (list, resp?.PageCount ?? 0);
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
    /// 追跡キーを楽天Kobo検索APIの keyword 仕様に合わせて正規化する。
    /// 半角/全角スペース・タブで分割し、連続空白・前後空白を畳み込んで半角スペース 1 個区切りに統一する
    /// （これにより複数語が orFlag 既定=0 の AND 検索に乗る）。
    /// 楽天APIは1文字キーワードを 400（"keyword parameter is not valid"）で弾くため、複数トークン時は
    /// 1文字トークンを除外する。ただし全トークンが1文字（除外すると空になる）なら、検索不能を避けるため
    /// そのまま渡す。
    /// </summary>
    private static string NormalizeKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return string.Empty;

        var tokens = keyword.Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length > 1)
        {
            var kept = tokens.Where(t => t.Length >= 2).ToArray();
            if (kept.Length > 0) tokens = kept;
        }
        return string.Join(' ', tokens);
    }

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
