# PR2: データ層パフォーマンス改善（H1 + H5）

作成日: 2026-04-08
対象ブランチ: `app-novelviewer` から派生する作業ブランチ（例: `feature/pr2-data-perf-20260408`）
前提: PR1（未コミット変更の確定 / Converter 整理の土台）はマージ済み

このドキュメントは **追加調査なしで別セッションが実装可能** なレベルを目標に書かれている。必要な型情報・スキーマ・呼び出し元・DI 構成・完成形コードをすべて含む。

---

関連: 全課題マスター一覧は [audit_2026-04-08_apps-refactor.md](audit_2026-04-08_apps-refactor.md) を参照。本 PR は H1 / H5 に対応する。

## 0. 目的

`_Apps`（LanobeReader, .NET MAUI Android）のデータ取得処理における 2 点のパフォーマンス問題を解消する。

- **H1**: `NovelListViewModel.LoadNovelsAsync` の N+1 クエリ解消
- **H5**: `EpisodeListViewModel` のフィルタ＆ページング重複 LINQ の解消

UI/XAML・ナビゲーション・Search/Reader/Settings には一切手を入れない。変更は Repository 1 本 + ViewModel 2 本のみに閉じる。

---

## 1. 事前に把握しておくべき事実（調査済み）

### 1.1 DB スキーマ（確認済み）

#### `novels` テーブル — `Models/Novel.cs`
| 列名 | C# プロパティ | 型 | 備考 |
|---|---|---|---|
| `id` | `Id` | int | PK, AutoIncrement |
| `site_type` | `SiteType` | int | |
| `novel_id` | `NovelId` | string | サイト側 ID |
| `title` | `Title` | string | |
| `author` | `Author` | string | |
| `total_episodes` | `TotalEpisodes` | int | |
| `is_completed` | `IsCompleted` | int | 0/1 |
| `last_updated_at` | `LastUpdatedAt` | string? | ISO 8601 |
| `registered_at` | `RegisteredAt` | string | ISO 8601 |
| `has_unconfirmed_update` | `HasUnconfirmedUpdate` | int | 0/1 |
| `has_check_error` | `HasCheckError` | int | 0/1 |
| `is_favorite` | `IsFavorite` | int | 0/1 |
| `favorited_at` | `FavoritedAt` | string? | |

`SiteTypeEnum` は `[Ignore]` プロパティなので DB マッピング対象外。

#### `episodes` テーブル — `Models/Episode.cs`
主要列のみ: `id`, `novel_id`, `episode_no`, `chapter_name`, `title`, `is_read` (0/1), `read_at`, `published_at`, `is_favorite` (0/1), `favorited_at`。`novel_id` に `idx_episodes_novel_id` インデックスあり。

### 1.2 sqlite-net のカラムマッピング仕様

`sqlite-net-pcl` の `QueryAsync<T>` は、SELECT 結果の**列名**と `T` の `[Column("xxx")]` 属性を照合してマッピングする。既定では大文字小文字を区別しない。`T` に存在しない列は無視される。したがって:

- `Novel` 派生クラスに `[Column("unread_count")] int UnreadCount` を追加した型を用意し、`SELECT n.*, ... AS unread_count` で一括マッピングできる。
- 派生クラスを別テーブルとして扱わせないため、クラスに `[Table]` 属性は付けない。`QueryAsync<T>` は `[Table]` 不要で動作する（`Table<T>()` LINQ API とは異なる）。

### 1.3 既存の `NovelRepository.GetAllAsync` 呼び出し元（Grep 済み）

| ファイル | 呼び方 | 用途 | 本 PR で影響 |
|---|---|---|---|
| `ViewModels/NovelListViewModel.cs:55` | `GetAllAsync(SortKey)` | 一覧表示 | **差し替え対象** |
| `Services/UpdateCheckService.cs:39` | `GetAllAsync()`（引数なし） | 更新チェック巡回 | **温存** |
| `Services/Background/PrefetchService.cs:64` | `GetAllAsync()`（引数なし） | バックグラウンド取得 | **温存** |

