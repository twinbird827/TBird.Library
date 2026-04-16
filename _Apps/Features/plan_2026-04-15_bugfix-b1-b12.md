# BUG修正プラン B1-B12（2026-04-15）

## Context

2026-04-14 に実施した _Apps フォルダ全57ファイルのコードレビュー（[todo_2026-04-14_code-review.md](todo_2026-04-14_code-review.md)）で発見された 12件の BUG を一括修正する。現在ブランチ `app-novelviewer`。

## 対象ファイル（9ファイル）

- `_Apps/Services/UpdateCheckService.cs` (B1)
- `_Apps/Platforms/Android/UpdateCheckWorker.cs` (B2, B3, B4)
- `_Apps/Platforms/Android/NotificationHelper.cs` (B5, B6)
- `_Apps/Platforms/Android/MainActivity.cs` (B5)
- `_Apps/Services/Network/NetworkPolicyService.cs` (B7)
- `_Apps/Models/RankingPeriod.cs` (B8)
- `_Apps/Services/Narou/NarouApiService.cs` (B8)
- `_Apps/Services/Kakuyomu/KakuyomuApiService.cs` (B9)
- `_Apps/Services/Background/BackgroundJobQueue.cs` (B10)
- `_Apps/ViewModels/SettingsViewModel.cs` (B11)
- `_Apps/ViewModels/NovelListViewModel.cs` (B12)

---

## B1: UpdateCheckService.cs — throw で foreach 中断

**問題（L101-107）:** `catch` 内の `throw;` が foreach ループ内にあり、1小説のHTTPエラーで全チェック中断＋L110-115 のエラーフラグリセットが到達不能。

**注意点:** `throw` を `continue` にするだけでは、今回失敗した小説も L111 の `novels.Where(n => n.HasCheckError == 1)` でマッチしてリセットされてしまう（L104 で在メモリオブジェクトの `HasCheckError = 1` を設定済みのため）。

**修正:**
1. L40 `var updates = new List<(Novel, int)>();` の後に追加:
   ```csharp
   var failedIds = new HashSet<int>();
   ```
2. L101-107 の catch ブロックを置換:
   ```csharp
   catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
   {
       LogHelper.Warn(nameof(UpdateCheckService), $"Failed to check {novel.Title}: {ex.Message}");
       novel.HasCheckError = 1;
       await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
       failedIds.Add(novel.Id);
       continue;
   }
   ```
3. L111 の filter を変更:
   ```csharp
   foreach (var novel in novels.Where(n => n.HasCheckError == 1 && !failedIds.Contains(n.Id)))
   ```

---

## B2: UpdateCheckWorker.cs — GetAwaiter().GetResult() デッドロックリスク

**問題（L34, L39）:** `Task.Run(() => ...).GetAwaiter().GetResult()` が脆弱。

**判断:** Android Worker スレッドには SynchronizationContext がなく、`Task.Run` + `GetAwaiter().GetResult()` は事実上安全。B1 修正により `CheckAllAsync` が例外をスローしなくなり到達リスクも低下。**構造変更は不要**、コメントのみ追加。

**修正:** L34 の上にコメント追加:
```csharp
// Worker threads have no SynchronizationContext, so blocking on Task.Run is safe here
```

---

## B3: UpdateCheckWorker.cs — 到達不能な catch 削除

**問題（L55-59）:** 元の指摘は「一時的障害に `Result.InvokeFailure()` を返しリトライしない」だったが、**B1 修正後は `CheckAllAsync` が `HttpRequestException`/`TaskCanceledException` を投げなくなる**（各小説でエラーを握り潰し `continue`）。ワーカー内で残る処理（`GetFirstUnreadEpisodeAsync` = DB アクセス、`NotificationHelper.ShowUpdateNotification`）は HTTP 例外を投げないため、この catch は到達不能になる。

**修正:** L55-60 の `catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ... }` ブロック全体を削除。下の汎用 `catch (Exception ex)`（L61-65）のみ残す。

- `ShowErrorNotification` 呼び出しも同時に消える（リトライ経路がなくなるため本プランではリトライ機能自体を見送り。将来ネットワーク起因のリトライが必要になったら別途 `InvokeRetry()` 対応を検討）

---

## B4: UpdateCheckWorker.cs — DB 未初期化

**問題（L23-34）:** WorkManager はアプリ停止中でも Worker を起動。DB 初期化が保証されない。

**調査結果:** 全リポジトリメソッドが内部で `_dbService.EnsureInitializedAsync()` を呼ぶため暗黙的に初期化はされる。ただし明示呼び出し無しは意図が不明瞭で、将来のリグレッションリスクあり。

