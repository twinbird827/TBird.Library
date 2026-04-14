# LanobeReader 10項目バグ修正・改善プラン

## Context

ユーザーがLanobeReader（.NET MAUI小説リーダーアプリ）のリーダー画面・一覧画面・スクレイピングに関する10個の問題を報告。
各項目の原因を調査し、修正方針を策定する。

---

## Issue 1: レンダー画面の左上の前へボタンは不要

**原因:** `_Apps/Views/ReaderPage.xaml:29` にヘッダー左端の「◀」ボタンがある。フッター（L86-87）にも「◀ 前へ」がありUI重複。

**修正:**
- `ReaderPage.xaml:29` の `<Button Grid.Column="0" Text="◀" .../>` を削除
- ヘッダーGridの `ColumnDefinitions` を `Auto,*,Auto,Auto` → `*,Auto,Auto` に変更し、タイトルLabelを Column="0" に詰める

**対象ファイル:** `_Apps/Views/ReaderPage.xaml`

---

## Issue 2: 一覧はキャッシュ使いまわしにしたい

**原因:** `NovelListPage` は `OnAppearing` のたびに `InitializeAsync()` → DB全件再取得。エピソード一覧も同様。ナビゲーションで戻るたびにリロードが走り、ユーザー体感が遅い。

**修正方針:** ViewModelにインメモリキャッシュフラグを追加し、既にロード済みの場合は再取得をスキップする。

### NovelListViewModel の変更

```csharp
// フィールド追加
private bool _isInitialized;

// InitializeAsync を変更
public async Task InitializeAsync()
{
    if (_isInitialized) return;   // ← 追加
    SortKey = await _settingsRepo.GetValueAsync(SettingsKeys.NOVEL_SORT_KEY, "updated_desc");
    await LoadNovelsAsync();
    _isInitialized = true;        // ← 追加
}

// キャッシュ無効化が必要なメソッド（既存メソッド内に _isInitialized = false を追加）:
// - RefreshAsync()        → try ブロック先頭で _isInitialized = false
// - DeleteNovel()         → Novels.Remove 後に _isInitialized = false（次回OnAppearingで再取得させる）
// - RegisterAsync() 完了後にNovelListに戻った時  → NovelListPage側でフラグリセット

// OnSortKeyChanged は LoadNovelsAsync() を直接呼ぶので _isInitialized に影響なし（OK）
```

### NovelListPage.xaml.cs の変更

```csharp
// _viewModel.InvalidateCache() を呼ぶ公開メソッドを VM に追加するか、
// または OnAppearing をそのまま維持して VM 側で制御する（推奨）。
// 現状の OnAppearing → InitializeAsync() はそのまま。VM 側の _isInitialized で制御。
// 変更なし。
```

### EpisodeListViewModel の変更

`EpisodeListViewModel` は `IQueryAttributable` で `ApplyQueryAttributes` 経由で初期化される。
Shell ナビゲーションで「戻る」時は `ApplyQueryAttributes` は再呼出しされないため、
EpisodeListPage に `OnAppearing` を追加して既読状態のみ軽量更新する。

```csharp
// EpisodeListViewModel にメソッド追加
public async Task RefreshReadStatusAsync()
{
    if (_allEpisodes.Count == 0) return;
    
    // DB から既読状態のみ再取得
    var freshEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
    var readMap = freshEpisodes.ToDictionary(e => e.Id, e => e.IsRead == 1);
    
    // インメモリの _allEpisodes を更新
    foreach (var ep in _allEpisodes)
    {
        if (readMap.TryGetValue(ep.Id, out var isRead))
            ep.IsRead = isRead ? 1 : 0;
    }
    
    // 表示中の ViewModel も更新
    foreach (var vm in Episodes)
    {
        if (readMap.TryGetValue(vm.Id, out var isRead))
            vm.IsRead = isRead;
    }
}
```

### EpisodeListPage.xaml.cs の変更