→ 既存の `GetAllAsync()` / `GetAllAsync(string)` は**削除せず温存**する。新メソッドを追加するのみ。

### 1.4 `EpisodeRepository.CountUnreadByNovelIdAsync` の参照（Grep 済み）

`NovelListViewModel.cs:60` 以外で使われていない。本 PR で唯一の呼び出し元が消えるが、**メソッド自体は残す**（スコープ最小化）。

### 1.5 `NovelListViewModel` 内での `_episodeRepo` 使用箇所（Grep 済み）

- `:13` フィールド宣言
- `:26` コンストラクタ代入
- `:60` `CountUnreadByNovelIdAsync` 呼び出し

→ `:60` を消すと `_episodeRepo` は完全未使用になる。コンストラクタ引数ごと**削除**する。

### 1.6 DI 構成（`MauiProgram.cs:52` 確認済み）

```csharp
builder.Services.AddTransient<NovelListViewModel>();
```

コンテナは MS.DI の自動コンストラクタ解決。コンストラクタから `EpisodeRepository` を外しても**他の登録変更は不要**。

### 1.7 `NovelCardViewModel.FromModel` のシグネチャ（確認済み）

```csharp
public static NovelCardViewModel FromModel(Novel novel, int unreadCount)
```

引数に `Novel` インスタンスと未読数の `int` を受ける。既存のまま使える。

### 1.8 既存 `GetAllAsync(sortKey)` の ORDER BY 対応表（`NovelRepository.cs:22-41` より）

| sortKey | ORDER BY（移植先で再現すべき順序） |
|---|---|
| `updated_desc` （既定） | `n.last_updated_at DESC` |
| `updated_asc` | `n.last_updated_at ASC` |
| `title_asc` | `n.title ASC` |
| `title_desc` | `n.title DESC` |
| `author_asc` | `n.author ASC` |
| `registered_desc` | `n.registered_at DESC` |
| `unread_desc` | `unread_count DESC, n.last_updated_at DESC`（既存のサブクエリ版と同結果） |
| `favorite_first` | `n.is_favorite DESC, n.last_updated_at DESC` |

> 注: sqlite-net の `Table<T>().OrderBy(x => x.Title)` は SQL 化時に大小文字を気にしない `COLLATE NOCASE` を**付けない**。生 SQL 移植版も `COLLATE` 指定なしで揃え、順序の差異をなくす。

---

## 2. 変更対象ファイル（全 3 ファイル）

1. `_Apps/Services/Database/NovelRepository.cs`  — メソッド 1 個追加、内部 row 型 1 個追加、`record` 1 個追加
2. `_Apps/ViewModels/NovelListViewModel.cs`  — `LoadNovelsAsync` 書き換え、`EpisodeRepository` 依存削除
3. `_Apps/ViewModels/EpisodeListViewModel.cs`  — フィルタキャッシュ導入、`FilteredEpisodes()` 削除

**触らないファイル**: View/XAML、Converters、Styles、Search/Reader/Settings 系、Services/Background、Models、MauiProgram.cs、EpisodeRepository.cs。

---

## 3. 実装詳細

### 3.1 `NovelRepository.cs` の変更

#### 3.1.1 追加する型（ファイル末尾、`NovelRepository` クラスの**外**、同一名前空間内）

```csharp
// using LanobeReader.Models; は既に using 済み

/// <summary>
/// 小説と未読話数をひとまとめにした DTO。
/// H1 対応で N+1 を解消するための集約クエリ戻り値。
/// </summary>
public sealed record NovelWithUnread(Novel Novel, int UnreadCount);
```

#### 3.1.2 追加する内部クラス（`NovelRepository` クラス**内**、private）

```csharp
// sqlite-net は [Column] 属性で列マッピングするため、Novel を継承して
// 未読数列だけ拡張する。[Table] は付けない（QueryAsync<T> では不要）。
private sealed class NovelWithUnreadRow : Novel
{
    [SQLite.Column("unread_count")]
    public int UnreadCount { get; set; }
}
```

#### 3.1.3 追加するメソッド（`GetAllAsync(string sortKey)` の直後に配置）