**修正:** L32 と L34 の間に明示的な初期化呼び出し追加:
```csharp
// Ensure DB is initialized (Worker may run before app startup completes)
dbService.EnsureInitializedAsync().GetAwaiter().GetResult();
```
- Worker スレッドには SynchronizationContext がないため `Task.Run` ラップは不要（B2 の判断と一貫）

---

## B5: NotificationHelper.cs + MainActivity.cs — POST_NOTIFICATIONS 権限

**問題:** Android 13+（API 33+）で `POST_NOTIFICATIONS` ランタイム権限が必須。`SupportedOSPlatformVersion=34` なので全ユーザーが該当。現状権限チェック・リクエストが一切ない。

**修正（NotificationHelper.cs）:**
1. `CreateNotificationChannels` の後（L33 の後）に権限判定メソッド追加:
   ```csharp
   public static bool HasNotificationPermission(Context context)
   {
       if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return true;
       return AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
           context, Android.Manifest.Permission.PostNotifications)
           == Android.Content.PM.Permission.Granted;
   }
   ```
2. `ShowUpdateNotification` の先頭（L37 `var intent = ...` の前）に追加:
   ```csharp
   if (!HasNotificationPermission(context)) return;
   ```
3. `ShowErrorNotification` の先頭（L62 `var notification = ...` の前）にも同様に追加

**修正（MainActivity.cs）:**
- L23 `UpdateCheckScheduler.SchedulePeriodicCheck(this);` の後に追加:
  ```csharp
  // Request notification permission for Android 13+
  if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
      && AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
          this, Android.Manifest.Permission.PostNotifications)
          != Permission.Granted)
  {
      AndroidX.Core.App.ActivityCompat.RequestPermissions(
          this, new[] { Android.Manifest.Permission.PostNotifications }, 1001);
  }
  ```
- `using Android.Content.PM;` は既に存在（L3）

---

## B6: NotificationHelper.cs — エラー通知ID固定値 9999

**問題（L71）:** 全エラー通知が ID `9999` 固定。複数エラーが上書き＋novel.Id=9999 と衝突リスク。

**方針:** 「現在アクティブなエラー通知のID最小値 - 1」を採番する。負のID空間を使うことで novel.Id（正の自動採番）と衝突不可能、かつ既存通知を上書きしない。`NotificationManager.GetActiveNotifications()` は API 23+ で利用可能、本プロジェクトは `SupportedOSPlatformVersion=34` のため問題なし。

**修正:** L71 を以下に置換:
```csharp
var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
int errorId = -1;
if (manager is not null)
{
    var minActive = manager.GetActiveNotifications()?
        .Select(n => n.Id)
        .Where(id => id < 0)
        .DefaultIfEmpty(0)
        .Min() ?? 0;
    errorId = minActive - 1;
}
NotificationManagerCompat.From(context)?.Notify(errorId, notification!);
```
- 初回は -1、次に -2、... と減少。ユーザーが通知を dismiss するとアクティブ集合から除外され、再利用可能になる
- 正のIDは使わないため novel.Id とは絶対に衝突しない
- `using System.Linq;` は [NotificationHelper.cs](../Platforms/Android/NotificationHelper.cs) に未追加のため先頭に追加が必要

---

## B7: NetworkPolicyService.cs — GetStreamAsync を削除

**問題（L102-117）:** Stream 返却前にセマフォ解放しておりレート制限が機能しない。

**調査結果:** `_Apps` 内で `GetStreamAsync` を呼ぶ箇所はゼロ（Grep 確認済み）。デッドコード。

**修正:** L99-117 の `GetStreamAsync` メソッド全体を削除。将来 gzip 解凍等で再必要になった場合は、呼び出し元と一緒に正しい設計（Stream 読了まで gate を保持 or byte array 返却）で再導入する。

---

## B8: NarouApiService.cs — BuildRtype の Yearly/All 未処理

**問題（L254-261）:** switch で `Yearly`/`All` がデフォルト（Daily）に fallthrough。

**調査結果:** なろう rankget API 公式仕様（https://dev.syosetu.com/man/rankget/）は `d/w/m/q` のみサポート。Yearly/All はそもそも API 側に存在しない。また UI（[SearchViewModel.cs:192](../ViewModels/SearchViewModel.cs#L192)）は `Math.Clamp(RankingPeriodIndex, 0, 3)` で Daily〜Quarterly のみ採用しており、Yearly/All は使われていない。

**方針:** enum から Yearly/All を削除し、そもそも渡せないようにする。switch は型安全になり default ケース不要。

