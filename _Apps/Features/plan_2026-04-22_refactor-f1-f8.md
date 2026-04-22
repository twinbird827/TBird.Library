# REFACTOR修正プラン (F1-F8)

コードレビュー ([todo_2026-04-14_code-review.md](todo_2026-04-14_code-review.md)) で抽出した REFACTOR 項目 8 件を整理する。
妥当性検証の結果、実装対象は **F1 / F3 / F4 / F5 / F8** の 5 件。F2 は実施済、F6 / F7 は skip。

ブランチ: `app-novelviewer` から feature ブランチを作成 → `app-novelviewer` へ PR。
3 PR に分割する（理由: F4 は DB スキーマ変更でレビュー観点が独立、F5 は定数抽出で機械的リネーム、F1+F3+F8 は独立した局所修正でまとめ可能）。

---

## PR 分割

| PR | ブランチ名 | 含む項目 | サイズ目安 |
|---|---|---|---|
| PR-1 | `feature/refactor-f1-f3-f8` | F1, F3, F8 | 3ファイル, ~150行 delta |
| PR-2 | `feature/refactor-f5-reader-constants` | F5 | 新規1+既存4ファイル, ~30行 delta |
| PR-3 | `feature/refactor-f4-schema-version` | F4 | 1ファイル (DatabaseService), ~120行 delta |

各 PR は独立してマージ可能。推奨順序: PR-1 → PR-2 → PR-3（PR-3 は DB 挙動確認のため最後）。

---

## PR-1: F1 + F3 + F8（即時効果の高い局所改善）

### F1 — SearchViewModel の 3 メソッド重複をテンプレート化

