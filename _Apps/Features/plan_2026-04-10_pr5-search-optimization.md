# PR5: SearchViewModel改善（H8 + M7）

作成日: 2026-04-10
対象: `_Apps` — SearchViewModel, NovelRepository
担当項目: H8（ShowResultsAsync N+1クエリ）, M7（FetchRanking/FetchGenre エラー不可視）

## Context

PR4で検索の両サイト並列化を実施した結果、以下が顕在化:
- H8: ヒット数が倍増（最大60件）→ N+1クエリの体感劣化が深刻に
- M7: 両サイト同時失敗ケースが現実的に → エラーがUIに伝わらない

本PRはPR4の「後始末」として、SearchViewModelの残課題を集中対応する。

## 変更ファイル一覧

1. `_Apps/Services/Database/NovelRepository.cs`
2. `_Apps/ViewModels/SearchViewModel.cs`

## H8 — ShowResultsAsync N+1クエリ解消

### NovelRepository.cs — バルクメソッド追加（line 124付近）

```csharp
public async Task<HashSet<(int SiteType, string NovelId)>> GetExistingSiteNovelIdsAsync()
{
    await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
    var novels = await _db.Table<Novel>().ToListAsync().ConfigureAwait(false);
    return new HashSet<(int, string)>(novels.Select(n => (n.SiteType, n.NovelId)));
}
```

設計判断: novels数はユーザーの個人ライブラリ（想定<1000件）なので全件取得+メモリフィルタで十分。`WHERE (site_type, novel_id) IN (...)`のraw SQLはSQLiteの制約（tuple IN非対応）で冗長になるため避ける。

### SearchViewModel.cs — ShowResultsAsync書き換え（lines 265-275）

現在:
```csharp
var viewModels = new List<SearchResultViewModel>();
foreach (var result in results)
{
    var existing = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
    viewModels.Add(SearchResultViewModel.FromModel(result, existing is not null));
}
```

変更後:
```csharp
var existingIds = await _novelRepo.GetExistingSiteNovelIdsAsync();
var viewModels = results.Select(r =>
    SearchResultViewModel.FromModel(r, existingIds.Contains(((int)r.SiteType, r.NovelId)))
).ToList();
```

## M7 — FetchRankingAsync / FetchGenreAsync エラー可視化

### FetchRankingAsync（lines 222-226付近）

現在:
```csharp
foreach (var r in siteResults)
{
    if (r.error is not null)
        LogHelper.Warn(nameof(SearchViewModel), r.error);
}
```

変更後（`SearchAsync` lines 165-169と同じパターン）:
```csharp
var errors = siteResults.Select(r => r.error).Where(e => e is not null).ToList();
if (errors.Count > 0)
{
    HasError = true;
    ErrorMessage = string.Join("\n", errors);
}
```

### FetchGenreAsync（lines 254-258付近）

同一パターンを適用。

### catch追加: FetchRankingAsync / FetchGenreAsync

`SearchAsync`にはcatch(Exception)があるが、`FetchRankingAsync`/`FetchGenreAsync`にはない。`ShowResultsAsync`等でのunexpected例外をカバーするためcatchを追加:

```csharp
catch (Exception ex)
{
    LogHelper.Error(nameof(SearchViewModel), $"Ranking fetch failed: {ex.Message}");
    HasError = true;
    ErrorMessage = "通信エラーが発生しました";
}
```

FetchGenreAsyncも同様（メッセージは `"Genre fetch failed: ..."` に変更）。

## 変更サマリ

| ファイル | 変更 | 行数 |
|---|---|---|
| NovelRepository.cs | `GetExistingSiteNovelIdsAsync` 追加 | +7 |
| SearchViewModel.cs | ShowResultsAsync 1クエリ化 | +4/-7 |
| SearchViewModel.cs | FetchRankingAsync エラー+catch | +12/-4 |
| SearchViewModel.cs | FetchGenreAsync エラー+catch | +12/-4 |
| **合計** | | ~42行 |

## 検証方法

- `dotnet build _Apps/App.sln` 成功
- アプリ起動 → 検索 → キーワード検索実行 → 結果が正しく表示される（「登録済」表示が正しいこと）
- ランキング取得 → エラー時にUI上部にエラーメッセージが赤字で表示される
- ジャンル別取得 → 同上
