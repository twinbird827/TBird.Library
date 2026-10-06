using System.Text.Json;
using LanobeReader.Helpers;
using LanobeReader.Models;

namespace LanobeReader.Services.Narou;

// novelapi 応答の解析を NarouApiService から切り出したもの。LanobeReader.Tests がこのファイル単体を
// リンク取り込みしてテストするため、BCL・SearchResult・SiteType・NarouDateTime 以外(LanobeReader の他の型・MAUI)に依存しない。
public static class NarouNovelApiParser
{
    public static List<SearchResult> Parse(string json)
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
                // general_lastup(JST 生値)は UTC ISO へ正規化して保存する。詳細は NarouDateTime 参照。
                LastUpdatedAt = item.TryGetProperty("general_lastup", out var lastup) ? NarouDateTime.ToUtcIso(lastup.GetString()) : null,
            });
        }
        return results;
    }
}