**問題:** [SearchViewModel.cs:136-277](../ViewModels/SearchViewModel.cs#L136-L277) の `SearchAsync` / `FetchRankingAsync` / `FetchGenreAsync` は以下の同一骨格を持つ:

```
IsLoading=true; HasError=false; ErrorMessage="";
try {
    CancellationTokenSource cts (30秒);
    var narouTask = SearchNarou ? RunSiteSearchAsync(() => narou.XxxAsync(...), "なろう") : 空結果;
    var kakuyomuTask = SearchKakuyomu ? RunSiteSearchAsync(() => kakuyomu.XxxAsync(...), "カクヨム") : 空結果;
    await Task.WhenAll(...);
    エラー集約 → HasError / ErrorMessage 設定;
    await ShowResultsAsync(全ヒット);
} catch (Exception ex) {
    LogHelper.Error(...); HasError=true; ErrorMessage="通信エラー...";
} finally { IsLoading=false; }
```

**修正:** 共通ヘルパー `ExecuteSiteQueryAsync` を追加し、3 メソッドから呼び出す。

```csharp
// 追加位置: 行114 RunSiteSearchAsync の直後
private async Task ExecuteSiteQueryAsync(
    string operationName,
    Func<CancellationToken, Task<List<SearchResult>>>? narouFetch,
    Func<CancellationToken, Task<List<SearchResult>>>? kakuyomuFetch)
{
    IsLoading = true;
    HasError = false;
    ErrorMessage = string.Empty;
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        var narouTask = narouFetch is not null
            ? RunSiteSearchAsync(() => narouFetch(ct), "なろう")
            : Task.FromResult<(List<SearchResult> hits, string? error)>(([], null));
        var kakuyomuTask = kakuyomuFetch is not null
            ? RunSiteSearchAsync(() => kakuyomuFetch(ct), "カクヨム")
            : Task.FromResult<(List<SearchResult> hits, string? error)>(([], null));

        var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

        var allHits = siteResults.SelectMany(r => r.hits).ToList();
        var errors = siteResults.Select(r => r.error).Where(e => e is not null).ToList();
        if (errors.Count > 0)
        {
            HasError = true;
            ErrorMessage = string.Join("\n", errors);
        }

        await ShowResultsAsync(allHits);
    }
    catch (Exception ex)
    {
        LogHelper.Error(nameof(SearchViewModel), $"{operationName} failed: {ex.Message}");
        HasError = true;
        ErrorMessage = "通信エラーが発生しました";
    }
    finally
    {
        IsLoading = false;
    }
}
```

**呼出側の置換 (3箇所):**

```csharp
[RelayCommand(CanExecute = nameof(CanSearch))]
private Task SearchAsync()
{
    var target = "Both";
    return ExecuteSiteQueryAsync(
        "Search",
        SearchNarou    ? ct => _narou.SearchAsync(SearchKeyword, target, ct)    : null,
        SearchKakuyomu ? ct => _kakuyomu.SearchAsync(SearchKeyword, target, ct) : null);
}

[RelayCommand]
private Task FetchRankingAsync()
{
    var period = (RankingPeriod)Math.Clamp(RankingPeriodIndex, 0, 3);
    return ExecuteSiteQueryAsync(
        "Ranking fetch",
        SearchNarou ? ct =>
        {
            int? bg = null;
            if (SelectedNarouBigGenre is not null
                && int.TryParse(SelectedNarouBigGenre.Id, out var bgv)) bg = bgv;
            return _narou.FetchRankingAsync(period, bg, 30, ct);
        } : null,
        SearchKakuyomu ? ct =>
        {
            var slug = period switch
            {
                RankingPeriod.Daily   => "daily",
                RankingPeriod.Weekly  => "weekly",
                RankingPeriod.Monthly => "monthly",
                _                     => "weekly",
            };
            return _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre?.Id ?? "all", slug, ct);
        } : null);
}

[RelayCommand]
private Task FetchGenreAsync()
{
    int? narouBg = (SearchNarou
        && SelectedNarouBigGenre is not null
        && int.TryParse(SelectedNarouBigGenre.Id, out var bg)) ? bg : null;

    return ExecuteSiteQueryAsync(
        "Genre fetch",
        narouBg is int bgv ? ct => _narou.FetchByGenreAsync(bgv, "weeklypoint", 30, ct) : null,
        (SearchKakuyomu && SelectedKakuyomuGenre is not null)
            ? ct => _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre.Id, "weekly", ct)
            : null);
}
```

**注意点:**
- `FetchGenreAsync` の narou 判定は `int?` 変数 1 つで `TryParse` 結果を保持する（二重パース回避）。
- `SearchAsync` の `CanExecute = nameof(CanSearch)` 属性は既存のまま維持（`ExecuteSiteQueryAsync` は `async Task` を返すので問題なし）。
- `ct => ...` 三項演算子は C# 10+ の target-typed conditional で `Func<CancellationToken, Task<List<SearchResult>>>?` に推論されるはずだが、コンパイルエラーが出た場合は `(Func<CancellationToken, Task<List<SearchResult>>>?)(ct => ...)` にキャストして解消する。
- 動作変更なし。ログメッセージの prefix のみ変わる (`"Search failed"` → `"Search failed"`, `"Ranking fetch failed"` → `"Ranking fetch failed"` で実質同じ)。

---

### F3 — ReaderHtmlBuilder を raw string literal 化

**問題:** [ReaderHtmlBuilder.cs:13-75](../Helpers/ReaderHtmlBuilder.cs#L13-L75) の `sb.Append()` 連鎖が CSS/JS を読みにくくしている。

**修正:** C# 11 raw string literal (`"""`) + interpolation (`$$`) で HEAD/CSS/JS を分離。

`.csproj` の `LangVersion` 確認: プロジェクトルートの `.csproj` を `Read` で確認し、`<LangVersion>` が 11 以上または未指定（.NET 9 なので既定で C# 13）であること。
（CLAUDE.md 記載: C# 10 有効。.NET 9 ターゲットなので C# 13 が既定で有効。raw string literal は C# 11+ で使用可能。）

**新実装:**

```csharp
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace LanobeReader.Helpers;

/// <summary>
/// 縦書き WebView 用の HTML を生成するビルダー。
/// スタイル値は CSS カスタムプロパティ（--reader-fs 等）に切り出してあり、
/// 将来 JS から :root の値を書き換えることでライブ反映が可能な構造。
/// </summary>
public static class ReaderHtmlBuilder
{
    public static string Build(string content, ReaderCssState state)
    {
        var inv = CultureInfo.InvariantCulture;
        var (bgHex, fgHex) = ReaderStyleResolver.ResolveThemeColors(state.BackgroundThemeIndex);
        var lh = ReaderStyleResolver.ResolveLineHeight(state.LineSpacingIndex);
        var fs = state.FontSizePx.ToString("0.##", inv);
        var lhs = lh.ToString("0.##", inv);

        var body = BuildBody(content);

        // NOTE: 下部 <script> の read-end / next-episode / prev-episode URI 発火は
        // ReaderPage.OnWebViewNavigating と連動しているため変更禁止。
        return $$"""
            <!doctype html><html lang="ja"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <style>
            :root{--reader-fs:{{fs}}px;--reader-lh:{{lhs}};--reader-bg:{{bgHex}};--reader-fg:{{fgHex}};}
            html,body{margin:0;padding:0;height:100%;overflow-y:hidden;}
            body{
              background:var(--reader-bg);color:var(--reader-fg);
              font-family:serif;
              writing-mode:vertical-rl;-webkit-writing-mode:vertical-rl;
              font-size:var(--reader-fs);line-height:var(--reader-lh);
              padding:16px;box-sizing:border-box;
              overflow-x:auto;overflow-y:hidden;touch-action:pan-x;overscroll-behavior-y:none;
              -webkit-tap-highlight-color:transparent;
            }
            p{margin:0 0 1em 0;text-indent:1em;}
            </style></head><body>
            {{body}}
            <script>
            (function(){var fired=false;function check(){if(fired)return;
              var el=document.scrollingElement||document.documentElement;
              var maxNeg=-(el.scrollWidth-el.clientWidth);
              if(el.scrollLeft<=maxNeg+10){fired=true;location.href='lanobe://read-end';}}
              window.addEventListener('scroll',check,{passive:true});setTimeout(check,100);})();
            (function(){var sx,sy,st;
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
                  else location.href='lanobe://prev-episode';}
              },{passive:true});})();
            </script>
            </body></html>
            """;
    }

    private static string BuildBody(string content)
    {
        var sb = new StringBuilder(content.Length + 256);
        foreach (var line in content.ReplaceLineEndings("\n").Split('\n'))
        {
            sb.Append("<p>");
            sb.Append(System.Net.WebUtility.HtmlEncode(line));
            sb.Append("</p>");
        }
        return sb.ToString();
    }
}
```

**ポイント:**
- `$$"""..."""` で `{` `}` (CSS/JS) を無エスケープで記述可能。`{{var}}` で補間。
- `ReplaceLineEndings` は R8 修正で既に入っている想定（`plan_2026-04-16_risk-r1-r14.md` 9節）。
- `<body>` 部の `<p>...` 生成は StringBuilder のまま維持（件数可変のため）。
- JS 連動箇所 (`lanobe://` URI) は **コメント明示** して将来の修正時の事故防止。

**検証:**
- 既存の [ReaderPage.xaml.cs:37-58](../Views/ReaderPage.xaml.cs#L37-L58) の `OnWebViewNavigating` が引き続き発火すること。
- 縦書きモードでエピソード末スクロール → 既読化、上下スワイプでエピソード遷移。

---

### F8 — HasValueConverter の IEnumerable Dispose

**問題:** [HasValueConverter.cs:16](../Converters/HasValueConverter.cs#L16) の `e.GetEnumerator().MoveNext()` が Enumerator を Dispose しない。非ジェネリック `IEnumerable.GetEnumerator()` は `IDisposable` を実装しないが、返す具象型（`List<T>.Enumerator` 等）は実装することが多い。

**修正:** プライベートヘルパーを追加し、try/finally で保護。

```csharp
using System.Collections;
using System.Globalization;

namespace LanobeReader.Converters;

public class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            ICollection c => c.Count > 0,
            IEnumerable e => HasAny(e),
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            float f => f != 0,
            decimal m => m != 0,
            _ => true,
        };
    }

    private static bool HasAny(IEnumerable source)
    {
        var enumerator = source.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

**検証:** 使用箇所は [EpisodeListPage.xaml:51](../Views/EpisodeListPage.xaml#L51) の `ChapterName`(string) のみ。string は上の `string s` 分岐に入るため挙動変化なし。ビルドが通れば OK。

---

### PR-1 検証手順

1. `dotnet build _Apps/App.sln --no-restore` が通る。
2. 検索画面: キーワード検索 / ランキング表示 / ジャンルブラウズ の 3 機能で結果取得・エラー表示が正常。
3. 片方のサイト OFF 時に該当サイト分が空リストで返ること。
4. リーダー画面: 縦書き表示で CSS テーマ（3色）/ 行間（3段階）/ フォントサイズが反映。
5. 縦書きモードで読了検知・前後エピソード遷移が動作。

---

## PR-2: F5 — ReaderCssState のマジックナンバーを定数クラスに

**問題:** [ReaderCssState.cs:10-11](../Helpers/ReaderCssState.cs#L10-L11) の `BackgroundThemeIndex` / `LineSpacingIndex` および
[ReaderStyleResolver.cs:12-13, 22-27](../Helpers/ReaderStyleResolver.cs#L12-L27) の switch 式で `0 / 1 / 2` のマジックナンバーがハードコード。

**方針:** enum 化は XAML DataTrigger の `Value="0"` と噛み合わず `{x:Static}` が必要となり冗長化するため、**static 定数クラス**を追加して C# 側のみ自己文書化する。XAML は int リテラルのまま。

### 追加ファイル: `_Apps/Helpers/ReaderThemeIndex.cs`

```csharp
namespace LanobeReader.Helpers;

/// <summary>
/// Reader 画面の背景テーマ設定値。AppSetting "background_theme" に int で保存される。
/// XAML の DataTrigger は int リテラル ("0" / "1" / "2") のまま使用するため、
/// ここは C# 側の自己文書化用途。値を変える場合は ReaderPage.xaml の DataTrigger Value も要同期更新。
/// </summary>
public static class BackgroundTheme
{
    public const int Light = 0;
    public const int Dark = 1;
    public const int Sepia = 2;
}

/// <summary>
/// Reader 画面の行間設定値。AppSetting "line_spacing" に int で保存される。
/// </summary>
public static class LineSpacing
{
    public const int Compact = 0;   // CSS line-height: 1.4
    public const int Normal = 1;    // CSS line-height: 1.7  (default)
    public const int Relaxed = 2;   // CSS line-height: 2.1
}
```

### 変更箇所

#### [ReaderStyleResolver.cs:12-13](../Helpers/ReaderStyleResolver.cs#L12-L13)

```csharp
// 変更前
var bgKey = themeIndex switch { 1 => "ThemeDarkBg", 2 => "ThemeSepiaBg", _ => "ThemeWhiteBg" };
var fgKey = themeIndex switch { 1 => "ThemeDarkText", 2 => "ThemeSepiaText", _ => "ThemeWhiteText" };

// 変更後
var bgKey = themeIndex switch
{
    BackgroundTheme.Dark  => "ThemeDarkBg",
    BackgroundTheme.Sepia => "ThemeSepiaBg",
    _                     => "ThemeWhiteBg",
};
var fgKey = themeIndex switch
{
    BackgroundTheme.Dark  => "ThemeDarkText",
    BackgroundTheme.Sepia => "ThemeSepiaText",
    _                     => "ThemeWhiteText",
};
```

#### [ReaderStyleResolver.cs:22-27](../Helpers/ReaderStyleResolver.cs#L22-L27)

```csharp
// 変更前
public static double ResolveLineHeight(int lineSpacingIndex) => lineSpacingIndex switch
{
    0 => 1.4,
    2 => 2.1,
    _ => 1.7,
};

// 変更後
public static double ResolveLineHeight(int lineSpacingIndex) => lineSpacingIndex switch
{
    LineSpacing.Compact => 1.4,
    LineSpacing.Relaxed => 2.1,
    _                   => 1.7,
};
```

#### XAML はそのまま

[ReaderPage.xaml:12-20, 51-68](../Views/ReaderPage.xaml#L12-L68) の `Value="0"` / `"1"` / `"2"` は変更しない。
理由: `{x:Static local:BackgroundTheme.Dark}` への書き換えには `xmlns:local` 追加が必要で、DataTrigger Value が `x:Static` を受け付けない場合もある（MAUI の既知制約）。
代わりに XAML にコメントを付ける:

```xml
<ContentPage.Triggers>
    <!-- BackgroundThemeIndex: 0=Light / 1=Dark / 2=Sepia (see ReaderThemeIndex.cs) -->
    <DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}" Value="0">
        <Setter Property="BackgroundColor" Value="{StaticResource ThemeWhiteBg}" />
    </DataTrigger>
    ...
</ContentPage.Triggers>
```

同様に Label.Triggers の直前にも:

```xml
<Label.Triggers>
    <!-- BackgroundThemeIndex / LineSpacingIndex (see ReaderThemeIndex.cs) -->
    <DataTrigger TargetType="Label" Binding="{Binding BackgroundThemeIndex}" Value="0">
    ...
```

### SettingsKeys.cs への追記（任意）

[SettingsKeys.cs:20-21](../Helpers/SettingsKeys.cs#L20-L21) の `DEFAULT_BACKGROUND_THEME` / `DEFAULT_LINE_SPACING` を新定数で表現し直す:

```csharp
// 変更前
public const int DEFAULT_BACKGROUND_THEME = 0;
public const int DEFAULT_LINE_SPACING = 1;

// 変更後
public const int DEFAULT_BACKGROUND_THEME = BackgroundTheme.Light;
public const int DEFAULT_LINE_SPACING = LineSpacing.Normal;
```

**注意:** `const int` のイニシャライザは別の `const int` を参照可能。`SettingsKeys.cs` は `namespace LanobeReader.Helpers;` と同一なので `using` 不要。

### PR-2 検証手順

1. `dotnet build _Apps/App.sln --no-restore` が通る。
2. 設定画面で背景テーマ 3 種 / 行間 3 段階を切り替え、リーダー画面で反映。
3. アプリ再起動後も設定値が保持される（DB の int 値保存挙動は不変）。

---

## PR-3: F4 — DB全体のUNIQUE制約見直し + schema_version 機構

### 現状の UNIQUE 制約棚卸し

| テーブル | PK | 既存の UNIQUE 制約 | 業務一意性 | ギャップ |
|---|---|---|---|---|
| `novels` | id (auto) | `idx_novels_site_novel` UNIQUE (site_type, novel_id) ([DatabaseService.cs:53-55](../Services/Database/DatabaseService.cs#L53-L55)) | (site_type, novel_id) | **なし** |
| `episodes` | id (auto) | **なし** (非UNIQUE index `idx_episodes_novel_episode` ([DatabaseService.cs:48-50](../Services/Database/DatabaseService.cs#L48-L50))) | (novel_id, episode_no) | **UNIQUE化が必要** |
| `episode_cache` | id (auto) | `[Unique]` on episode_id ([EpisodeCache.cs:13](../Models/EpisodeCache.cs#L13)) | episode_id | **なし** |
| `app_settings` | key (string PK) | PK = UNIQUE ([AppSetting.cs:8-10](../Models/AppSetting.cs#L8-L10)) | key | **なし** |

**結論:** 業務ロジック上の制約漏れは **`episodes(novel_id, episode_no)` の 1 件のみ**。
ただし将来のスキーマ変更に備え、**`schema_version` マイグレーション機構** を同時に導入する。

### 方針

- **schema_version** を `app_settings` テーブルの 1 キーとして管理（初期値なしなら v1 扱い）。
- 今回のリリースで **v2** に上げる。v2 への migration = 「`idx_episodes_novel_episode` を UNIQUE 化」。
- migration 中に UNIQUE 違反を起こす既存データ（重複 episodes）は **削除**してから UNIQUE 化。user 承認済 (「必要なら全初期化もかまわない」)。
- schema_version 読み書きは `_connection` 経由の raw SQL で行う。`AppSettingsRepository` は `DatabaseService` に依存（循環防止）。
- 失敗時は例外を飲んでログ出力のみ。次回起動で再試行される。

### 設計: DatabaseService.cs の変更

#### 変更 1: CURRENT_SCHEMA_VERSION 定数追加

クラス先頭に追加:

```csharp
private const int CURRENT_SCHEMA_VERSION = 2;
```

#### 変更 2: InitializeInternalAsync の書き換え

既存 [DatabaseService.cs:34-58](../Services/Database/DatabaseService.cs#L34-L58) を置換:

```csharp
private async Task InitializeInternalAsync()
{
    // 1. CreateTable は冪等なので先に走らせる（v0 の新規インストール時の初期化も兼ねる）
    await _connection.CreateTableAsync<Novel>().ConfigureAwait(false);
    await _connection.CreateTableAsync<Episode>().ConfigureAwait(false);
    await _connection.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
    await _connection.CreateTableAsync<AppSetting>().ConfigureAwait(false);

    // 2. 既存カラム追加 (新規カラムの後方互換)
    await EnsureColumnAsync("novels", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
    await EnsureColumnAsync("novels", "favorited_at", "TEXT NULL").ConfigureAwait(false);
    await EnsureColumnAsync("episodes", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
    await EnsureColumnAsync("episodes", "favorited_at", "TEXT NULL").ConfigureAwait(false);

    // 3. novels の UNIQUE 制約 (v1 時点で既に整備済みなので再適用するだけ)
    await _connection.ExecuteAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS idx_novels_site_novel ON novels (site_type, novel_id)"
    ).ConfigureAwait(false);

    // 4. 既定設定のシード
    await SeedSettingsAsync().ConfigureAwait(false);

    // 5. schema_version を読み、必要なマイグレーションを順番に適用
    var currentVersion = await GetSchemaVersionAsync().ConfigureAwait(false);
    if (currentVersion < CURRENT_SCHEMA_VERSION)
    {
        LogHelper.Info(nameof(DatabaseService),
            $"Schema migration: v{currentVersion} → v{CURRENT_SCHEMA_VERSION}");
        await MigrateAsync(currentVersion).ConfigureAwait(false);
        await SetSchemaVersionAsync(CURRENT_SCHEMA_VERSION).ConfigureAwait(false);
    }
}
```

#### 変更 3: schema_version 読み書きメソッド追加

`SeedSettingsAsync` の後に追加:

```csharp
private async Task<int> GetSchemaVersionAsync()
{
    try
    {
        var row = await _connection.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
        if (row is null) return 1; // 未設定は v1 扱い（既存リリースは v1 で動作してきた）
        return int.TryParse(row.Value, out var v) ? v : 1;
    }
    catch
    {
        return 1;
    }
}

private async Task SetSchemaVersionAsync(int version)
{
    var existing = await _connection.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
    if (existing is null)
    {
        await _connection.InsertAsync(
            new AppSetting { Key = "schema_version", Value = version.ToString() }
        ).ConfigureAwait(false);
    }
    else
    {
        existing.Value = version.ToString();
        await _connection.UpdateAsync(existing).ConfigureAwait(false);
    }
}
```

#### 変更 4: MigrateAsync メソッド追加

```csharp
/// <summary>
/// スキーマ version を fromVersion から CURRENT_SCHEMA_VERSION まで順次引き上げる。
/// 新しい migration を追加する場合は、対応する case を足し、CURRENT_SCHEMA_VERSION を +1 すること。
/// </summary>
private async Task MigrateAsync(int fromVersion)
{
    if (fromVersion < 2)
    {
        await MigrateToV2Async().ConfigureAwait(false);
    }
    // 今後のバージョンは↓に追加
    // if (fromVersion < 3) await MigrateToV3Async().ConfigureAwait(false);
}

/// <summary>
/// v1 → v2: episodes(novel_id, episode_no) を UNIQUE 化。
/// 既存の非UNIQUEインデックス idx_episodes_novel_episode を DROP してから
/// 重複レコードを除去し、UNIQUE インデックスを貼り直す。
/// 重複の episode_cache も連鎖削除。
/// </summary>
private async Task MigrateToV2Async()
{
    try
    {
        await _connection.ExecuteAsync("DROP INDEX IF EXISTS idx_episodes_novel_episode")
            .ConfigureAwait(false);

        var dupCount = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM (" +
            "  SELECT novel_id, episode_no FROM episodes " +
            "  GROUP BY novel_id, episode_no HAVING COUNT(*) > 1" +
            ")"
        ).ConfigureAwait(false);

        if (dupCount > 0)
        {
            LogHelper.Warn(nameof(DatabaseService),
                $"[MigrateToV2] Found {dupCount} duplicate (novel_id, episode_no) groups. Deduping.");

            // 孤立 cache 先 → episodes 後（FK なしのため手動順序管理）
            await _connection.ExecuteAsync(
                "DELETE FROM episode_cache WHERE episode_id IN (" +
                "  SELECT id FROM episodes WHERE id NOT IN (" +
                "    SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                "  )" +
                ")"
            ).ConfigureAwait(false);

            var deleted = await _connection.ExecuteAsync(
                "DELETE FROM episodes WHERE id NOT IN (" +
                "  SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                ")"
            ).ConfigureAwait(false);
            LogHelper.Info(nameof(DatabaseService),
                $"[MigrateToV2] Deleted {deleted} duplicate episode rows.");
        }

        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_novel_episode " +
            "ON episodes (novel_id, episode_no)"
        ).ConfigureAwait(false);

        LogHelper.Info(nameof(DatabaseService), "[MigrateToV2] Done.");
    }
    catch (Exception ex)
    {
        LogHelper.Warn(nameof(DatabaseService), $"[MigrateToV2] Failed: {ex.Message}");
        throw; // 上位 (InitializeInternalAsync) で SetSchemaVersion を skip させるため再送出
    }
}
```

#### 変更 5: InitializeInternalAsync で migration 失敗時に version を上げない

上記「変更 2」の step 5 は、`MigrateAsync` が例外を投げた場合は `SetSchemaVersionAsync` を呼ばない構造になっている。
try/catch で補足する形に修正:

```csharp
var currentVersion = await GetSchemaVersionAsync().ConfigureAwait(false);
if (currentVersion < CURRENT_SCHEMA_VERSION)
{
    LogHelper.Info(nameof(DatabaseService),
        $"Schema migration: v{currentVersion} → v{CURRENT_SCHEMA_VERSION}");
    try
    {
        await MigrateAsync(currentVersion).ConfigureAwait(false);
        await SetSchemaVersionAsync(CURRENT_SCHEMA_VERSION).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        // migration 失敗時は version を上げずに継続。
        // 次回起動で再試行される。
        LogHelper.Warn(nameof(DatabaseService),
            $"Schema migration failed, will retry next launch: {ex.Message}");
    }
}
```

### `_initLock` 補足

[DatabaseService.cs:24-30](../Services/Database/DatabaseService.cs#L24-L30) の `EnsureInitializedAsync` は `_initLock` + `_initTask` で 1 回だけ走るため、並行で呼ばれても migration は 1 度しか動かない。

### Migration 冪等性

`MigrateToV2Async` の各ステップは全て冪等に設計されており、**部分失敗 → 次回起動で再試行** のパスで安全に復帰可能:

| ステップ | 冪等保証 |
|---|---|
| `DROP INDEX IF EXISTS idx_episodes_novel_episode` | `IF EXISTS` により無い場合は no-op |
| 重複件数 COUNT | 副作用なし。重複 0 なら dedup block はスキップ |
| `DELETE FROM episode_cache WHERE ...` / `DELETE FROM episodes WHERE ...` | 再実行時は既に dedup 済なので削除 0 件 |
| `CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_novel_episode` | 既存の場合 no-op |

**失敗シナリオの回復:**
- **DROP 後に DELETE 失敗**: 非UNIQUE index が消えた状態で重複データ残存。`schema_version` は v1 のまま。次回起動で DROP (no-op) → DELETE (再試行) → CREATE UNIQUE で回復。
- **DELETE 後に CREATE 失敗**: 重複は除去済、index 無し状態。`schema_version` は v1 のまま。次回起動で DROP (no-op) → DELETE (no-op) → CREATE UNIQUE (成功) で回復。
- **CREATE 後に `SetSchemaVersion` 失敗**: UNIQUE index 済、schema_version v1 のまま。次回起動で DROP → DELETE (no-op) → CREATE IF NOT EXISTS (no-op) → SetSchemaVersion (再試行)。

v2 適用済 DB で再度 `MigrateToV2Async` が走っても完全 no-op で完了する（重複 0、UNIQUE index 既存）。**`schema_version` v1 判定と MigrateToV2Async 実行の組み合わせは、何度再実行しても最終状態が同一になる**ことをプラン設計の前提とする。

### 挿入コード側は変更不要

SQLite-net の `InsertAsync` / `InsertAllAsync` は UNIQUE 違反で `SQLiteException` を投げる。
現状のフロー:
- [SearchViewModel.RegisterAsync](../ViewModels/SearchViewModel.cs#L290): 登録前に `GetExistingSiteNovelIdsAsync` で重複チェック。Novel は確実にユニーク。
- [FetchEpisodeListAsync](../Services/Kakuyomu/KakuyomuApiService.cs#L89): 単一フェッチで EpisodeNo は連番生成。重複なし。
- [UpdateCheckService](../Services/UpdateCheckService.cs): 既存 episodes 件数との差分 insert。EpisodeNo の衝突なし。

いずれも UNIQUE 化で即時壊れるフローなし。万が一の将来の二重挿入はここで検知される（防御として望ましい）。

### 非採用案と理由

| 案 | 却下理由 |
|---|---|
| v2 migration で `episodes` テーブル全 DROP → 再生成 | 既読状態・お気に入り消失。重複除去で十分目的達成できるため過剰 |
| `[Indexed(Unique=true)]` 属性を Episode の Novel+EpisodeNo に付与 | SQLite-net は複合 UNIQUE を属性で表現不可。単一列 UNIQUE のみ |
| FK (`FOREIGN KEY ... ON DELETE CASCADE`) の追加 | SQLite-net は `PRAGMA foreign_keys = ON` を自動設定しない + 既存 DELETE 経路 ([NovelRepository.DeleteAsync](../Services/Database/NovelRepository.cs#L167)) で手動 CASCADE 済。別 PR スコープ |
| schema_version を導入せず直接 UNIQUE 化 | 将来のスキーマ変更のたびに ad-hoc な ALTER 分散 → 枠組み導入が長期的に安価 |

### 代替案（user が希望する場合のみ）: 完全初期化

「必要なら全初期化も OK」の承認を活かす場合、MigrateToV2Async を以下に置換可能:

```csharp
private async Task MigrateToV2Async()
{
    // 完全リセット: app_settings は保持、他は全テーブル DROP → 再作成
    await _connection.ExecuteAsync("DROP TABLE IF EXISTS episode_cache").ConfigureAwait(false);
    await _connection.ExecuteAsync("DROP TABLE IF EXISTS episodes").ConfigureAwait(false);
    await _connection.ExecuteAsync("DROP TABLE IF EXISTS novels").ConfigureAwait(false);
    await _connection.CreateTableAsync<Novel>().ConfigureAwait(false);
    await _connection.CreateTableAsync<Episode>().ConfigureAwait(false);
    await _connection.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
    await _connection.ExecuteAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS idx_novels_site_novel ON novels (site_type, novel_id)"
    ).ConfigureAwait(false);
    await _connection.ExecuteAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_novel_episode ON episodes (novel_id, episode_no)"
    ).ConfigureAwait(false);
    LogHelper.Warn(nameof(DatabaseService),
        "[MigrateToV2] Full reset: novels/episodes/episode_cache cleared. User must re-register novels.");
}
```

**推奨は主案（重複除去方式）**。登録小説・既読状態・お気に入りが保持され、重複がなければ no-op で完了するため安全度が高い。

### PR-3 検証手順

1. `dotnet build _Apps/App.sln --no-restore` が通る。
2. **新規インストール (初回起動):**
   a. `adb uninstall` 後にアプリ起動。
   b. `GetSchemaVersionAsync` は `schema_version` 行不在で **1 を返す** ため、migration が走る。
   c. ログで `Schema migration: v1 → v2` が 1 回だけ出ること。episodes は空テーブルなので `Found N duplicate` は出ず、`[MigrateToV2] Done.` のみ。
   d. migration 完了後 `schema_version = 2` が `app_settings` に insert され、以降の起動では migration スキップ。
3. **既存 v1 DB (重複なし):**
   a. 既存小説登録済みの DB で起動。
   b. ログで `Schema migration: v1 → v2` + `[MigrateToV2] Done.` が出る（dedup の Warn は出ない）。
   c. 小説一覧・既読状態・お気に入りが無傷。
4. **既存 v1 DB (重複あり・合成テスト):**
   a. SQLite ブラウザで `INSERT INTO episodes ... VALUES (novel_1, 1)` を手動で 1 行追加（既存行と衝突）。
   b. アプリ再起動。
   c. ログに `Found N duplicate ...` と `Deleted N duplicate ...` の両方が出る。
   d. 再起動後、重複行は消え、UNIQUE 動作可。
5. **UNIQUE 動作確認:**
   a. SQLite ブラウザで `(novel_id, episode_no)` 重複の手動 INSERT が `SQLITE_CONSTRAINT` エラーになる。
6. **v2 冪等性:**
   a. v2 適用済 DB でアプリ再起動。
   b. `GetSchemaVersionAsync` が 2 を返し、`MigrateAsync` 呼び出しがスキップされる（ログなし）。

---

## スキップ項目と TODO 更新

### F2 — 実施済み
[KakuyomuApiService.cs:132](../Services/Kakuyomu/KakuyomuApiService.cs#L132) に統合版 `ParseApolloState` が既に存在（R4 の修正、`plan_2026-04-16_risk-r1-r14.md` 5節）。
`ParseEpisodeIdsFromApolloState` / `ParseEpisodesFromApolloState` は削除済。

**TODO 更新:** [todo_2026-04-14_code-review.md:66](todo_2026-04-14_code-review.md#L66) の F2 項目を `[x]` にマークし、「R4 修正時に実装済み」の注記を追加。

### F6 — スキップ
[ReaderPage.xaml:11-21, 50-69](../Views/ReaderPage.xaml#L11-L69) のテーマ Trigger は ContentPage と Label で型が異なり、単一 Style に集約不可。
Label 側のみ Style 抽出は可能だが、使用箇所が Reader ページ 1 つのため再利用性なし。コード量も減らない。

**TODO 更新:** F6 項目に `→ skip: ContentPage と Label は型が異なるため単一 Style に集約不可。再利用箇所もない` を追記。

### F7 — スキップ
`BindingContext is` キャスト方式 ([EpisodeListPage](../Views/EpisodeListPage.xaml.cs#L16) / [ReaderPage](../Views/ReaderPage.xaml.cs#L18,L30,L42) / [SettingsPage](../Views/SettingsPage.xaml.cs#L16)) とフィールド保持方式 ([NovelListPage](../Views/NovelListPage.xaml.cs#L7,L12)) の統一。
削減行数 10 行未満、DI 生成ページで BindingContext 差替えはないためどちらも安全、現状の `is` キャスト方式は防御的で読みやすい。

**TODO 更新:** F7 項目に `→ skip: 効果小（削減行数10未満）・どちらも安全・is キャストは防御的で可読性は悪くない` を追記。

---

## 検証の全体サマリー

各 PR で必須のコマンド:

```bash
dotnet build /c/Work/Github/TBird.Library/_Apps/App.sln --no-restore
```

動作確認（Android エミュレータ / 実機）:
- PR-1: 検索・ランキング・ジャンル / 縦書き読書 / ChapterName 表示
- PR-2: 背景テーマ切替・行間切替・アプリ再起動で永続化
- PR-3: 新規インストール / 既存 DB / 重複データ挿入シナリオ

---

## 参照

- 元レビュー: [todo_2026-04-14_code-review.md](todo_2026-04-14_code-review.md)
- 関連プラン: [plan_2026-04-16_risk-r1-r14.md](plan_2026-04-16_risk-r1-r14.md) (R4 で F2 相当修正済)
- 関連プラン: [plan_2026-04-10_pr7-reader-theme-mvvm.md](plan_2026-04-10_pr7-reader-theme-mvvm.md) (Reader MVVM の設計背景)
