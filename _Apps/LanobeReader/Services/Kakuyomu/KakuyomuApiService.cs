using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using LanobeReader.Models;
using LanobeReader.Services.Network;
using TBird.Core;
using TBird.Maui.Web;

namespace LanobeReader.Services.Kakuyomu;

public class KakuyomuApiService : INovelService
{
    private const string BASE_URL = "https://kakuyomu.jp";

    private readonly NetworkPolicyService _network;
    // novelId -> (取得時刻, 解析済みエピソード). ids は episodes[i].SiteEpisodeId と一致するため別持ちせず
    // episodes を単一の真実源にする。FetchNovelInfoAsync が毎巡 populate し、直後の FetchEpisodeListAsync が
    // 同一 /works/{novelId} を再取得せず再利用する(2 fetch 冗長と、2 回取得のズレによる count-mismatch を解消)。
    private readonly ConcurrentDictionary<string, (DateTime cachedAt, List<Episode> episodes)> _tocCache = new();
    private static readonly TimeSpan TocCacheTtl = TimeSpan.FromMinutes(5);
    private const int TocCacheMaxEntries = 100;

    // 全 HTTP は _network.GetStringAsync(TBird.Maui.Web の SiteRateLimiter 経由)で行う。
    // UA 等のヘッダは SiteRateLimiter 側の HttpClient に集約されるため、ここで HttpClient は持たない。
    public KakuyomuApiService(NetworkPolicyService network)
    {
        _network = network;
    }

    public SiteType SiteType => SiteType.Kakuyomu;

    public async Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var encoded = Uri.EscapeDataString(keyword);
        var url = $"{BASE_URL}/search?q={encoded}";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

        var document = await AngleSharpHelper.ParseAsync(html, cts.Token).ConfigureAwait(false);

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

            // 検索ページは CSS Modules（ハッシュ付クラス名）で組まれており、カードコンテナの
            // class はビルドごとに変わる。安定して取れる identifying signal は「カード内に
            // /users/ リンクと Meta_metaItem__* が両方ある」ことなので、両方を含む最近接の
            // 共通祖先を「カード」とみなす。
            AngleSharp.Dom.IElement? cardEl = null;
            var ancestor = link.ParentElement;
            for (int i = 0; i < 10 && ancestor is not null; i++)
            {
                if (ancestor.QuerySelector("a[href*='/users/']") is not null
                    && ancestor.QuerySelector("[class*='Meta_metaItem__']") is not null)
                {
                    cardEl = ancestor;
                    break;
                }
                ancestor = ancestor.ParentElement;
            }

            var author = "";
            int totalEpisodes = 0;
            bool isCompleted = false;

            if (cardEl is not null)
            {
                var userLink = cardEl.QuerySelector("a[href*='/users/']");
                author = userLink?.TextContent.Trim() ?? "";

                // 「連載中 NN話」「完結済 NN話」テキストを持つ Meta_metaItem__* を探す。
                // CSS Modules のハッシュ部分はビルドごとに変わるため substring 一致で抽出。
                foreach (var meta in cardEl.QuerySelectorAll("[class*='Meta_metaItem__']"))
                {
                    var text = meta.TextContent.Trim();
                    // 桁区切り(半角/全角コンマ)を含む話数表記 "完結済 1,234話" にも対応する。
                    // 区切りで弾くと話数 0・完結→連載中の誤判定になるため、抽出後にコンマを除去して解析する。
                    var m = Regex.Match(text, @"^(連載中|完結済)\s*([\d,，]+)話$");
                    if (m.Success)
                    {
                        isCompleted = m.Groups[1].Value == "完結済";
                        int.TryParse(m.Groups[2].Value.Replace(",", "").Replace("，", ""), out totalEpisodes);
                        break;
                    }
                }
            }
            else
            {
                // カード特定失敗時のフォールバック: 旧実装の 4 段親辿りで author だけ取得。
                // CSS Modules のクラスプレフィックス自体が将来変わった場合の保険。
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
            }

            results.Add(new SearchResult
            {
                SiteType = SiteType.Kakuyomu,
                NovelId = workId,
                Title = title,
                Author = author,
                TotalEpisodes = totalEpisodes,
                IsCompleted = isCompleted,
            });