```csharp
// OnAppearing 追加
protected override void OnAppearing()
{
    base.OnAppearing();
    if (BindingContext is EpisodeListViewModel vm)
    {
        _ = vm.RefreshReadStatusAsync();
    }
}
```

**対象ファイル:**
- `_Apps/ViewModels/NovelListViewModel.cs` (`_isInitialized` フラグ + リセット箇所)
- `_Apps/ViewModels/EpisodeListViewModel.cs` (`RefreshReadStatusAsync` 追加)
- `_Apps/Views/EpisodeListPage.xaml.cs` (`OnAppearing` 追加)

---

## Issue 3: カクヨムランキング取得できない

**原因:** `KakuyomuApiService.FetchRankingAsync` (L275) のセレクタ `a[href^='/works/']` が問題。
実際のカクヨムランキングページでは作品リンクが**絶対URL** (`https://kakuyomu.jp/works/...`) で記述されているため、`^='/works/'`（先頭一致）が一切マッチしない。

同様に、作者リンク `a[href^='/users/']` (L292) も絶対URL (`https://kakuyomu.jp/users/...`) のためマッチしない。

**検証済み:** Firecrawlでランキングページをスクレイピングし、以下を確認:
- ページは正常にSSRでレンダリングされている（200 OK）
- 作品リンクは `<a href="https://kakuyomu.jp/works/{ID}" class="widget-workCard-titleLabel">`
- 作者リンクは `<a class="widget-workCard-authorLabel" href="https://kakuyomu.jp/users/{ID}">`

**修正:**
- L275: `a[href^='/works/']` → `a[href*='/works/']`（部分一致）に変更
- L279: `/episodes/` のフィルタも `href.Contains` で既に対応済みなので問題なし
- L292: `a[href^='/users/']` → `a[href*='/users/']`（部分一致）に変更
- title取得も `link.GetAttribute("title")` ではなく `link.TextContent` をプライマリにする（ランキングページのタイトルリンクにはtitle属性がない場合がある）

**対象ファイル:** `_Apps/Services/Kakuyomu/KakuyomuApiService.cs`

---

## Issue 4: 横書きと縦書きでフォント不一致

**原因:**
- 縦書き（WebView）: `ReaderHtmlBuilder.cs:32` で `font-family:serif;` を指定
- 横書き（MAUI Label）: `ReaderPage.xaml:48` の Label に `FontFamily` 指定なし → Android/iOSデフォルトのsans-serifフォントが使われる

**修正:**
- 横書きLabelに `FontFamily` を設定してserifフォントに統一する
- Android向けには `"serif"` でネイティブのセリフフォント（Noto Serif CJK等）が使われる
- XAMLで `FontFamily="serif"` を Label に追加

**対象ファイル:** `_Apps/Views/ReaderPage.xaml`

---

## Issue 5: 縦書き時に下にスクロールできる

**原因:** `ReaderHtmlBuilder.cs:36` で `overflow-y:hidden` を設定しているが、Android WebViewではbody要素の `overflow-y:hidden` だけでは WebView 自体のスクロールを抑制できない場合がある。`writing-mode:vertical-rl` では本来、縦方向（Y軸）のスクロールは不要。

**修正:**
- CSSに `touch-action: pan-x;` を追加して、Y方向のタッチスクロールを無効化
- `html` 要素にも `overflow-y:hidden;` を追加（bodyだけでは不十分な場合がある）
- `overscroll-behavior-y: none;` を追加してオーバースクロールも防止

**対象ファイル:** `_Apps/Helpers/ReaderHtmlBuilder.cs`

---

## Issue 6: 横書き時は左右スワイプで前後話、縦書き時は上下スワイプで前後話

**原因:** 現状、スワイプジェスチャーが未実装。ナビゲーションはヘッダー/フッターのボタンのみ。

**注意:** 横書きのScrollViewは縦スクロールを使うため、左右スワイプとは干渉しない。縦書きのWebViewは横スクロール（Issue 5で縦スクロール無効化済み）なので、上下スワイプとは干渉しない。