```csharp
public async Task<List<NovelWithUnread>> GetAllWithUnreadCountAsync(string sortKey)
{
    await _dbService.EnsureInitializedAsync().ConfigureAwait(false);

    // novels の全列を明示列挙 + 未読数を 1 クエリで取得。
    // LEFT JOIN により episodes が 0 件の小説も unread_count = 0 で返る。
    // `n.*` は使わない（カラム順序依存やスキーマ変更時の破損を避けるため）。
    // 列は Models/Novel.cs の [Column] 宣言と 1 対 1 対応させる。
    const string baseSql =
        "SELECT " +
        "  n.id, " +
        "  n.site_type, " +
        "  n.novel_id, " +
        "  n.title, " +
        "  n.author, " +
        "  n.total_episodes, " +
        "  n.is_completed, " +
        "  n.last_updated_at, " +
        "  n.registered_at, " +
        "  n.has_unconfirmed_update, " +
        "  n.has_check_error, " +
        "  n.is_favorite, " +
        "  n.favorited_at, " +
        "  COALESCE(u.cnt, 0) AS unread_count " +
        "FROM novels n " +
        "LEFT JOIN (" +
        "    SELECT novel_id, COUNT(*) AS cnt " +
        "    FROM episodes " +
        "    WHERE is_read = 0 " +
        "    GROUP BY novel_id" +
        ") u ON u.novel_id = n.id ";

    string orderBy = sortKey switch
    {
        "updated_asc"     => "ORDER BY n.last_updated_at ASC",
        "title_asc"       => "ORDER BY n.title ASC",
        "title_desc"      => "ORDER BY n.title DESC",
        "author_asc"      => "ORDER BY n.author ASC",
        "registered_desc" => "ORDER BY n.registered_at DESC",
        "unread_desc"     => "ORDER BY unread_count DESC, n.last_updated_at DESC",
        "favorite_first"  => "ORDER BY n.is_favorite DESC, n.last_updated_at DESC",
        _                 => "ORDER BY n.last_updated_at DESC", // updated_desc / 未知キー
    };

    var rows = await _db.QueryAsync<NovelWithUnreadRow>(baseSql + orderBy)
        .ConfigureAwait(false);

    var result = new List<NovelWithUnread>(rows.Count);
    foreach (var r in rows)
    {
        // Novel 部分のコピー（派生クラスインスタンスをそのまま外に出さない）
        var novel = new Novel
        {
            Id = r.Id,
            SiteType = r.SiteType,
            NovelId = r.NovelId,
            Title = r.Title,
            Author = r.Author,
            TotalEpisodes = r.TotalEpisodes,
            IsCompleted = r.IsCompleted,
            LastUpdatedAt = r.LastUpdatedAt,
            RegisteredAt = r.RegisteredAt,
            HasUnconfirmedUpdate = r.HasUnconfirmedUpdate,
            HasCheckError = r.HasCheckError,
            IsFavorite = r.IsFavorite,
            FavoritedAt = r.FavoritedAt,
        };
        result.Add(new NovelWithUnread(novel, r.UnreadCount));
    }
    return result;
}
```

> **実装メモ**: `NovelWithUnreadRow` はそのまま `Novel` としてキャストして返すのではなく、値をコピーした新 `Novel` を作って返す。理由は (a) 派生クラスが外部に漏れるのを防ぐ (b) `NovelWithUnread.Novel` の型契約を厳格にする。件数規模（最大数百件想定）からコピーコストは無視できる。

#### 3.1.4 既存メソッドの扱い

- `GetAllAsync()` / `GetAllAsync(string sortKey)`: **温存**（UpdateCheckService, PrefetchService が使用中）
- 他メソッド: 変更なし

---

### 3.2 `NovelListViewModel.cs` の変更

#### 3.2.1 コンストラクタから `EpisodeRepository` を削除

