using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using AngleSharp.Dom;
using LanobeReader.Models;
using LanobeReader.Services.Network;
using TBird.Maui.Web;

namespace LanobeReader.Services.Narou;

public class NarouApiService : INovelService
{
    private const string API_BASE = "https://api.syosetu.com/novelapi/api/";
    private const string RANK_BASE = "https://api.syosetu.com/rank/rankget/";
    private const string NCODE_BASE = "https://ncode.syosetu.com/";

    private readonly NetworkPolicyService _network;

    // 全 HTTP は _network.GetStringAsync(TBird.Maui.Web の SiteRateLimiter 経由)で行う。
    // UA 等のヘッダは SiteRateLimiter 側の HttpClient に集約されるため、ここで HttpClient は持たない。
    public NarouApiService(NetworkPolicyService network)
    {
        _network = network;
    }

    public SiteType SiteType => SiteType.Narou;

    public async Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(keyword);
        // title=1 + wname=1 で「タイトル or 作者名」にマッチする作品のみ取得。
        // word 単独だとあらすじ・キーワード・作者名まで全文検索され、無関係な作品が大量にヒットする。
        var url = $"{API_BASE}?out=json&lim=20&word={encoded}&title=1&wname=1";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var response = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
        return ParseNovelApiJson(response);
    }

    private static List<SearchResult> ParseNovelApiJson(string json)
    {
        var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(json);
        var results = new List<SearchResult>();
        if (jsonArray is null || jsonArray.Length <= 1) return results;

        // First element is the allcount metadata, skip it
        for (int i = 1; i < jsonArray.Length; i++)
        {
            var item = jsonArray[i];
            // ncode / title を欠く要素(API 仕様変化・通知/エラーオブジェクト混入)は、その 1 件だけ
            // スキップする。GetProperty は欠落時に例外送出するため、1 件の不正でページ全体(検索/
            // ランキング結果)が失われていた。TryGetProperty + continue で局所化する。
            if (!item.TryGetProperty("ncode", out var ncodeEl)
                || !item.TryGetProperty("title", out var titleEl)) continue;
            var ncode = ncodeEl.GetString();
            if (string.IsNullOrEmpty(ncode)) continue;
            results.Add(new SearchResult
            {
                SiteType = SiteType.Narou,
                // ncode はキー(URL・dedup)に使うため、ロケール非依存の ToLowerInvariant で正規化する
                // (FetchRankingAsync 側と揃える。ToLower だと tr-TR 等で 'I'→'ı' となりキーが分裂する)。
                NovelId = ncode.ToLowerInvariant(),
                Title = titleEl.GetString() ?? "",
                Author = item.TryGetProperty("writer", out var w) ? w.GetString() ?? "" : "",
                TotalEpisodes = item.TryGetProperty("general_all_no", out var ga) ? ga.GetInt32() : 0,
                IsCompleted = item.TryGetProperty("end", out var end) && end.GetInt32() == 0,
                LastUpdatedAt = item.TryGetProperty("general_lastup", out var lastup) ? ToUtcIso(lastup.GetString()) : null,
            });
        }
        return results;
    }

    /// <summary>
    /// Narou の general_lastup("yyyy-MM-dd HH:mm:ss", JST・オフセット無し)を UTC ISO("o")へ正規化する。
    /// 保存形式を UTC に統一することで、新着確定時フォールバック(DateTime.UtcNow.ToString("o"))と同形式に
    /// 揃え、UpdateCheckService.SameInstant の TZ/形式差による誤判定(無駄なフル再取得)と、端末ローカル TZ
    /// での誤表示を防ぐ。JST は DST が無いため固定 +9 時間で変換する。解析不能な値は素のまま返す。
    /// </summary>
    private static string? ToUtcIso(string? narouJst)
    {
        if (string.IsNullOrEmpty(narouJst)) return narouJst;
        if (!DateTime.TryParse(narouJst, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return narouJst;
        }
        var utc = new DateTimeOffset(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
        return utc.ToString("o", CultureInfo.InvariantCulture);
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
            var html = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);

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

    public async Task<string> FetchEpisodeContentAsync(string novelId, int episodeNo, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var url = $"{NCODE_BASE}{novelId}/{episodeNo}/";
        var html = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);

        var document = await AngleSharpHelper.ParseAsync(html, cts.Token).ConfigureAwait(false);

        var honbun = document.QuerySelector(".js-novel-text.p-novel__text:not(.p-novel__text--afterword)");
        if (honbun is null)
        {
            throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
        }

        var paragraphs = honbun.QuerySelectorAll("p");
        var lines = paragraphs.Select(p => p.TextContent);
        return string.Join("\n", lines).Trim();
    }

    public async Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var url = $"{API_BASE}?out=json&ncode={novelId}&of=ga-gl-e-w";
        var response = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
        var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(response);

        if (jsonArray is null || jsonArray.Length <= 1)
        {
            throw new InvalidOperationException("小説情報の取得に失敗しました");
        }

        var item = jsonArray[1];
        var totalEpisodes = item.GetProperty("general_all_no").GetInt32();
        var lastUpdatedAt = item.TryGetProperty("general_lastup", out var lastup) ? ToUtcIso(lastup.GetString()) : null;
        var isCompleted = item.TryGetProperty("end", out var end) && end.GetInt32() == 0;
        var author = item.TryGetProperty("writer", out var writerProp) ? writerProp.GetString() : null;

        return (totalEpisodes, lastUpdatedAt, isCompleted, author);
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

        var rankJson = await _network.GetStringAsync(SiteType.Narou, rankUrl, cts.Token).ConfigureAwait(false);
        var rankItems = JsonSerializer.Deserialize<JsonElement[]>(rankJson);
        if (rankItems is null || rankItems.Length == 0) return new List<SearchResult>();

        var ncodes = new List<string>();
        foreach (var item in rankItems)
        {
            if (!item.TryGetProperty("ncode", out var nc)) continue;
            var ncode = nc.GetString();
            if (!string.IsNullOrEmpty(ncode)) ncodes.Add(ncode.ToLowerInvariant());
            if (ncodes.Count >= Math.Min(limit, 100)) break;
        }
        if (ncodes.Count == 0) return new List<SearchResult>();

        // novelapi へハイフン結合で一括問い合わせ（最大500件、API制限）
        var ncodeParam = string.Join('-', ncodes);
        var detailUrl = $"{API_BASE}?out=json&lim={ncodes.Count}&ncode={ncodeParam}";
        if (biggenre.HasValue) detailUrl += $"&biggenre={biggenre.Value}";

        var detailJson = await _network.GetStringAsync(SiteType.Narou, detailUrl, cts.Token).ConfigureAwait(false);
        var results = ParseNovelApiJson(detailJson);

        // ランキング順に並べる
        var order = ncodes.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
        return results
            .Where(r => order.ContainsKey(r.NovelId))
            .OrderBy(r => order[r.NovelId])
            .ToList();
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

        var json = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
        return ParseNovelApiJson(json);
    }

    private static string BuildRtype(RankingPeriod period)
    {
        DateTime now;
        try
        {
            var jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
            now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
        }
        catch (TimeZoneNotFoundException)
        {
            // フォールバック: UTC + 9h（DST なし、JST 固定オフセット）
            now = DateTime.UtcNow.AddHours(9);
        }
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
