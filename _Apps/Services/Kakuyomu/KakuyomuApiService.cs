using System.Collections.Concurrent;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using LanobeReader.Models;
using LanobeReader.Services.Network;

namespace LanobeReader.Services.Kakuyomu;

public class KakuyomuApiService : INovelService
{
    private const string BASE_URL = "https://kakuyomu.jp";
    private const string USER_AGENT = "Mozilla/5.0 (compatible; LanobeReader/1.0)";

    private readonly HttpClient _httpClient;
    private readonly NetworkPolicyService _network;
    private readonly ConcurrentDictionary<string, (DateTime cachedAt, List<string> episodeIds)> _episodeIdCache = new();
    private static readonly TimeSpan EpisodeIdCacheTtl = TimeSpan.FromMinutes(5);

    public KakuyomuApiService(HttpClient httpClient, NetworkPolicyService network)
    {
        _httpClient = httpClient;
        _network = network;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(USER_AGENT);
        }
    }

    public SiteType SiteType => SiteType.Kakuyomu;

    public async Task<List<SearchResult>> SearchAsync(string keyword, string searchTarget, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var encoded = Uri.EscapeDataString(keyword);
        var url = $"{BASE_URL}/search?q={encoded}";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

        var results = new List<SearchResult>();
        var seen = new HashSet<string>();

        var titleLinks = document.QuerySelectorAll("a[title][href*='/works/']");
        foreach (var link in titleLinks)
        {
            var href = link.GetAttribute("href") ?? "";
            if (href.Contains("/reviews")) continue;

            var workId = ExtractWorkId(href);
            if (string.IsNullOrEmpty(workId) || !seen.Add(workId)) continue;

            var title = link.GetAttribute("title")?.Trim() ?? link.TextContent.Trim();

            // 作者名抽出: 親要素から /users/ アンカーを探す
            var author = "";
            var parentEl = link.ParentElement;
            for (int i = 0; i < 4 && parentEl is not null; i++)
            {
                var userLink = parentEl.QuerySelector("a[href*='/users/']");
                if (userLink is not null)
                {
                    author = userLink.TextContent.Trim();
                    break;
                }
                parentEl = parentEl.ParentElement;
            }

            results.Add(new SearchResult
            {
                SiteType = SiteType.Kakuyomu,
                NovelId = workId,
                Title = title,
                Author = author,
                TotalEpisodes = 0,
                IsCompleted = false,
            });

            if (results.Count >= 20) break;
        }

        return results;
    }

    public async Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

        return ParseEpisodesFromApolloState(html);
    }

    private static List<string> ParseEpisodeIdsFromApolloState(string html)
    {
        var ids = new List<string>();
        var apolloState = ExtractApolloState(html);
        if (apolloState is null) return ids;

        var state = apolloState.Value;
        var chapters = new List<JsonElement>();
        foreach (var prop in state.EnumerateObject())
        {
            if (prop.Name.StartsWith("TableOfContentsChapter:", StringComparison.Ordinal))
            {
                chapters.Add(prop.Value);
            }
        }

        foreach (var chapter in chapters)
        {
            if (!chapter.TryGetProperty("episodeUnions", out var episodeUnions)) continue;
            if (episodeUnions.ValueKind != JsonValueKind.Array) continue;

            foreach (var union in episodeUnions.EnumerateArray())
            {
                if (!union.TryGetProperty("__ref", out var refProp)) continue;
                var refKey = refProp.GetString();
                if (string.IsNullOrEmpty(refKey)) continue;
                var colonIdx = refKey.IndexOf(':');
                if (colonIdx < 0) continue;
                ids.Add(refKey.Substring(colonIdx + 1));
            }
        }

        return ids;
    }

    private async Task<List<string>> GetEpisodeIdsAsync(string novelId, CancellationToken ct)
    {
        if (_episodeIdCache.TryGetValue(novelId, out var cached)
            && DateTime.UtcNow - cached.cachedAt < EpisodeIdCacheTtl)
        {
            return cached.episodeIds;
        }

        var tocUrl = $"{BASE_URL}/works/{novelId}";
        var tocHtml = await _network.GetStringAsync(SiteType.Kakuyomu, tocUrl, ct).ConfigureAwait(false);
        var ids = ParseEpisodeIdsFromApolloState(tocHtml);
        _episodeIdCache[novelId] = (DateTime.UtcNow, ids);
        return ids;
    }

    private static JsonElement? ExtractApolloState(string html)
    {
        const string marker = "<script id=\"__NEXT_DATA__\" type=\"application/json\">";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        if (end < 0) return null;

        var json = html.Substring(start, end - start);
        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("props", out var props)) return null;
        if (!props.TryGetProperty("pageProps", out var pageProps)) return null;
        if (!pageProps.TryGetProperty("__APOLLO_STATE__", out var apolloState)) return null;
        return apolloState.Clone();
    }

    private static List<Episode> ParseEpisodesFromApolloState(string html)
    {
        var episodes = new List<Episode>();
        var apolloState = ExtractApolloState(html);
        if (apolloState is null) return episodes;

        var state = apolloState.Value;
        var chapters = new List<JsonElement>();
        foreach (var prop in state.EnumerateObject())
        {
            if (prop.Name.StartsWith("TableOfContentsChapter:", StringComparison.Ordinal))
            {
                chapters.Add(prop.Value);
            }
        }

        int episodeNo = 0;
        foreach (var chapter in chapters)
        {
            string? chapterTitle = null;
            if (chapter.TryGetProperty("title", out var ct) && ct.ValueKind == JsonValueKind.String)
            {
                var t = ct.GetString();
                if (!string.IsNullOrEmpty(t)) chapterTitle = t;
            }

            if (!chapter.TryGetProperty("episodeUnions", out var episodeUnions)) continue;
            if (episodeUnions.ValueKind != JsonValueKind.Array) continue;

            foreach (var union in episodeUnions.EnumerateArray())
            {
                if (!union.TryGetProperty("__ref", out var refProp)) continue;
                var refKey = refProp.GetString();
                if (string.IsNullOrEmpty(refKey)) continue;

                if (!state.TryGetProperty(refKey, out var episodeEntry)) continue;
                if (!episodeEntry.TryGetProperty("__typename", out var typename)) continue;
                if (typename.GetString() != "Episode") continue;

                var title = episodeEntry.TryGetProperty("title", out var titleProp)
                    ? titleProp.GetString() ?? ""
                    : "";

                episodeNo++;
                episodes.Add(new Episode
                {
                    EpisodeNo = episodeNo,
                    Title = title,
                    ChapterName = chapterTitle,
                });
            }
        }

        return episodes;
    }

    public async Task<string> FetchEpisodeContentAsync(string novelId, int episodeNo, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var episodeIds = await GetEpisodeIdsAsync(novelId, cts.Token).ConfigureAwait(false);

        if (episodeNo < 1 || episodeNo > episodeIds.Count)
        {
            throw new InvalidOperationException($"エピソード {episodeNo} が見つかりません");
        }

        var episodeId = episodeIds[episodeNo - 1];
        var episodeHref = $"{BASE_URL}/works/{novelId}/episodes/{episodeId}";

        var episodeHtml = await _network.GetStringAsync(SiteType.Kakuyomu, episodeHref, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var episodeDoc = await context.OpenAsync(req => req.Content(episodeHtml), cts.Token).ConfigureAwait(false);

        var contentEl = episodeDoc.QuerySelector(".widget-episodeBody, [class*='EpisodeBody']");
        if (contentEl is null)
        {
            throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
        }

        var paragraphs = contentEl.QuerySelectorAll("p");
        if (paragraphs.Length > 0)
        {
            var lines = paragraphs.Select(p => p.TextContent);
            return string.Join("\n", lines).Trim();
        }

        return contentEl.TextContent.Trim();
    }

    public async Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

        // 更新チェックでフェッチした最新TOCでキャッシュを上書きする。
        // これにより直後の Prefetch が古いエピソードIDリストを使うリスクを防ぐ。
        var episodeIds = ParseEpisodeIdsFromApolloState(html);
        _episodeIdCache[novelId] = (DateTime.UtcNow, episodeIds);
        var totalEpisodes = episodeIds.Count;

        bool isCompleted = false;
        string? author = null;
        var apolloState = ExtractApolloState(html);
        if (apolloState is not null)
        {
            var workKey = $"Work:{novelId}";
            if (apolloState.Value.TryGetProperty(workKey, out var work))
            {
                if (work.TryGetProperty("serialStatus", out var status)
                    && status.ValueKind == JsonValueKind.String)
                {
                    isCompleted = status.GetString() == "COMPLETED";
                }

                if (work.TryGetProperty("author", out var authorRef)
                    && authorRef.TryGetProperty("__ref", out var refProp))
                {
                    var userKey = refProp.GetString();
                    if (!string.IsNullOrEmpty(userKey)
                        && apolloState.Value.TryGetProperty(userKey, out var userAccount)
                        && userAccount.TryGetProperty("activityName", out var activityName)
                        && activityName.ValueKind == JsonValueKind.String)
                    {
                        author = activityName.GetString();
                    }
                }
            }
        }

        return (totalEpisodes, DateTime.UtcNow.ToString("o"), isCompleted, author);
    }

    /// <summary>
    /// ランキングページをスクレイピングして作品一覧を返す。
    /// </summary>
    public async Task<List<SearchResult>> FetchRankingAsync(string genreSlug, string periodSlug, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var url = $"{BASE_URL}/rankings/{genreSlug}/{periodSlug}";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

        var results = new List<SearchResult>();
        var seen = new HashSet<string>();

        var links = document.QuerySelectorAll("a[href*='/works/']");
        foreach (var link in links)
        {
            var href = link.GetAttribute("href") ?? "";
            if (href.Contains("/reviews") || href.Contains("/episodes/")) continue;

            var workId = ExtractWorkId(href);
            if (string.IsNullOrEmpty(workId) || !seen.Add(workId)) continue;

            var title = link.TextContent.Trim();
            if (string.IsNullOrEmpty(title))
                title = link.GetAttribute("title")?.Trim() ?? "";
            if (string.IsNullOrEmpty(title)) continue;

            // 作者名抽出: 親要素から /users/ アンカーを探す
            var author = "";
            var parent = link.ParentElement;
            for (int i = 0; i < 4 && parent is not null; i++)
            {
                var userLink = parent.QuerySelector("a[href*='/users/']");
                if (userLink is not null)
                {
                    author = userLink.TextContent.Trim();
                    break;
                }
                parent = parent.ParentElement;
            }

            results.Add(new SearchResult
            {
                SiteType = SiteType.Kakuyomu,
                NovelId = workId,
                Title = title,
                Author = author,
                TotalEpisodes = 0,
                IsCompleted = false,
            });

            if (results.Count >= 30) break;
        }

        return results;
    }

    private static string ExtractWorkId(string href)
    {
        var parts = href.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "works" && i + 1 < parts.Length)
            {
                return parts[i + 1].Split('?')[0].Split('#')[0];
            }
        }
        return string.Empty;
    }
}