**Before**:
```csharp
private readonly NovelRepository _novelRepo;
private readonly EpisodeRepository _episodeRepo;
private readonly EpisodeCacheRepository _cacheRepo;
private readonly AppSettingsRepository _settingsRepo;
private readonly UpdateCheckService _updateCheckService;

public NovelListViewModel(
    NovelRepository novelRepo,
    EpisodeRepository episodeRepo,
    EpisodeCacheRepository cacheRepo,
    AppSettingsRepository settingsRepo,
    UpdateCheckService updateCheckService)
{
    _novelRepo = novelRepo;
    _episodeRepo = episodeRepo;
    _cacheRepo = cacheRepo;
    _settingsRepo = settingsRepo;
    _updateCheckService = updateCheckService;
}
```

**After**:
```csharp
private readonly NovelRepository _novelRepo;
private readonly EpisodeCacheRepository _cacheRepo;
private readonly AppSettingsRepository _settingsRepo;
private readonly UpdateCheckService _updateCheckService;

public NovelListViewModel(
    NovelRepository novelRepo,
    EpisodeCacheRepository cacheRepo,
    AppSettingsRepository settingsRepo,
    UpdateCheckService updateCheckService)
{
    _novelRepo = novelRepo;
    _cacheRepo = cacheRepo;
    _settingsRepo = settingsRepo;
    _updateCheckService = updateCheckService;
}
```

> DI は `MauiProgram.cs:52` で `AddTransient<NovelListViewModel>()` として自動解決されるため、登録側の変更は不要。

#### 3.2.2 `LoadNovelsAsync` の書き換え

**Before** (`:51-71`):
```csharp
private async Task LoadNovelsAsync()
{
    try
    {
        var novels = await _novelRepo.GetAllAsync(SortKey);
        var cards = new List<NovelCardViewModel>();

        foreach (var novel in novels)
        {
            var unread = await _episodeRepo.CountUnreadByNovelIdAsync(novel.Id);
            cards.Add(NovelCardViewModel.FromModel(novel, unread));
        }

        Novels = new ObservableCollection<NovelCardViewModel>(cards);
        HasCheckError = novels.Any(n => n.HasCheckError == 1);
    }
    catch (Exception ex)
    {
        LogHelper.Error(nameof(NovelListViewModel), $"LoadNovelsAsync failed: {ex.Message}");
    }
}
```

**After**:
```csharp
private async Task LoadNovelsAsync()
{
    try
    {
        var rows = await _novelRepo.GetAllWithUnreadCountAsync(SortKey);
        Novels = new ObservableCollection<NovelCardViewModel>(
            rows.Select(r => NovelCardViewModel.FromModel(r.Novel, r.UnreadCount)));
        HasCheckError = rows.Any(r => r.Novel.HasCheckError == 1);
    }
    catch (Exception ex)
    {
        LogHelper.Error(nameof(NovelListViewModel), $"LoadNovelsAsync failed: {ex.Message}");
    }
}
```

#### 3.2.3 using 文の整理

`LanobeReader.Services.Database` は引き続き必要（`NovelRepository` / `EpisodeCacheRepository` / `AppSettingsRepository`）。**削除する using はなし**。

---

### 3.3 `EpisodeListViewModel.cs` の変更

#### 3.3.1 フィールド追加

`:19-21` の既存フィールド群の直後に追加:

```csharp
private int _novelDbId;
private List<Episode> _allEpisodes = new();
private HashSet<int> _cachedIds = new();
// ↓ 追加
private List<Episode> _filteredCache = new(); // ShowUnreadOnly / ShowFavoritesOnly 適用済みキャッシュ
```

> `_allEpisodes` と `_filteredCache` は Episode への参照を共有するため追加メモリは参照分のみ。

#### 3.3.2 `FilteredEpisodes()` を削除し、`RebuildFilterCache()` に置き換え

**Before** (`:125-131`):
```csharp
private IEnumerable<Episode> FilteredEpisodes()
{
    IEnumerable<Episode> src = _allEpisodes;
    if (ShowUnreadOnly) src = src.Where(e => e.IsRead == 0);
    if (ShowFavoritesOnly) src = src.Where(e => e.IsFavorite == 1);
    return src;
}
```

