using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using LanobeReader.Helpers;
using LanobeReader.Models;

namespace LanobeReader.Services.Kakuyomu;

public class KakuyomuApiService : INovelService
{
    private const string BASE_URL = "https://kakuyomu.jp";
    private const string USER_AGENT = "Mozilla/5.0 (compatible; LanobeReader/1.0)";

    private readonly HttpClient _httpClient;

    public KakuyomuApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(USER_AGENT);
    }

    public SiteType SiteType => SiteType.Kakuyomu;

    public async Task<List<SearchResult>> SearchAsync(string keyword, string searchTarget, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Kakuyomu search via HTML scraping (public API is undocumented)
        var encoded = Uri.EscapeDataString(keyword);
        var url = $"{BASE_URL}/search?q={encoded}";
        var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

        var results = new List<SearchResult>();
        var seen = new HashSet<string>();

        // New structure: <a title="タイトル" href="https://kakuyomu.jp/works/ID" class="...">タイトル</a>
        var titleLinks = document.QuerySelectorAll("a[title][href*='/works/']");
        foreach (var link in titleLinks)
        {
            var href = link.GetAttribute("href") ?? "";
            if (href.Contains("/reviews")) continue;

            var workId = ExtractWorkId(href);
            if (string.IsNullOrEmpty(workId) || !seen.Add(workId)) continue;

            var title = link.GetAttribute("title")?.Trim() ?? link.TextContent.Trim();

            results.Add(new SearchResult
            {
                SiteType = SiteType.Kakuyomu,
                NovelId = workId,
                Title = title,
                Author = "",
                TotalEpisodes = 0,  // Will be fetched on registration
                IsCompleted = false,
            });

            if (results.Count >= 20) break;
        }

        return results;
    }

    public async Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

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
                // refKey looks like "Episode:16816927862837791426"
                var colonIdx = refKey.IndexOf(':');
                if (colonIdx < 0) continue;
                ids.Add(refKey.Substring(colonIdx + 1));
            }
        }

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
        // Clone so the JsonDocument can be disposed safely
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
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        // Fetch TOC and extract episode IDs from Apollo State
        var tocUrl = $"{BASE_URL}/works/{novelId}";
        var tocHtml = await _httpClient.GetStringAsync(tocUrl, cts.Token).ConfigureAwait(false);
        var episodeIds = ParseEpisodeIdsFromApolloState(tocHtml);

        if (episodeNo < 1 || episodeNo > episodeIds.Count)
        {
            throw new InvalidOperationException($"エピソード {episodeNo} が見つかりません");
        }

        var episodeId = episodeIds[episodeNo - 1];
        var episodeHref = $"{BASE_URL}/works/{novelId}/episodes/{episodeId}";

        var episodeHtml = await _httpClient.GetStringAsync(episodeHref, cts.Token).ConfigureAwait(false);

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

    public async Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

        var totalEpisodes = ParseEpisodeIdsFromApolloState(html).Count;

        bool isCompleted = false;
        var apolloState = ExtractApolloState(html);
        if (apolloState is not null)
        {
            var workKey = $"Work:{novelId}";
            if (apolloState.Value.TryGetProperty(workKey, out var work)
                && work.TryGetProperty("serialStatus", out var status)
                && status.ValueKind == JsonValueKind.String)
            {
                isCompleted = status.GetString() == "COMPLETED";
            }
        }

        return (totalEpisodes, DateTime.UtcNow.ToString("o"), isCompleted);
    }

    private static string ExtractWorkId(string href)
    {
        // Extract work ID from href like "/works/1234567890"
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
