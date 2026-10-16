using System.IO.Compression;
using System.Text.Json;
using AngleSharp.Dom;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Network;
using TBird.Maui.Web;

namespace LanobeReader.Services.Narou;

public class NarouApiService(NetworkPolicyService network) : INovelService
{
    private const string API_BASE = "https://api.syosetu.com/novelapi/api/";
    private const string RANK_BASE = "https://api.syosetu.com/rank/rankget/";
    private const string NCODE_BASE = "https://ncode.syosetu.com/";

    // 全 HTTP は network.GetStringAsync(TBird.Maui.Web の SiteRateLimiter 経由)で行う。
    // UA 等のヘッダは SiteRateLimiter 側の HttpClient に集約されるため、ここで HttpClient は持たない。

    public SiteType SiteType => SiteType.Narou;

    public async Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(keyword);
        // title=1 + wname=1 で「タイトル or 作者名」にマッチする作品のみ取得。
        // word 単独だとあらすじ・キーワード・作者名まで全文検索され、無関係な作品が大量にヒットする。
        var url = $"{API_BASE}?out=json&lim=20&word={encoded}&title=1&wname=1";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var response = await network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
        return NarouNovelApiParser.Parse(response);
    }

    public async Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default)
    {
        var episodes = new List<Episode>();
        string? currentChapter = null;
        int episodeNo = 0;
        int page = 1;

        while (true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var url = page == 1
                ? $"{NCODE_BASE}{novelId}/"
                : $"{NCODE_BASE}{novelId}/?p={page}";
            var html = await network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);

            var document = await AngleSharpHelper.ParseAsync(html, cts.Token).ConfigureAwait(false);

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

    // Narou は本文 URL を episode_no で直接組めるため siteEpisodeId は使用しない(INovelService 共通シグネチャ)。
    // 位置依存フォールバックを持たず誤話リスクが無いため、本文は常にキャッシュ可(cacheable=true)。
    public async Task<(string content, bool cacheable)> FetchEpisodeContentAsync(string novelId, int episodeNo, string? siteEpisodeId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var url = $"{NCODE_BASE}{novelId}/{episodeNo}/";
        var html = await network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);

        var document = await AngleSharpHelper.ParseAsync(html, cts.Token).ConfigureAwait(false);

        return (NarouEpisodeParser.ExtractContent(document), true);
    }

    public async Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default)
    {
        var infos = await FetchNovelInfosAsync([novelId], null, ct).ConfigureAwait(false);
        if (!infos.TryGetValue(novelId, out var r))
        {
            throw new InvalidOperationException("小説情報の取得に失敗しました");
        }
        return (r.TotalEpisodes, r.LastUpdatedAt, r.IsCompleted, r.Author);
    }

    /// <summary>
    /// ncode をハイフン結合して novelapi へ 1 回で問い合わせ、NovelId(小文字 ncode) → 作品情報の辞書を返す。
    /// 削除・検索除外中などで応答に含まれない作品は辞書に入らない。1 回で渡すのは 100 件まで(分割は呼び出し側)。
    /// </summary>
    public async Task<Dictionary<string, SearchResult>> FetchNovelInfosAsync(IReadOnlyCollection<string> ncodes, int? biggenre, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        // lim を付けないと既定の 20 件で切れる。of の n(ncode)・t(title)はパーサが要素の識別に使う。
        var url = $"{API_BASE}?out=json&lim={ncodes.Count}&ncode={string.Join('-', ncodes)}&of=n-t-ga-gl-e-w";
        if (biggenre.HasValue) url += $"&biggenre={biggenre.Value}";

        var json = await network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
        var dict = new Dictionary<string, SearchResult>();
        foreach (var r in NarouNovelApiParser.Parse(json)) dict[r.NovelId] = r;
        return dict;
    }

    /// <summary>
    /// ランキング取得。期間と任意の大ジャンルで絞り込み、詳細メタを novelapi で一括取得する。
    /// </summary>
    public async Task<List<SearchResult>> FetchRankingAsync(RankingPeriod period, int? biggenre, int limit, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var rtype = BuildRtype(period);
        var rankUrl = $"{RANK_BASE}?out=json&rtype={rtype}";

        var rankJson = await network.GetStringAsync(SiteType.Narou, rankUrl, cts.Token).ConfigureAwait(false);
        var rankItems = JsonSerializer.Deserialize<JsonElement[]>(rankJson);
        if (rankItems is null || rankItems.Length == 0) return [];

        var ncodes = new List<string>();
        foreach (var item in rankItems)
        {
            if (!item.TryGetProperty("ncode", out var nc)) continue;
            var ncode = nc.GetString();
            if (!string.IsNullOrEmpty(ncode)) ncodes.Add(ncode.ToLowerInvariant());
            if (ncodes.Count >= Math.Min(limit, 100)) break;
        }
        if (ncodes.Count == 0) return [];

        // ランキング順に並べる
        var dict = await FetchNovelInfosAsync(ncodes, biggenre, cts.Token).ConfigureAwait(false);
        return ncodes.Where(dict.ContainsKey).Select(n => dict[n]).ToList();
    }

    /// <summary>
    /// 大ジャンル別の新着・人気作品取得（novelapi）。biggenre=null で全ジャンル。
    /// 旧シグネチャは `genre=` パラメータ（サブジャンル ID）を渡していたが、
    /// UI は大ジャンル ID（1=恋愛 等）を扱うため `biggenre=` が正しい。
    /// </summary>
    public async Task<List<SearchResult>> FetchByGenreAsync(int? biggenre, string order, int limit, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var lim = Math.Clamp(limit, 1, 100);
        var url = $"{API_BASE}?out=json&lim={lim}&order={Uri.EscapeDataString(order)}";
        if (biggenre.HasValue) url += $"&biggenre={biggenre.Value}";

        var json = await network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
        return NarouNovelApiParser.Parse(json);
    }

    private static string BuildRtype(RankingPeriod period)
    {
        // JST は夏時間が無いため固定 +9h で足りる(NarouDateTime の換算と揃える)。
        var now = DateTime.UtcNow.AddHours(9);
        var today = now.Date;
        // 4:00-7:00頃集計のため、当日朝8時以前は2日前、それ以外は前日を採用
        var dailyTarget = now.Hour < 8 ? today.AddDays(-2) : today.AddDays(-1);

        return period switch
        {
            RankingPeriod.Daily => $"{dailyTarget:yyyyMMdd}-d",
            RankingPeriod.Weekly => $"{NearestTuesday(today):yyyyMMdd}-w",
            RankingPeriod.Monthly => $"{new DateTime(today.Year, today.Month, 1):yyyyMMdd}-m",
            RankingPeriod.Quarterly => $"{new DateTime(today.Year, today.Month, 1):yyyyMMdd}-q",
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
        };
    }

    private static DateTime NearestTuesday(DateTime today)
    {
        int diff = ((int)today.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
        return today.AddDays(-diff);
    }
}
