using System.Text.Json;
using AngleSharp;
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
        var items = document.QuerySelectorAll("[data-widget-id] a[href^='/works/']");

        // Try parsing search results from the page
        var workCards = document.QuerySelectorAll(".widget-workCard");
        if (workCards.Length == 0)
        {
            // Fallback: try alternative selectors
            workCards = document.QuerySelectorAll("[class*='WorkCard']");
        }

        foreach (var card in workCards)
        {
            var linkEl = card.QuerySelector("a[href*='/works/']");
            if (linkEl is null) continue;

            var href = linkEl.GetAttribute("href") ?? "";
            var workId = ExtractWorkId(href);
            if (string.IsNullOrEmpty(workId)) continue;

            var titleEl = card.QuerySelector("[class*='title'], .widget-workCard-title, h3");
            var authorEl = card.QuerySelector("[class*='author'], .widget-workCard-author");

            results.Add(new SearchResult
            {
                SiteType = SiteType.Kakuyomu,
                NovelId = workId,
                Title = titleEl?.TextContent.Trim() ?? linkEl.TextContent.Trim(),
                Author = authorEl?.TextContent.Trim() ?? "",
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
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

        var episodes = new List<Episode>();
        string? currentChapter = null;
        int episodeNo = 0;

        // Parse table of contents
        var tocItems = document.QuerySelectorAll(".widget-toc-episode, .widget-toc-chapter");
        if (tocItems.Length == 0)
        {
            tocItems = document.QuerySelectorAll("[class*='TableOfContents'] a, [class*='chapter']");
        }

        foreach (var item in tocItems)
        {
            if (item.ClassList.Contains("widget-toc-chapter") || item.TagName == "H3")
            {
                currentChapter = item.TextContent.Trim();
                continue;
            }

            var link = item.TagName == "A" ? item : item.QuerySelector("a");
            if (link is null) continue;

            episodeNo++;
            episodes.Add(new Episode
            {
                EpisodeNo = episodeNo,
                Title = link.TextContent.Trim(),
                ChapterName = currentChapter,
            });
        }

        return episodes;
    }

    public async Task<string> FetchEpisodeContentAsync(string novelId, int episodeNo, CancellationToken ct = default)
    {
        // First fetch episode list to get the actual episode URL
        var episodes = await FetchEpisodeListAsync(novelId, ct).ConfigureAwait(false);
        if (episodeNo < 1 || episodeNo > episodes.Count)
        {
            throw new InvalidOperationException($"エピソード {episodeNo} が見つかりません");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Fetch episode page and extract episode ID from TOC
        var tocUrl = $"{BASE_URL}/works/{novelId}";
        var tocHtml = await _httpClient.GetStringAsync(tocUrl, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var tocDoc = await context.OpenAsync(req => req.Content(tocHtml), cts.Token).ConfigureAwait(false);

        var episodeLinks = tocDoc.QuerySelectorAll(".widget-toc-episode a, [class*='TableOfContents'] a");
        if (episodeNo - 1 >= episodeLinks.Length)
        {
            throw new InvalidOperationException("エピソードのURLが特定できません");
        }

        var episodeHref = episodeLinks[episodeNo - 1].GetAttribute("href") ?? "";
        if (!episodeHref.StartsWith("http"))
        {
            episodeHref = $"{BASE_URL}{episodeHref}";
        }

        var episodeHtml = await _httpClient.GetStringAsync(episodeHref, cts.Token).ConfigureAwait(false);
        var episodeDoc = await context.OpenAsync(req => req.Content(episodeHtml), cts.Token).ConfigureAwait(false);

        var contentEl = episodeDoc.QuerySelector(".widget-episodeBody, [class*='EpisodeBody']");
        if (contentEl is null)
        {
            throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
        }

        return contentEl.InnerHtml
            .Replace("<br>", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br />", "\n")
            .Replace("</p>", "\n")
            .Replace("<p>", "")
            .Trim();
    }

    public async Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

        // Count episodes
        var episodeLinks = document.QuerySelectorAll(".widget-toc-episode a, [class*='TableOfContents'] a");
        int totalEpisodes = episodeLinks.Length;

        // Check completion status
        var statusEl = document.QuerySelector("[class*='Status'], [class*='status']");
        bool isCompleted = statusEl?.TextContent.Contains("完結") ?? false;

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