**修正1（[_Apps/Models/RankingPeriod.cs](../Models/RankingPeriod.cs)）:** Yearly, All を削除:
```csharp
public enum RankingPeriod
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
}
```

**修正2（NarouApiService.cs L254-261）:** switch 式を網羅的に:
```csharp
return period switch
{
    RankingPeriod.Daily => $"{dailyTarget:yyyyMMdd}-d",
    RankingPeriod.Weekly => $"{NearestTuesday(today):yyyyMMdd}-w",
    RankingPeriod.Monthly => $"{new DateTime(today.Year, today.Month, 1):yyyyMMdd}-m",
    RankingPeriod.Quarterly => $"{new DateTime(today.Year, today.Month, 1):yyyyMMdd}-q",
    _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
};
```

**影響確認:** `RankingPeriod.Yearly`/`RankingPeriod.All` への参照は `_Apps` 内でゼロ（Grep で BuildRtype の switch 以外にヒットなし）。削除による破綻は起きない。

---

## B9: KakuyomuApiService.cs — 毎回 TOC フェッチ

**問題（L205-221）:** `FetchEpisodeContentAsync` が毎回 TOC ページをフェッチ。100話プリフェッチで100回 TOC 取得。

**修正:**
1. L1 の `using System.Text.Json;` の後に追加:
   ```csharp
   using System.Collections.Concurrent;
   ```
2. L15 `private readonly NetworkPolicyService _network;` の後にフィールド追加:
   ```csharp
   private readonly ConcurrentDictionary<string, (DateTime cachedAt, List<string> episodeIds)> _episodeIdCache = new();
   private static readonly TimeSpan EpisodeIdCacheTtl = TimeSpan.FromMinutes(5);
   ```
3. `ParseEpisodeIdsFromApolloState` の後にヘルパー追加:
   ```csharp
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
   ```
4. `FetchEpisodeContentAsync` の L210-213（コメント＋TOC フェッチ3行）を以下1行に置換:
   ```csharp
   var episodeIds = await GetEpisodeIdsAsync(novelId, cts.Token).ConfigureAwait(false);
   ```
5. `FetchNovelInfoAsync` の L253 もキャッシュ更新を兼ねて置換:
   ```csharp
   // 更新チェックでフェッチした最新TOCでキャッシュを上書きする。
   // これにより直後の Prefetch が古いエピソードIDリストを使うリスクを防ぐ。
   var episodeIds = ParseEpisodeIdsFromApolloState(html);
   _episodeIdCache[novelId] = (DateTime.UtcNow, episodeIds);
   var totalEpisodes = episodeIds.Count;
   ```

- `KakuyomuApiService` は DI singleton（MauiProgram.cs L47）なのでキャッシュはアプリ起動中保持される
- シングルトンで `ConcurrentDictionary` を使うためスレッドセーフ
- キャッシュ整合性の依存関係: 新エピソード取得 → `FetchNovelInfoAsync` がキャッシュ更新 → Prefetch が新しいIDを参照、の順序で動く。`UpdateCheckService.CheckAllAsync` が `FetchNovelInfoAsync` を先に呼び、その後 `PrefetchEpisodeJob` を Enqueue する設計に依存しているため、この呼び出し順を変更する場合はキャッシュ更新ロジックの見直しが必要

---

## B10: BackgroundJobQueue.cs — CTS 未 Dispose

**問題（L76, L86）:** `EnsureWorkerStarted` で新 CTS 作成時に旧 CTS の Dispose なし。`StopWorker` で Cancel のみ Dispose なし、空 catch で例外握りつぶし。

**修正（EnsureWorkerStarted L76 の前）:** 1行追加:
```csharp
_workerCts?.Dispose();
_workerCts = new CancellationTokenSource();
```

**修正（StopWorker L82-88 全体）:** 以下に置換:
```csharp
public void StopWorker()
{
    lock (_startLock)
    {
        try { _workerCts?.Cancel(); }
        catch (ObjectDisposedException) { }
        finally
        {
            _workerCts?.Dispose();
            _workerCts = null;
        }
    }
}
```
- 空 `catch` を `ObjectDisposedException` のみに絞り込み（想定外例外は握りつぶさない）

---

## B11: SettingsViewModel.cs — InitializeAsync で OnXxxChanged 発火

**問題（L49-59）:** プロパティ代入→CommunityToolkit.Mvvm が `OnXxxChanged` 発火→`_ = _settingsRepo.SetValueAsync(...)` で DB から読んだ同じ値を書き戻し（9項目）。

**修正:**
1. L11 `private readonly EpisodeCacheRepository _cacheRepo;` の後にフィールド追加:
   ```csharp
   private bool _isInitializing;
   ```