**After**:
```csharp
private void RebuildFilterCache()
{
    IEnumerable<Episode> src = _allEpisodes;
    if (ShowUnreadOnly) src = src.Where(e => e.IsRead == 0);
    if (ShowFavoritesOnly) src = src.Where(e => e.IsFavorite == 1);
    _filteredCache = src.ToList();
}
```

#### 3.3.3 `ApplyFilterAndShow` の変更

**Before** (`:133-137`):
```csharp
private void ApplyFilterAndShow()
{
    Episodes = new ObservableCollection<EpisodeViewModel>(
        FilteredEpisodes().Select(e => EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id))));
}
```

**After**:
```csharp
private void ApplyFilterAndShow()
{
    Episodes = new ObservableCollection<EpisodeViewModel>(
        _filteredCache.Select(e => EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id))));
}
```

#### 3.3.4 `RecalcPaging` の変更

**Before** (`:139-144`):
```csharp
private void RecalcPaging()
{
    var totalCount = FilteredEpisodes().Count();
    MaxPage = Math.Max(1, (int)Math.Ceiling((double)totalCount / _episodesPerPage));
    if (CurrentPage > MaxPage) CurrentPage = MaxPage;
}
```

**After**:
```csharp
private void RecalcPaging()
{
    var totalCount = _filteredCache.Count;
    MaxPage = Math.Max(1, (int)Math.Ceiling((double)totalCount / _episodesPerPage));
    if (CurrentPage > MaxPage) CurrentPage = MaxPage;
}
```

#### 3.3.5 `LoadPageAsync` の変更

**Before** (`:146-155`):
```csharp
private Task LoadPageAsync()
{
    var list = FilteredEpisodes()
        .Skip((CurrentPage - 1) * _episodesPerPage)
        .Take(_episodesPerPage)
        .Select(e => EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id)))
        .ToList();
    Episodes = new ObservableCollection<EpisodeViewModel>(list);
    return Task.CompletedTask;
}
```

**After**:
```csharp
private Task LoadPageAsync()
{
    var list = _filteredCache
        .Skip((CurrentPage - 1) * _episodesPerPage)
        .Take(_episodesPerPage)
        .Select(e => EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id)))
        .ToList();
    Episodes = new ObservableCollection<EpisodeViewModel>(list);
    return Task.CompletedTask;
}
```

#### 3.3.6 `InitializeAsync` への `RebuildFilterCache()` 差し込み

**Before** (`:83-123` の該当部分抜粋):
```csharp
_allEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
_cachedIds = await _cacheRepo.GetCachedEpisodeIdsAsync(_novelDbId);

var hasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
```

**After**:
```csharp
_allEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
_cachedIds = await _cacheRepo.GetCachedEpisodeIdsAsync(_novelDbId);
RebuildFilterCache(); // ← 追加（_allEpisodes 代入直後）

var hasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
```

#### 3.3.7 `ReloadListAsync` への `RebuildFilterCache()` 差し込み

**Before** (`:160-172`):
```csharp
private async Task ReloadListAsync()
{
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
```

**After**:
```csharp
private async Task ReloadListAsync()
{
    RebuildFilterCache(); // ← ShowUnreadOnly / ShowFavoritesOnly 変更時の再計算はここだけ
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
```

#### 3.3.8 `ToggleEpisodeFavoriteAsync` の同期

お気に入りを外した際、`ShowFavoritesOnly == true` だとキャッシュから該当要素を除く必要がある。

**Before** (`:196-205`):
```csharp
[RelayCommand]
private async Task ToggleEpisodeFavoriteAsync(EpisodeViewModel ep)
{
    var newValue = !ep.IsFavorite;
    await _episodeRepo.SetFavoriteAsync(ep.Id, newValue);
    ep.IsFavorite = newValue;

    var source = _allEpisodes.FirstOrDefault(e => e.Id == ep.Id);
    if (source is not null) source.IsFavorite = newValue ? 1 : 0;
}
```