### 横書きモード: ReaderPage.xaml の ScrollView にスワイプ追加

```xml
<!-- Content (horizontal - Label) の ScrollView 内に追加 -->
<ScrollView Grid.Row="1" x:Name="ContentScrollView" Scrolled="OnScrolled"
            IsVisible="{Binding IsHorizontal}">
    <ScrollView.GestureRecognizers>
        <SwipeGestureRecognizer Direction="Left" Command="{Binding NextEpisodeCommand}" />
        <SwipeGestureRecognizer Direction="Right" Command="{Binding PrevEpisodeCommand}" />
    </ScrollView.GestureRecognizers>
    <Label ... />
</ScrollView>
```

### 縦書きモード: ReaderHtmlBuilder.cs にスワイプ検知JS追加

既存の `read-end` 検知スクリプトの直後に追加。スクロール中の誤発火を防ぐため、以下の条件を設ける:
- Y方向の移動距離 > X方向（縦方向のスワイプのみ反応）
- Y方向の移動距離 > 80px（閾値。指の小さな動きを無視）
- タッチ開始から終了まで 300ms 以下（長押し/ドラッグを除外）

```javascript
// ReaderHtmlBuilder.cs の sb.Append("<script>") ブロック内、read-end JS の後に追加
(function(){
  var sx,sy,st;
  document.addEventListener('touchstart',function(e){
    sx=e.touches[0].clientX;sy=e.touches[0].clientY;st=Date.now();
  },{passive:true});
  document.addEventListener('touchend',function(e){
    var dx=e.changedTouches[0].clientX-sx;
    var dy=e.changedTouches[0].clientY-sy;
    var dt=Date.now()-st;
    if(dt>300)return;
    if(Math.abs(dy)>Math.abs(dx)&&Math.abs(dy)>80){
      if(dy<0)location.href='lanobe://next-episode';
      else location.href='lanobe://prev-episode';
    }
  },{passive:true});
})();
```

### ReaderPage.xaml.cs の OnWebViewNavigating に URI ハンドリング追加

```csharp
private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
{
    if (e.Url?.StartsWith("lanobe://", StringComparison.OrdinalIgnoreCase) != true) return;
    e.Cancel = true;

    if (BindingContext is not ReaderViewModel vm) return;

    if (e.Url.Contains("read-end", StringComparison.OrdinalIgnoreCase))
    {
        await vm.MarkAsReadCommand.ExecuteAsync(null);
    }
    else if (e.Url.Contains("next-episode", StringComparison.OrdinalIgnoreCase))
    {
        if (vm.NextEpisodeCommand.CanExecute(null))
            await vm.NextEpisodeCommand.ExecuteAsync(null);
    }
    else if (e.Url.Contains("prev-episode", StringComparison.OrdinalIgnoreCase))
    {
        if (vm.PrevEpisodeCommand.CanExecute(null))
            await vm.PrevEpisodeCommand.ExecuteAsync(null);
    }
}
```

**対象ファイル:**
- `_Apps/Views/ReaderPage.xaml` (SwipeGestureRecognizer 2個追加)
- `_Apps/Views/ReaderPage.xaml.cs` (OnWebViewNavigating 拡張)
- `_Apps/Helpers/ReaderHtmlBuilder.cs` (スワイプ検知JS追加)

---

## Issue 7: なろう版目次がページングされない

**原因:** `EpisodeListViewModel.InitializeAsync()` (L106-109) で、`hasChapters = true` の場合に `ApplyFilterAndShow()` で**全エピソードを一括表示**し、`MaxPage = 1` に固定している。ページングUIも `HasChapters` が true だと非表示（`EpisodeListPage.xaml:81`）。

なろうの連載小説は大半が章構造を持つため、数百〜数千話がすべて一度に表示され、スクロールが非常に重くなる。

**補足:** スクレイピング側（`NarouApiService.FetchEpisodeListAsync`）のページネーション（`.c-pager__item--next`）は正常に動作することをFirecrawlで確認済み。