            if (results.Count >= 20) break;
        }

        return results;
    }

    public async Task<List<Episode>> FetchEpisodeListAsync(string novelId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var episodes = await GetEpisodesAsync(novelId, cts.Token).ConfigureAwait(false);
        // キャッシュは pristine を保持する。共有インスタンスを返すと呼び出し元(UpdateCheckService が NovelId、
        // InsertAll が PK を設定)の破壊的変更がキャッシュを汚染するため、ParseApolloState が設定する 4 項目だけ
        // 複製して新インスタンスで返す(NovelId/Id 等は呼び出し元が後から設定)。
        return episodes.Select(e => new Episode
        {
            EpisodeNo = e.EpisodeNo,
            Title = e.Title,
            ChapterName = e.ChapterName,
            SiteEpisodeId = e.SiteEpisodeId,
        }).ToList();
    }

    // _tocCache への書き込みを 1 箇所へ集約し、エントリ数が上限を超えたときだけ Sweep する軽量ゲート。
    // 読み取り経路(GetEpisodesAsync)のみが Sweep していたため、更新巡回が多数の Kakuyomu 作品で本書き込み
    // (FetchEpisodeListAsync / FetchNovelInfoAsync)を連続実行すると、間に読み取りが挟まらず上限
    // (TocCacheMaxEntries)を一時超過してメモリが膨らんでいた。書き込み毎のフル TTL Sweep は巡回中
    // O(n^2) になり高コストなので、上限超過時のみ Sweep して常時上限内へ収める(超過がなければ純粋な代入のみ)。
    private void StoreTocCache(string novelId, List<Episode> episodes)
    {
        _tocCache[novelId] = (DateTime.UtcNow, episodes);
        if (_tocCache.Count > TocCacheMaxEntries)
        {
            SweepExpiredTocCache();
        }
    }

    private void SweepExpiredTocCache()
    {
        var now = DateTime.UtcNow;
        var expired = _tocCache
            .Where(kv => now - kv.Value.cachedAt >= TocCacheTtl)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var k in expired) _tocCache.TryRemove(k, out _);

        if (_tocCache.Count > TocCacheMaxEntries)
        {
            var oldest = _tocCache
                .OrderBy(kv => kv.Value.cachedAt)
                .Take(_tocCache.Count - TocCacheMaxEntries)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in oldest) _tocCache.TryRemove(k, out _);
        }
    }

    // 鮮度の高い TOC(解析済み episodes)をキャッシュ優先で返す。FetchNovelInfoAsync が温めた直後の
    // FetchEpisodeListAsync が同一 /works/{novelId} を再取得せず再利用する合流点。
    // 注意: 命中時は _tocCache 内の共有インスタンス(List とその Episode 要素)をそのまま返す。呼び出し元は
    // read-only で扱うこと。要素を変更する必要がある経路(FetchEpisodeListAsync が NovelId/Id 設定のため公開)は
    // 必ず複製してから返し、キャッシュ汚染(以後の FetchNovelInfoAsync/Prefetch が破壊済み TOC を再利用)を防ぐ。
    private async Task<List<Episode>> GetEpisodesAsync(string novelId, CancellationToken ct)
    {
        SweepExpiredTocCache();

        if (_tocCache.TryGetValue(novelId, out var cached)
            && DateTime.UtcNow - cached.cachedAt < TocCacheTtl)
        {
            return cached.episodes;
        }

        var url = $"{BASE_URL}/works/{novelId}";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, ct).ConfigureAwait(false);
        var (_, episodes) = ParseApolloState(html);
        StoreTocCache(novelId, episodes);
        return episodes;
    }

    private async Task<List<string>> GetEpisodeIdsAsync(string novelId, CancellationToken ct)
    {
        var episodes = await GetEpisodesAsync(novelId, ct).ConfigureAwait(false);
        // ParseApolloState で各話に必ず SiteEpisodeId を設定するため順序・件数は episodeIds と一致する。
        return episodes.Select(e => e.SiteEpisodeId!).ToList();
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
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("props", out var props)) return null;
        if (!props.TryGetProperty("pageProps", out var pageProps)) return null;
        if (!pageProps.TryGetProperty("__APOLLO_STATE__", out var apolloState)) return null;
        return apolloState.Clone();
    }

    private static (List<string> episodeIds, List<Episode> episodes) ParseApolloState(string html)
    {
        var ids = new List<string>();
        var episodes = new List<Episode>();
        var apolloState = ExtractApolloState(html);
        if (apolloState is null) return (ids, episodes);

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
                var colonIdx = refKey.IndexOf(':');
                if (colonIdx < 0) continue;
                if (!state.TryGetProperty(refKey, out var episodeEntry)) continue;
                if (!episodeEntry.TryGetProperty("__typename", out var typename)) continue;
                if (typename.GetString() != "Episode") continue;

                // ID 追加は Episode 確定後に行う。episodeUnions には非 Episode(広告/お知らせ等)の
                // __ref も含まれ、判定前に追加すると episodeIds が実話数(episodes)とズレる:
                //   (a) FetchEpisodeContentAsync の episodeIds[episodeNo-1] 索引がズレて誤話/404
                //   (b) totalEpisodes(=episodeIds.Count)が水増しされ UpdateCheckService が毎回再取得
                var siteEpisodeId = refKey.Substring(colonIdx + 1);
                ids.Add(siteEpisodeId);

                var title = episodeEntry.TryGetProperty("title", out var titleProp)
                    ? titleProp.GetString() ?? ""
                    : "";

                episodeNo++;
                episodes.Add(new Episode
                {
                    EpisodeNo = episodeNo,
                    Title = title,
                    ChapterName = chapterTitle,
                    // 本文取得を位置依存(episodeIds[episodeNo-1])から安定 ID へ移行するため、各話に
                    // サイト側エピソード ID を持たせて永続化する(序盤話の削除/並べ替えでの誤話表示を防ぐ)。
                    SiteEpisodeId = siteEpisodeId,
                });
            }
        }

        return (ids, episodes);
    }

    public async Task<(string content, bool cacheable)> FetchEpisodeContentAsync(string novelId, int episodeNo, string? siteEpisodeId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var selfHealing = false;
        if (!string.IsNullOrEmpty(siteEpisodeId))
        {
            // DB に永続化済みの安定したサイト話 ID を直接使う本命パス。生 TOC の位置依存解決(序盤話の
            // 削除/並べ替えで episodeIds がシフトし誤話/404 になる)を回避する。
            try
            {
                return (await FetchContentByEpisodeIdAsync(novelId, siteEpisodeId, cts.Token).ConfigureAwait(false), true);
            }
            // (U2) 自己修復は「安定 ID の陳腐化を示す失敗」に限定する。5xx/408/429/通信断などの transient
            // エラーは SiteRateLimiter 内で既にリトライ済みで、ここまで来たら一過性の通信障害とみなし再送出して
            // 上位リトライに委ねる(正しい安定 ID を一過性失敗で捨ててキャッシュ無効化+誤話リスクを負わない)。
            // 404 等の非 transient な HttpRequestException(クライアントエラー=削除/改稿で ID 失効)と本文欠落
            // (InvalidOperationException)のみ位置依存フォールバックへ降格させる。予期せぬ例外(NRE/JsonException/
            // HTTP に包まれない IOException 等)はここで握らず再送出し、上位(ReaderViewModel)でエラー表示させる
            // (承認外の文脈で位置依存降格=誤話リスクを負わないため、対象を陳腐化失敗の 2 種に厳密化)。
            catch (Exception ex) when (
                (ex is HttpRequestException hre && !TransientHttpErrorHelper.IsTransient(hre))
                || ex is InvalidOperationException)
            {
                // (U3) 安定 ID での取得失敗(404/本文欠落=削除・改稿で ID が陳腐化)時は、生 TOC の位置依存
                // 解決で 1 度だけ自己修復を試みる。位置依存はドリフトで誤話になりうるが、移行前の挙動と
                // 同等であり「読めない」確定よりは可読性を優先する方針(ユーザ承認済み U3-A)。なお訂正後の
                // ID 永続化は API サービスへ DB 依存を持ち込まない設計方針のため行わない(読み取り都度の
                // 再解決は 5 分 TOC キャッシュにより軽微)。
                MessageService.Warn($"安定IDでの本文取得に失敗、位置依存解決で再試行: works/{novelId} ep={episodeNo}: {ex.Message}");
                selfHealing = true;
            }
        }

        // 旧データ(site_episode_id 列追加前に保存された話)、または上の安定 ID パスが失敗した場合の
        // フォールバック: 生 TOC から位置で解決する。
        var episodeIds = await GetEpisodeIdsAsync(novelId, cts.Token).ConfigureAwait(false);
        if (episodeNo < 1 || episodeNo > episodeIds.Count)
        {
            throw new InvalidOperationException($"エピソード {episodeNo} が見つかりません");
        }
        var content = await FetchContentByEpisodeIdAsync(novelId, episodeIds[episodeNo - 1], cts.Token).ConfigureAwait(false);
        // 自己修復フォールバック(陳腐化した安定 ID の代替)で取得した本文はドリフトで誤話の可能性があり、
        // かつ安定 ID が残置されるため backfill のキャッシュ破棄でも訂正されない。恒久キャッシュ(誤話キャッシュの
        // 恒久化)を避けるため cacheable=false で返す。旧データ(siteEpisodeId 無し)の位置解決は移行 backfill
        // 時にキャッシュ破棄で訂正されるため従来どおりキャッシュ可。
        return (content, cacheable: !selfHealing);
    }

    private async Task<string> FetchContentByEpisodeIdAsync(string novelId, string episodeId, CancellationToken ct)
    {
        var episodeHref = $"{BASE_URL}/works/{novelId}/episodes/{episodeId}";

        var episodeHtml = await _network.GetStringAsync(SiteType.Kakuyomu, episodeHref, ct).ConfigureAwait(false);

        var episodeDoc = await AngleSharpHelper.ParseAsync(episodeHtml, ct).ConfigureAwait(false);

        // CSS3 の [class~='X'] は「スペース区切り単語の完全一致」のため、
        // EpisodeBodyHeader 等の連結クラス名にはマッチしない（過剰マッチ回避）。
        var contentEl =
            episodeDoc.QuerySelector(".widget-episodeBody") ??
            episodeDoc.QuerySelector("[class~='EpisodeBody']");

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

        // 更新チェックでフェッチした最新TOCでキャッシュを上書きする。これにより直後の FetchEpisodeListAsync
        // /Prefetch が同一 /works/{novelId} を再取得せず再利用し(2 fetch 冗長を解消)、古い TOC を使うリスクも防ぐ。
        var (_, episodes) = ParseApolloState(html);
        StoreTocCache(novelId, episodes);
        var totalEpisodes = episodes.Count;

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

        // lastUpdatedAt は null を返す。Kakuyomu の Apollo State には信頼できる作品単位の最終更新時刻が
        // 無く、ここで DateTime.UtcNow を返すと「毎回値が変わる」ため、更新チェック側の
        // siteUpdatedSinceLast(前回保存値との比較)が常に true となり、報告話数が頭打ちのまま実話が
        // 埋まらないケースで毎周期フル一覧を再取得し続けてしまう。null なら報告話数(totalEpisodes)の
        // 変化のみで再取得を判定する従来挙動に委ねられる。表示/ソート用の値は更新チェック側が新着確定時に
        // フォールバックで補う。
        return (totalEpisodes, null, isCompleted, author);
    }

    /// <summary>
    /// ランキングページをスクレイピングして作品一覧を返す。
    /// `div.widget-work` のうち `p.widget-work-rank` を持つカードのみを対象とすることで
    /// 広告/おすすめ枠の混入を排除し、DOM 順 = ランキング順を保つ。
    /// </summary>
    public async Task<List<SearchResult>> FetchRankingAsync(string genreSlug, string periodSlug, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        // `?work_variation=long` を必ず付ける。省略するとオリジン nginx が
        // `Location: http://kakuyomu.jp/...?work_variation=long` の 302 を返し（HTTPS→HTTP）、
        // Android の Network Security Config（cleartextTrafficPermitted=false）が 2 段目で
        // 接続を拒否して HttpRequestException "Connection failure" になる。
        // CloudFront が後段で 301 https に戻すためブラウザは通るが AndroidMessageHandler は通らない。
        var url = $"{BASE_URL}/rankings/{genreSlug}/{periodSlug}?work_variation=long";
        var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

        var document = await AngleSharpHelper.ParseAsync(html, cts.Token).ConfigureAwait(false);

        var results = new List<SearchResult>();
        var seen = new HashSet<string>(); // サイト構造変化への保険（事前調査では重複なし）

        var workCards = document.QuerySelectorAll("div.widget-work");
        foreach (var card in workCards)
        {
            // ランキング順位を持つカードに限定（広告/おすすめ枠を除外）
            var rankEl = card.QuerySelector("p.widget-work-rank");
            if (rankEl is null) continue;

            var titleLink = card.QuerySelector("a.widget-workCard-titleLabel");
            if (titleLink is null) continue;

            var href = titleLink.GetAttribute("href") ?? "";
            var workId = ExtractWorkId(href);
            if (string.IsNullOrEmpty(workId)) continue;
            if (!seen.Add(workId)) continue;

            var title = titleLink.TextContent.Trim();
            if (string.IsNullOrEmpty(title)) continue;

            var authorLink = card.QuerySelector("a.widget-workCard-authorLabel");
            var author = authorLink?.TextContent.Trim() ?? "";

            var statusLabel = card.QuerySelector("span.widget-workCard-statusLabel");
            var isCompleted = statusLabel?.TextContent.Trim() == "完結";

            var episodeCountText = card.QuerySelector("span.widget-workCard-episodeCount")?.TextContent ?? "";
            var episodeMatch = Regex.Match(episodeCountText, @"\d+");
            var totalEpisodes = episodeMatch.Success && int.TryParse(episodeMatch.Value, out var n) ? n : 0;

            results.Add(new SearchResult
            {
                SiteType = SiteType.Kakuyomu,
                NovelId = workId,
                Title = title,
                Author = author,
                TotalEpisodes = totalEpisodes,
                IsCompleted = isCompleted,
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