**After**:
```csharp
[RelayCommand]
private async Task ToggleEpisodeFavoriteAsync(EpisodeViewModel ep)
{
    var newValue = !ep.IsFavorite;
    await _episodeRepo.SetFavoriteAsync(ep.Id, newValue);
    ep.IsFavorite = newValue;

    var source = _allEpisodes.FirstOrDefault(e => e.Id == ep.Id);
    if (source is not null) source.IsFavorite = newValue ? 1 : 0;

    // ShowFavoritesOnly 時は外した要素を表示から除く必要があるため再構築。
    // それ以外のフィルタ状態ではキャッシュ内容は不変なので何もしない。
    if (ShowFavoritesOnly)
    {
        RebuildFilterCache();
        if (HasChapters)
        {
            ApplyFilterAndShow();
        }
        else
        {
            RecalcPaging();
            await LoadPageAsync();
        }
    }
}
```

> **注**: 既読トグル系のコマンドは現状存在しない（Reader 側で `is_read` を更新）。`ShowUnreadOnly` のキャッシュ同期は画面遷移後の `InitializeAsync` 再実行で担保される（`EpisodeListPage` が `AddTransient` で都度生成される MAUI の挙動に依存）。別セッションで既読トグル機能を追加する場合は同様のキャッシュ同期を組み込むこと。

---

## 4. 完成形コードの配置マップ

| 項目 | 配置場所 |
|---|---|
| `NovelWithUnread` record | `NovelRepository.cs`、名前空間直下、クラス外 |
| `NovelWithUnreadRow` 内部クラス | `NovelRepository.cs`、クラス内 private |
| `GetAllWithUnreadCountAsync` | `NovelRepository.cs`、`GetAllAsync(string)` の直後 |
| `RebuildFilterCache` | `EpisodeListViewModel.cs`、旧 `FilteredEpisodes()` と同じ位置 |
| `_filteredCache` フィールド | `EpisodeListViewModel.cs`、`_cachedIds` の直後 |

---

## 5. ビルド・動作確認手順

### 5.1 ビルド
```bash
dotnet build /c/Work/Github/TBird.Library/_Apps/App.sln --no-restore
```
- 警告ゼロであること
- `EpisodeRepository` を削除した影響で `CS0246` 等が出ないこと（`using` は残したまま OK）

### 5.2 手動チェックリスト

#### 小説一覧（H1 検証）
- [ ] 8 種のソートキーすべてで一覧が表示される
- [ ] 既存の `GetAllAsync(sortKey)` と**同一の並び順**になる（切替前のブランチでスクリーンショット取得 → 比較）
- [ ] 未読話数バッジが従来と一致（3〜5 件の小説で目視比較）
- [ ] `unread_desc` でも一致（既存はサブクエリ版 `(SELECT COUNT(*) FROM episodes ...)` だったが LEFT JOIN + GROUP BY でも結果は同じ）
- [ ] 登録 0 話の小説（新規追加直後など）が `unread_count = 0` で表示される（LEFT JOIN + COALESCE の確認）
- [ ] リフレッシュ（更新チェック）→ リスト再読込が正常
- [ ] お気に入りトグル後、`favorite_first` ソート時にリストが更新される

#### エピソード一覧（H5 検証）
- [ ] 章ありタイトルで全話表示
- [ ] 章なしタイトルで前/次ページボタンが正常に動作
- [ ] ページ送りで `MaxPage` が変わらないこと（フィルタ未変更時にキャッシュが維持されることの裏付け）
- [ ] 「未読のみ」ON/OFF で表示が切り替わる
- [ ] 「お気に入りのみ」ON/OFF で表示が切り替わる
- [ ] 「お気に入りのみ」ON のときに表示中エピソードのお気に入りを外す → 即座にリストから消える
- [ ] 長編（500 話以上）でページ送りの体感が改善している

### 5.3 パフォーマンス実測（任意）

Android 実機で Stopwatch ログを仕込んで before/after を比較すると効果が定量化できる（ログは PR に含めない）。

---

## 6. リスク一覧と対策