2. `InitializeAsync`（L49-60）全体を以下に置換:
   ```csharp
   public async Task InitializeAsync()
   {
       _isInitializing = true;
       try
       {
           CacheMonths = await _settingsRepo.GetIntValueAsync(SettingsKeys.CACHE_MONTHS, SettingsKeys.DEFAULT_CACHE_MONTHS);
           UpdateIntervalHours = await _settingsRepo.GetIntValueAsync(SettingsKeys.UPDATE_INTERVAL_HOURS, SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS);
           FontSizeSp = await _settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, SettingsKeys.DEFAULT_FONT_SIZE_SP);
           BackgroundTheme = await _settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, SettingsKeys.DEFAULT_BACKGROUND_THEME);
           LineSpacing = await _settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, SettingsKeys.DEFAULT_LINE_SPACING);
           EpisodesPerPage = await _settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, SettingsKeys.DEFAULT_EPISODES_PER_PAGE);
           VerticalWriting = await _settingsRepo.GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, SettingsKeys.DEFAULT_VERTICAL_WRITING) == 1;
           PrefetchEnabled = await _settingsRepo.GetIntValueAsync(SettingsKeys.PREFETCH_ENABLED, SettingsKeys.DEFAULT_PREFETCH_ENABLED) == 1;
           RequestDelayMs = await _settingsRepo.GetIntValueAsync(SettingsKeys.REQUEST_DELAY_MS, SettingsKeys.DEFAULT_REQUEST_DELAY_MS);
       }
       finally
       {
           _isInitializing = false;
       }
   }
   ```
3. 全9個の `OnXxxChanged`（L62-87）を block-bodied に変更してガード追加。例（`OnCacheMonthsChanged`）:
   ```csharp
   partial void OnCacheMonthsChanged(int value)
   {
       if (_isInitializing) return;
       _ = _settingsRepo.SetValueAsync(SettingsKeys.CACHE_MONTHS, value.ToString());
   }
   ```
   対象: `OnCacheMonthsChanged`, `OnUpdateIntervalHoursChanged`, `OnFontSizeSpChanged`, `OnBackgroundThemeChanged`, `OnLineSpacingChanged`, `OnEpisodesPerPageChanged`, `OnVerticalWritingChanged`, `OnPrefetchEnabledChanged`, `OnRequestDelayMsChanged`

---

## B12: NovelListViewModel.cs — 初期化時 LoadNovelsAsync 2回実行

**問題（L45-60, L78-82）:** `InitializeAsync` の L49 `SortKey = ...` 代入で `OnSortKeyChanged` 発火 → L81 `_ = LoadNovelsAsync()` + L58 `await LoadNovelsAsync()` で計2回ロード。

**修正:**
1. L43 `private bool _needsReload = true;` の後にフィールド追加:
   ```csharp
   private bool _isInitializing;
   ```
2. `InitializeAsync` の SortKey 代入をフラグで囲む（L47-51 置換）:
   ```csharp
   if (!_sortKeyLoaded)
   {
       _isInitializing = true;
       try
       {
           SortKey = await _settingsRepo.GetValueAsync(SettingsKeys.NOVEL_SORT_KEY, "updated_desc");
       }
       finally
       {
           _isInitializing = false;
       }
       _sortKeyLoaded = true;
   }
   ```
3. `OnSortKeyChanged`（L78-82）にガード追加:
   ```csharp
   partial void OnSortKeyChanged(string value)
   {
       if (_isInitializing) return;
       _ = _settingsRepo.SetValueAsync(SettingsKeys.NOVEL_SORT_KEY, value);
       _ = LoadNovelsAsync();
   }
   ```

---

## 実装順序

依存関係と影響範囲の小さい順:

1. **B11, B12** — ViewModel 初期化ガード（独立・軽量）
2. **B10** — CTS Dispose（独立・軽量）
3. **B1** — UpdateCheckService throw 修正（B3 の前提）
4. **B3, B4, B2** — UpdateCheckWorker 一式（B1 に依存）
5. **B5, B6** — 通知関連（独立、MainActivity も変更）
6. **B7** — GetStreamAsync（独立）
7. **B8** — BuildRtype（独立）
8. **B9** — TOC キャッシュ（やや複雑、最後）

## 検証