### EpisodeListViewModel.InitializeAsync() の変更

`HasChapters` による分岐を削除し、常にページングを適用する。

```csharp
// Before (L98-115):
var hasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
// ...
HasChapters = hasChapters;
if (hasChapters)
{
    ApplyFilterAndShow();
    MaxPage = 1;
}
else
{
    RecalcPaging();
    await LoadPageAsync();
}

// After:
HasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
// ...
RecalcPaging();
await LoadPageAsync();
// ※ HasChapters 分岐を削除。常にページング。
```

### 章タイトルのページ境界問題への対処

ページングで `_filteredCache.Skip((page-1)*perPage).Take(perPage)` すると、
同じ章の途中でページが切れた場合、2ページ目の先頭エピソードに章名が表示されない可能性がある。

対処: `LoadPageAsync` でページ先頭エピソードの `ChapterName` が null でなければそのまま表示。
ページ先頭エピソードの `ChapterName` が前ページから継続している場合は、
`_filteredCache` 上でそのエピソードの `ChapterName` は既に設定済み（DB値をそのまま使う）なので問題なし。
→ 各 Episode の `ChapterName` はエピソード単位で保持されているため、**追加対処は不要**。

### EpisodeListPage.xaml のページングUI表示条件の変更

```xml
<!-- Before (L80-81): -->
<Grid Grid.Row="2" ... IsVisible="{Binding HasChapters, Converter={StaticResource InverseBool}}">

<!-- After (最終案): IsVisible 条件を削除して常時表示 -->
<Grid Grid.Row="2" ColumnDefinitions="*,Auto,*" Padding="8">
```

1ページしかない場合はボタンが disabled になるだけ（CanExecute で制御済み）。

### OnShowUnreadOnlyChanged / OnShowFavoritesOnlyChanged の ReloadListAsync

既に `HasChapters` 分岐があるので同様に統一:

```csharp
// Before:
private async Task ReloadListAsync()
{
    RebuildFilterCache();
    if (HasChapters)
    {
        ApplyFilterAndShow();
    }
    else
    {
        CurrentPage = 1;
        RecalcPaging();
        await LoadPageAsync();
    }
}

// After:
private async Task ReloadListAsync()
{
    RebuildFilterCache();
    CurrentPage = 1;
    RecalcPaging();
    await LoadPageAsync();
}
```

**対象ファイル:**
- `_Apps/ViewModels/EpisodeListViewModel.cs` (`InitializeAsync`, `ReloadListAsync`, `ApplyFilterAndShow` 削除可)
- `_Apps/Views/EpisodeListPage.xaml` (ページングGrid の `IsVisible` 削除)

---

## Issue 8: 日付フォーマットをyyyy/MM/dd HH:mm:ssにする

**原因:** 日付はISO 8601形式 (`2026-04-12T19:31:45.1234567Z`) で保存・表示されている。
`NovelCardViewModel.FromModel` (L52) で `LastUpdatedAt = novel.LastUpdatedAt ?? ""` と生の文字列をそのまま渡している。

**修正:**
- `NovelCardViewModel.FromModel` でISO 8601文字列をパースし、`yyyy/MM/dd HH:mm:ss` にフォーマットして渡す
- UTCで保存されているのでローカル時間に変換してから表示
- パースに失敗した場合は元の文字列をそのまま表示（後方互換）

```csharp
LastUpdatedAt = DateTime.TryParse(novel.LastUpdatedAt, null, 
    System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
    ? dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss")
    : novel.LastUpdatedAt ?? "",
```

**対象ファイル:** `_Apps/ViewModels/NovelCardViewModel.cs`

---

## Issue 9: 次話表示時にスクロール位置をリセットする

**原因:**
- **横書き:** `ReaderViewModel.LoadEpisodeAsync` で `EpisodeContent` を更新するが、`ScrollView.ScrollToAsync(0,0)` が呼ばれない → 前話のスクロール位置が残る
- **縦書き:** `RefreshHtml()` でHTML全体を差し替えるため WebView は自動リセットされるが、タイミングによっては残る可能性あり