| # | リスク | 発生箇所 | 対策 |
|---|---|---|---|
| R1 | sqlite-net のカラムマッピングずれで `UnreadCount` が常に 0 / 列追加時にクエリが壊れる | `GetAllWithUnreadCountAsync` | SELECT で `n.*` を**使わず**全列を明示列挙する（§3.1.3 参照）。`[Column("unread_count")]` と SQL の `AS unread_count` を一致させる。将来 `novels` に列が追加されたらこの SQL にも追記する |
| R2 | `unread_desc` の順序が既存と微妙にズレる | 同 | 既存はサブクエリ版で `ORDER BY (SELECT COUNT(*) ...) DESC, n.last_updated_at DESC`。新版も `unread_count DESC, n.last_updated_at DESC` で揃える。Null タイブレークは `last_updated_at` が `NULL` の行がある場合 SQLite 既定で NULL 最小扱い、既存と同じ挙動 |
| R3 | タイトル/著者ソートで大小文字順序が変わる | 同 | 既存の LINQ 版も `COLLATE` 指定なし → 新版も付けない |
| R4 | `_filteredCache` が古い状態のまま UI に反映 | `EpisodeListViewModel` | 再構築呼び出しを `InitializeAsync` / `ReloadListAsync` / `ToggleEpisodeFavoriteAsync (ShowFavoritesOnly時)` の 3 箇所に限定し列挙済み |
| R5 | `NovelListViewModel` のコンストラクタ引数削除で他所の手動 new がコンパイルエラー | - | Grep で `new NovelListViewModel(` を検索し、ヒットがないことを確認（DI 経由のみ想定） |
| R6 | `EpisodeRepository` の `CountUnreadByNovelIdAsync` が dead code になる | - | 今回は温存。削除は Repository 基底クラス整理（[audit_2026-04-08_apps-refactor.md](audit_2026-04-08_apps-refactor.md) の M5）に合わせて後続 PR で検討 |

### 事前に走らせる Grep（実装セッション開始時の最終確認）

```text
pattern: new NovelListViewModel\(
path:    c:\Work\Github\TBird.Library\_Apps
期待:    ヒットなし（DI 解決のみ）

pattern: CountUnreadByNovelIdAsync
path:    c:\Work\Github\TBird.Library\_Apps
期待:    EpisodeRepository.cs:43 の宣言のみ（呼び出し元消滅）
```

---

## 7. コミット分割方針

レビュー容易性のため 3 コミットに分割する。

1. **`refactor(NovelRepository): add GetAllWithUnreadCountAsync for H1`**
   - `NovelRepository.cs` のみ変更
   - この時点でビルドは通る（新メソッド追加のみ、既存メソッドは未削除）

2. **`perf(NovelListViewModel): use aggregated query to remove N+1 (H1)`**
   - `NovelListViewModel.cs` の `LoadNovelsAsync` 差し替え
   - `EpisodeRepository` 依存削除（コンストラクタ、フィールド）
   - 手動チェック: 一覧表示・ソート

3. **`perf(EpisodeListViewModel): cache filtered episodes (H5)`**
   - `EpisodeListViewModel.cs` のフィルタキャッシュ導入
   - 手動チェック: ページング・フィルタトグル

---

## 8. スコープ外（やらないこと）

- H2 ReaderPage コードビハインド除去
- H3 SettingsPage 状態管理
- H4 Reader の CSS 変数化
- H6 SearchViewModel 並列化
- H7 BackgroundJobQueue 競合安全化
- Medium / Low 以下のすべて
- `EpisodeRepository.CountUnreadByNovelIdAsync` の削除
- `NovelRepository.GetAllAsync` の既存オーバーロード削除
- `MauiProgram.cs` の DI 設定変更
- テストコードの追加（プロジェクトにテスト基盤なし）

---

## 9. 完了条件（Definition of Done）

1. 変更は 3 ファイルのみ（`git diff --stat` で確認）
2. `dotnet build` が警告ゼロ
3. §5.2 手動チェックリスト全通過
4. 3 コミット構成
5. PR 本文に本ファイル（`_Apps/Features/plan_2026-04-08_pr2-data-performance.md`）へのリンクと「H1/H5 対応」の明記
6. PR 対象ブランチは `app-novelviewer`（`_Apps` 配下のファイルは `app-novelviewer` にしか存在しないため、CLAUDE.md のブランチ選定ルールに従い base = `app-novelviewer`）