- **ビルド:** `dotnet build _Apps/App.sln --no-restore` で net9.0-android ビルド成功を確認
- **B1:** 複数小説登録状態でネットワーク切断→更新実行→ログで「Failed to check ...」の後も残りがチェックされていること
- **B3:** オフライン状態で Worker 実行→`Result.InvokeRetry()` 経路を logcat で確認
- **B5:** Android 13+ 端末で権限未許可時にクラッシュなく通知がスキップされること、初回起動で権限ダイアログが出ること
- **B9:** 同一カクヨム小説で5話連続 Prefetch→ TOC フェッチが1回のみ（ログまたはネットワークで確認）
- **B11:** 設定ページを開いた時に DB 書き込みが発生しないこと（SQLiteブレークポイント等）
- **B12:** 小説一覧初回表示時に `LoadNovelsAsync` が1回のみ実行されること（ログ計装）

## PR

ブランチ名: `feature/bugfix-b1-b12`（`app-novelviewer` から切る）
PR 本文に本プランファイルへのリンクと、修正した各 BUG の一覧を記載。

---

## 実装時の計画からの逸脱（2026-04-16 追記）

実装後に検証した結果、以下3点で計画と異なる対応を取った。いずれもビルド成功・動作健全を確認済み。

### B5: 権限リクエストを MAUI Essentials 流に変更
- **計画**: `MainActivity.cs:OnCreate` で直接 `ActivityCompat.RequestPermissions(this, ..., 1001)` を呼ぶ Android ネイティブ実装
- **実装**: `MainActivity.cs` は未変更。代わりに以下を新設:
  - [PostNotificationsPermission.cs](../Platforms/Android/PostNotificationsPermission.cs): `Permissions.BasePlatformPermission` を継承
  - [NotificationPermissionService.cs](../Services/NotificationPermissionService.cs): `Permissions.RequestAsync<>` 経由でリクエスト、`ShouldShowRationale` 時は `Shell.Current.DisplayAlert` で説明
  - [MauiProgram.cs:50](../MauiProgram.cs#L50) で DI 登録
  - [NovelListViewModel.InitializeAsync](../ViewModels/NovelListViewModel.cs#L63) から `EnsureRequestedAsync()` を呼び出し
- **逸脱の理由**: MAUI Essentials の `Permissions` API は内部で Android 13+ 判定・OS 差異を吸収するため、ネイティブ Android API を直接呼ぶより保守性が高い。Rationale ダイアログも統合できる
- **副作用（要承認事項）**:
  - 権限リクエスト時機が「アプリ起動時」→「小説一覧画面の初回表示時」に変化。ユーザは初回起動直後に通知ダイアログを見ない
  - `Services/NotificationPermissionService.cs` が `Platforms/Android/PostNotificationsPermission` を参照しており、`Services/` レイヤが `Platforms/Android/` に依存。現在 `net9.0-android` 単一ターゲットのためビルドは通るが、将来 iOS/Windows ターゲット追加時は要分離

### B6: `ShowErrorNotification` を修正ではなく削除
- **計画**: 負の ID を採番するロジックに置換（`NotificationManager.GetActiveNotifications()` ベース）
- **実装**: `ShowErrorNotification` メソッド本体および `ERROR_CHANNEL_ID` 定数・errorChannel 作成コードを [NotificationHelper.cs](../Platforms/Android/NotificationHelper.cs) から全削除
- **逸脱の理由**: B3 の修正で `UpdateCheckWorker` 内の `HttpRequestException` catch ブロックを削除したため `ShowErrorNotification` の唯一の呼び出し元が消えた。Grep で `_Apps` 配下に他参照ゼロを確認したため、メソッド自体を削除する方がコードがクリーン
- **将来再導入時の注意**: 元計画の負 ID 採番ロジックを参照のこと

### B10: `StopWorker` を計画より防御的に実装
- **計画**: lock 内で `Cancel()` → `Dispose()` を同期実行、`_workerCts = null` で終了
- **実装**: [BackgroundJobQueue.cs:83-105](../Services/Background/BackgroundJobQueue.cs#L83-L105) で以下の構造に変更:
  1. lock 内では `oldCts`/`oldTask` をローカルに退避し `_workerCts`/`_workerTask` を即 null 化
  2. lock 解放後に `oldCts.Cancel()`
  3. `oldTask` が存在すれば `ContinueWith` で Worker 完了後に `Dispose`、なければ即 Dispose
- **逸脱の理由**: 計画通り lock 内で同期 Dispose すると、Worker タスクが `ct` を参照中（`Task.Delay(ct)` など）に CTS が破棄され `ObjectDisposedException` が `WorkerLoopAsync` 内で発生するリスクがある。`ContinueWith` で完了を待つことで安全に Dispose できる
- **`EnsureWorkerStarted` 側**: 計画通り L76 に `_workerCts?.Dispose();` を追加（こちらは L72 のチェックで Worker 完了済が保証されるため安全）