**修正:**
- ReaderPage.xaml.cs から ScrollView のスクロールリセットを行うメソッドを公開
- ReaderViewModel にスクロールリセットを要求するイベントまたはメッセンジャーを追加
- または、よりシンプルに: `ReaderPage.xaml.cs` で `EpisodeContent` の変更を検知してスクロールリセット

**シンプル実装:** ReaderViewModelに `Action? ScrollToTop` コールバックを追加し、ReaderPage.xaml.cs の OnAppearing/コンストラクタで設定。LoadEpisodeAsync 完了時に呼び出す。

```csharp
// ReaderViewModel
public Action? ScrollToTop { get; set; }
// LoadEpisodeAsync の finally 前:
ScrollToTop?.Invoke();

// ReaderPage.xaml.cs
vm.ScrollToTop = () => ContentScrollView.ScrollToAsync(0, 0, false);
```

**対象ファイル:**
- `_Apps/ViewModels/ReaderViewModel.cs`
- `_Apps/Views/ReaderPage.xaml.cs`

---

## Issue 10: カクヨム版作者名が取得できていない

**原因（複合的）:**
1. `SearchAsync` (L61): `Author = ""` で固定。検索結果ページに作者情報がないわけではないが、抽出していない
2. `FetchRankingAsync` (L292): `a[href^='/users/']` セレクタが絶対URLにマッチしない（Issue 3と同根）
3. `FetchNovelInfoAsync` (L231-255): 作者名を一切返さない。Apollo Stateに `Work:{id}.author.__ref` → `UserAccount:{id}.activityName` として存在するが未利用
4. `RegisterAsync` (SearchViewModel L303): `Author = result.Author` で空文字がDBに保存される

**検証済み:** Apollo State内に以下の構造で著者情報が存在:
```
Work:{novelId}.author.__ref → "UserAccount:{userId}"
UserAccount:{userId}.activityName → "表示名"
```

### Step 1: INovelService シグネチャ変更

```csharp
// _Apps/Services/INovelService.cs
// Before:
Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default);

// After:
Task<(int totalEpisodes, string? lastUpdatedAt, bool isCompleted, string? author)> FetchNovelInfoAsync(string novelId, CancellationToken ct = default);
```

### Step 2: KakuyomuApiService.FetchNovelInfoAsync で著者名抽出

Apollo State から `Work:{novelId}` → `author.__ref` → `UserAccount:{id}` → `activityName` を辿る。

```csharp
// _Apps/Services/Kakuyomu/KakuyomuApiService.cs  FetchNovelInfoAsync 内

// 既存の isCompleted 取得コードの後に追加:
string? author = null;
if (apolloState is not null)
{
    var workKey = $"Work:{novelId}";
    if (apolloState.Value.TryGetProperty(workKey, out var work)
        && work.TryGetProperty("author", out var authorRef)
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

return (totalEpisodes, DateTime.UtcNow.ToString("o"), isCompleted, author);
```

### Step 3: NarouApiService.FetchNovelInfoAsync のシグネチャ合わせ

```csharp
// _Apps/Services/Narou/NarouApiService.cs  FetchNovelInfoAsync

// Before (L188):
return (totalEpisodes, lastUpdatedAt, isCompleted);

// After: writer フィールドを追加取得
// of パラメータに "w" (writer) を追加: "ga-gl-e" → "ga-gl-e-w"
var url = $"{API_BASE}?out=json&ncode={novelId}&of=ga-gl-e-w";
// ...
var author = item.TryGetProperty("writer", out var writerProp) ? writerProp.GetString() : null;
return (totalEpisodes, lastUpdatedAt, isCompleted, author);
```

### Step 4: UpdateCheckService の呼び出し箇所修正

