using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using LanobeReader.Helpers;
using LanobeReader.Models;

namespace LanobeReader.Services.Narou;

public class NarouApiService : INovelService
{
    private const string API_BASE = "https://api.syosetu.com/novelapi/api/";
    private const string NCODE_BASE = "https://ncode.syosetu.com/";
    private const string USER_AGENT = "Mozilla/5.0 (compatible; LanobeReader/1.0)";

    private readonly HttpClient _httpClient;

    public NarouApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(USER_AGENT);
    }

    public SiteType SiteType => SiteType.Narou;

    public async Task<List<SearchResult>> SearchAsync(string keyword, string searchTarget, CancellationToken ct = default)
    {
        var wordParam = searchTarget switch
        {
            "Title" => "title",
            "Author" => "wname",
            _ => "word", // Both
        };

        var encoded = Uri.EscapeDataString(keyword);
        var url = $"{API_BASE}?out=json&lim=20&{wordParam}={encoded}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var response = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);
        var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(response);

        var results = new List<SearchResult>();
        if (jsonArray is null || jsonArray.Length <= 1) return results;

        // First element is the allcount metadata, skip it
        for (int i = 1; i < jsonArray.Length; i++)
        {
            var item = jsonArray[i];
            results.Add(new SearchResult
            {
                SiteType = SiteType.Narou,
                NovelId = item.GetProperty("ncode").GetString()?.ToLower() ?? "",
                Title = item.GetProperty("title").GetString() ?? "",
                Author = item.GetProperty("writer").GetString() ?? "",
                TotalEpisodes = item.GetProperty("general_all_no").GetInt32(),
                IsCompleted = item.TryGetProperty("end", out var end) && end.GetInt32() == 0,
                LastUpdatedAt = item.TryGetProperty("general_lastup", out var lastup) ? lastup.GetString() : null,
            });
        }

        return results;
    }

    public async Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default)
    {
        var episodes = new List<Episode>();
        string? currentChapter = null;
        int episodeNo = 0;
        int page = 1;

        var config = Configuration.Default;

        while (true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var url = page == 1
                ? $"{NCODE_BASE}{novelId}/"
                : $"{NCODE_BASE}{novelId}/?p={page}";
            var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

            var eplist = document.QuerySelector(".p-eplist");
            if (eplist is null)
            {
                if (page == 1)
                {
                    // Single episode (short story)
                    episodes.Add(new Episode
                    {
                        EpisodeNo = 1,
                        Title = "本編",
                    });
                }
                break;
            }

            foreach (var child in eplist.Children)
            {
                if (child.ClassList.Contains("p-eplist__chapter-title"))
                {
                    currentChapter = child.TextContent.Trim();
                }
                else if (child.ClassList.Contains("p-eplist__sublist"))
                {
                    var link = child.QuerySelector(".p-eplist__subtitle");
                    if (link is not null)
                    {
                        episodeNo++;
                        episodes.Add(new Episode
                        {
                            EpisodeNo = episodeNo,
                            Title = link.TextContent.Trim(),
                            ChapterName = currentChapter,
                        });
                    }
                }
            }

            // Check for next page
            var nextLink = document.QuerySelector(".c-pager__item--next");
            if (nextLink is null || nextLink.TagName != "A")
                break;

            page++;
        }

        return episodes;
    }

    public async Task<string> FetchEpisodeContentAsync(string novelId, int episodeNo, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var url = $"{NCODE_BASE}{novelId}/{episodeNo}/";
        var html = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

        var honbun = document.QuerySelector(".js-novel-text.p-novel__text:not(.p-novel__text--afterword)");
        if (honbun is null)
        {
            throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
        }

        var paragraphs = honbun.QuerySelectorAll("p");
        var lines = paragraphs.Select(p => p.TextContent);
        return string.Join("\n", lines).Trim();
    }

    public async Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var url = $"{API_BASE}?out=json&ncode={novelId}&of=ga-gl-e";
        var response = await _httpClient.GetStringAsync(url, cts.Token).ConfigureAwait(false);
        var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(response);

        if (jsonArray is null || jsonArray.Length <= 1)
        {
            throw new InvalidOperationException("小説情報の取得に失敗しました");
        }

        var item = jsonArray[1];
        var totalEpisodes = item.GetProperty("general_all_no").GetInt32();
        var lastUpdatedAt = item.TryGetProperty("general_lastup", out var lastup) ? lastup.GetString() : null;
        var isCompleted = item.TryGetProperty("end", out var end) && end.GetInt32() == 0;

        return (totalEpisodes, lastUpdatedAt, isCompleted);
    }
}