```csharp
// _Apps/Services/UpdateCheckService.cs L52
// Before:
var (totalEpisodes, lastUpdatedAt, isCompleted) = await service.FetchNovelInfoAsync(novel.NovelId, ct).ConfigureAwait(false);

// After:
var (totalEpisodes, lastUpdatedAt, isCompleted, author) = await service.FetchNovelInfoAsync(novel.NovelId, ct).ConfigureAwait(false);

// L69-72 の novel 更新ブロック内に追加:
if (!string.IsNullOrEmpty(author) && string.IsNullOrEmpty(novel.Author))
{
    novel.Author = author;
}
```

### Step 5: SearchViewModel.RegisterAsync で作者名を補完

```csharp
// _Apps/ViewModels/SearchViewModel.cs  RegisterAsync 内
// L311 の service.FetchEpisodeListAsync 呼び出し後に追加:

// 作者名が空の場合、FetchNovelInfoAsync で補完
if (string.IsNullOrEmpty(result.Author) && dbNovel is not null)
{
    try
    {
        var (_, _, _, fetchedAuthor) = await service.FetchNovelInfoAsync(result.NovelId);
        if (!string.IsNullOrEmpty(fetchedAuthor))
        {
            dbNovel.Author = fetchedAuthor;
            // novel テーブルは後で UpdateAsync されるのでそこに含まれる
        }
    }
    catch { /* 作者名取得失敗は無視 */ }
}
```

### Step 6: FetchRankingAsync のセレクタ修正（Issue 3 と同時対応）

Issue 3 の修正で `a[href*='/users/']` に変更されるため、ランキング経由の作者名取得も修復される。

**対象ファイル:**
- `_Apps/Services/INovelService.cs` (シグネチャ変更)
- `_Apps/Services/Kakuyomu/KakuyomuApiService.cs` (Apollo State著者名抽出 + セレクタ修正)
- `_Apps/Services/Narou/NarouApiService.cs` (シグネチャ合わせ + `of` パラメータ拡張)
- `_Apps/Services/UpdateCheckService.cs` (分割代入変更 + author 補完)
- `_Apps/ViewModels/SearchViewModel.cs` (RegisterAsync での作者名補完)

---

## 実装順序

依存関係を考慮した推奨順:

| 順 | Issue | 理由 |
|----|-------|------|
| 1 | #1 ヘッダーボタン削除 | 単純なXAML変更、他に依存なし |
| 2 | #8 日付フォーマット | 単純なViewModel変更 |
| 3 | #4 フォント統一 | 単純なXAML/CSS変更 |
| 4 | #5 縦書きスクロール抑制 | CSS変更のみ |
| 5 | #9 スクロールリセット | VM + Page連携 |
| 6 | #3 カクヨムランキング | セレクタ修正 |
| 7 | #10 カクヨム作者名 | Issue 3のセレクタ修正を含む + インターフェース変更 |
| 8 | #7 目次ページング | ViewModel ロジック変更 |
| 9 | #2 一覧キャッシュ | ViewModel + Page変更 |
| 10 | #6 スワイプジェスチャー | XAML + JS + Page、最も複雑 |

---

## 検証方法

- 各Issue修正後に `dotnet build _Apps/App.sln` でビルド確認
- Android エミュレータまたは実機でUIの動作確認
  - Issue 1: ヘッダーに「◀」ボタンがないこと
  - Issue 2: 画面遷移して戻った際にリロードが走らないこと
  - Issue 3: カクヨムランキング取得でエラーにならず結果が表示されること
  - Issue 4: 横書き/縦書き切替でフォントが同じserifになること
  - Issue 5: 縦書き時にY方向スクロールが効かないこと
  - Issue 6: 横書きで左右スワイプ、縦書きで上下スワイプでナビゲーションできること
  - Issue 7: なろう小説の章あり目次でページングが動作すること
  - Issue 8: 小説一覧の更新日時が `yyyy/MM/dd HH:mm:ss` 形式で表示されること
  - Issue 9: 次話/前話遷移時にスクロール位置が先頭にリセットされること
  - Issue 10: カクヨム小説の作者名が正しく表示されること
