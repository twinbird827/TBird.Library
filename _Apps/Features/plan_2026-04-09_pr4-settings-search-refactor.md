# PR4: SettingsPage MVVM 修正 + SearchViewModel 並列化

**対応課題**: [H3](audit_2026-04-08_apps-refactor.md) (SettingsPage codebehind MVVM 違反), [H6](audit_2026-04-08_apps-refactor.md) (SearchViewModel 逐次実行 & エラー上書き)
**ブランチ元**: `app-novelviewer`
**PR 先**: `app-novelviewer`

---

## 0. スコープと前提

### H3: 監査時の記述 vs 実態

監査:
> `_initialized` フラグ + switch で状態復元、ラジオ変更イベントをコードビハインドで処理

**実態通り**。`SettingsPage.xaml.cs`(56 行）に以下の MVVM 違反がある:
- `_viewModel` フィールドと `_initialized` フラグによるコードビハインド状態管理 (7-8 行)
- `OnAppearing` 内の switch 文で RadioButton.IsChecked を命令的に設定 (22-34 行)
- `OnThemeChanged` / `OnSpacingChanged` イベントハンドラで VM プロパティを直接操作 (39-55 行)

**改善内容**: `RadioButtonGroup.SelectedValue` 添付プロパティで RadioButton を純粋な XAML バインディングに置換。コードビハインドを最小化。

### H6: 監査時の記述 vs 実態

監査:
> Narou -> Kakuyomu を順次 await。`HasError` が後段で上書きされる可能性。

**補足**: 実コードでは `if (!HasError)` ガードにより後段エラーは「無視」される（上書きではなく欠落）。
- `SearchAsync` (115-174): 逐次 + エラー欠落
- `FetchRankingAsync` (179-228): 同じ逐次パターン（エラーは LogHelper.Warn のみ、UI 非表示）
- `FetchGenreAsync` (231-269): 同上

3 メソッドとも同一パターンのため全て並列化する。加えて、API サービスが受け付ける `CancellationToken` を各呼び出しに伝搬する。

### 変更対象ファイル

| ファイル | 操作 | 対応課題 |
|---------|------|---------|
| `Views/SettingsPage.xaml` | 改修 | H3 |
| `Views/SettingsPage.xaml.cs` | 改修 | H3 |
| `ViewModels/SearchViewModel.cs` | 改修 | H6 |

**変更不要**:
- `ViewModels/SettingsViewModel.cs` — 既存の `BackgroundTheme` / `LineSpacing` プロパティと `OnXxxChanged` → fire-and-forget save のパイプラインはそのまま利用可能
- `Converters/IntToBoolConverter.cs` — 改修不要。`NovelListPage.xaml:83` で `IsVisible="{Binding UnreadCount, Converter={StaticResource IntToBool}}"` として使用中のため、既存動作を温存する

---

## 1. H3: SettingsPage RadioButton MVVM 修正

### 1.1 設計方針

**`RadioButtonGroup.SelectedValue` 添付プロパティを使用**:

.NET MAUI の `RadioButtonGroup` は親レイアウトに `GroupName` と `SelectedValue` を添付プロパティとして設定でき、各 `RadioButton.Value` との一致で選択状態を自動管理する。`IValueConverter` は不要。

**型一致が必須**: `SelectedValue` は `object` 型で、内部的に `object.Equals()` で比較する。VM の `BackgroundTheme` / `LineSpacing` は `int` のため、XAML 側の `RadioButton.Value` も `<x:Int32>` で int 値を設定する必要がある。`Value="0"` と書くと `string "0"` になり `int 0` と一致しない。

**`Mode=TwoWay` の明示が必須**: `RadioButtonGroup.SelectedValueProperty` は `defaultBindingMode` 未指定（= `OneWay`）で定義されている。双方向バインドには `Mode=TwoWay` を明示する。

**RadioButton バインディングパターン**:
```xml
<HorizontalStackLayout Spacing="8"
                       RadioButtonGroup.GroupName="Theme"
                       RadioButtonGroup.SelectedValue="{Binding BackgroundTheme, Mode=TwoWay}">
    <RadioButton Content="白">
        <RadioButton.Value><x:Int32>0</x:Int32></RadioButton.Value>
    </RadioButton>
    <RadioButton Content="黒">
        <RadioButton.Value><x:Int32>1</x:Int32></RadioButton.Value>
    </RadioButton>
    <RadioButton Content="セピア">
        <RadioButton.Value><x:Int32>2</x:Int32></RadioButton.Value>
    </RadioButton>
</HorizontalStackLayout>
```

**動作フロー**:
1. `InitializeAsync()` → `BackgroundTheme = 1` (DB から) → バインディング → `SelectedValue = 1` (int)
2. `RadioButtonGroup` が子 RadioButton を走査、`Value.Equals(1)` が true の「黒」を選択
3. ユーザーが「セピア」をタップ → `SelectedValue = 2` (int) → バインディング → `BackgroundTheme = 2`
4. `OnBackgroundThemeChanged(2)` → fire-and-forget save

**コンバーター方式を不採用とした理由**:
- `Binding.DoNothing` は WPF 専用 API であり .NET MAUI に存在しない（コンパイル不可）
- MAUI 側の代替 `BindableProperty.UnsetValue` で回避可能だが、`SelectedValue` ならコンバーター自体が不要
- `IntToBoolConverter` を改修すると `NovelListPage.xaml:83` の既存使用（`intValue > 0 → true`）が壊れる

### 1.2 SettingsViewModel への変更: なし

`SettingsViewModel.cs` は変更不要。理由:
- `BackgroundTheme` (`int`, [ObservableProperty]) は `SelectedValue` からの値をそのまま受け取る
- `OnBackgroundThemeChanged(int value)` → fire-and-forget save は既存パイプライン
- `InitializeAsync()` 内の `BackgroundTheme = await _settingsRepo.GetIntValueAsync(...)` → 値変更時にバインディング経由で RadioButton 選択が自動更新
- デフォルト値（`BackgroundTheme = 0`）と DB 値が同じ場合、MVVM Toolkit が等値チェックで PropertyChanged を抑制 → 不要な save/rebind なし

### 1.3 SettingsPage.xaml.cs の最終形

```csharp
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
```

削除されるもの:
- `_viewModel` フィールド（7 行目）
- `_initialized` フラグ（8 行目）
- `OnThemeChanged` イベントハンドラ（39-46 行目）
- `OnSpacingChanged` イベントハンドラ（48-55 行目）
- `OnAppearing` 内の switch 文 2 つ（22-34 行目）

残すもの:
- `OnAppearing` → `InitializeAsync()` 呼び出し（設定値の DB ロードトリガー）。`ReaderPage.xaml.cs`（PR3 後）と同じパターン。

### 1.4 SettingsPage.xaml の変更箇所

**変更箇所は RadioButton のみ（65-83 行目）**。他のバインディング（Slider, Switch, Button）は既に正しい。

**Before** (65-72):
```xml
<HorizontalStackLayout Spacing="8">
    <RadioButton Content="白" IsChecked="True" GroupName="Theme"
                 CheckedChanged="OnThemeChanged" x:Name="ThemeWhite" />
    <RadioButton Content="黒" GroupName="Theme"
                 CheckedChanged="OnThemeChanged" x:Name="ThemeBlack" />
    <RadioButton Content="セピア" GroupName="Theme"
                 CheckedChanged="OnThemeChanged" x:Name="ThemeSepia" />
</HorizontalStackLayout>
```

**After**:
```xml
<HorizontalStackLayout Spacing="8"
                       RadioButtonGroup.GroupName="Theme"
                       RadioButtonGroup.SelectedValue="{Binding BackgroundTheme, Mode=TwoWay}">
    <RadioButton Content="白">
        <RadioButton.Value><x:Int32>0</x:Int32></RadioButton.Value>
    </RadioButton>
    <RadioButton Content="黒">
        <RadioButton.Value><x:Int32>1</x:Int32></RadioButton.Value>
    </RadioButton>
    <RadioButton Content="セピア">
        <RadioButton.Value><x:Int32>2</x:Int32></RadioButton.Value>
    </RadioButton>
</HorizontalStackLayout>
```

**Before** (75-83):
```xml
<HorizontalStackLayout Spacing="8">
    <RadioButton Content="狭" GroupName="Spacing"
                 CheckedChanged="OnSpacingChanged" x:Name="SpacingNarrow" />
    <RadioButton Content="普通" IsChecked="True" GroupName="Spacing"
                 CheckedChanged="OnSpacingChanged" x:Name="SpacingNormal" />
    <RadioButton Content="広" GroupName="Spacing"
                 CheckedChanged="OnSpacingChanged" x:Name="SpacingWide" />
</HorizontalStackLayout>
```

**After**:
```xml
<HorizontalStackLayout Spacing="8"
                       RadioButtonGroup.GroupName="Spacing"
                       RadioButtonGroup.SelectedValue="{Binding LineSpacing, Mode=TwoWay}">
    <RadioButton Content="狭">
        <RadioButton.Value><x:Int32>0</x:Int32></RadioButton.Value>
    </RadioButton>
    <RadioButton Content="普通">
        <RadioButton.Value><x:Int32>1</x:Int32></RadioButton.Value>
    </RadioButton>
    <RadioButton Content="広">
        <RadioButton.Value><x:Int32>2</x:Int32></RadioButton.Value>
    </RadioButton>
</HorizontalStackLayout>
```

削除されるもの: `CheckedChanged` イベント参照、`x:Name` 属性、ハードコード `IsChecked="True"`、個別 `GroupName` 属性
追加されるもの: 親レイアウトの `RadioButtonGroup.GroupName` / `RadioButtonGroup.SelectedValue`、各 RadioButton の `<x:Int32>` Value

---

## 2. H6: SearchViewModel 並列検索

### 2.1 設計方針

**共通ヘルパーメソッド `RunSiteSearchAsync`**:
- 各サイトの検索を独立した `Task` で実行
- 結果とエラーを ValueTuple `(List<SearchResult> hits, string? error)` で返す
- 共有可変状態なし → スレッドセーフ
- 呼び出し側が `Task.WhenAll` で並列実行後、エラーの扱いを決定

```
SearchAsync       → HasError + ErrorMessage にエラー集約（UI 表示）
FetchRankingAsync → LogHelper.Warn でエラー記録（UI 非表示）
FetchGenreAsync   → LogHelper.Warn でエラー記録（UI 非表示）
```

**CancellationToken の伝搬**:
- API サービスは全メソッドで `CancellationToken ct = default` を受け付ける
- 各検索メソッドで `CancellationTokenSource(TimeSpan.FromSeconds(30))` を作成し、全体タイムアウトを設定
- トークンはラムダのクロージャ経由で API 呼び出しに伝搬
- `RunSiteSearchAsync` の `TaskCanceledException` catch で「タイムアウト」メッセージを返す既存パターンと自然に統合

**変更メソッド一覧**:

| メソッド | 行 | 変更内容 |
|---------|-----|---------|
| `SearchAsync` | 115-174 | 並列化 + エラー集約 + CancellationToken |
| `FetchRankingAsync` | 179-228 | 並列化 + 既存 LogHelper.Warn 維持 + CancellationToken |
| `FetchGenreAsync` | 231-269 | 並列化 + 既存 LogHelper.Warn 維持 + CancellationToken |
| `RegisterAsync` | 321 | `Task.Run` 除去（軽微） |
| **(新規)** `RunSiteSearchAsync` | - | 共通ヘルパー |

### 2.2 ヘルパーメソッド

```csharp
private static async Task<(List<SearchResult> hits, string? error)> RunSiteSearchAsync(
    Func<Task<List<SearchResult>>> search, string siteName)
{
    try
    {
        return (await search(), null);
    }
    catch (TaskCanceledException)
    {
        return ([], $"{siteName}の検索がタイムアウトしました");
    }
    catch (HttpRequestException ex)
    {
        return ([], $"{siteName}の通信エラー: {ex.Message}");
    }
    catch (Exception ex)
    {
        return ([], $"{siteName}のエラー: {ex.Message}");
    }
}
```

**設計判断**:
- `static` — VM インスタンスに依存しない純粋関数
- `CancellationToken` を引数に取らない — 呼び出し側がラムダのクロージャで API メソッドにトークンを渡すため、ヘルパー自体はトークンを知る必要がない
- `HttpRequestException` を分離 — `SearchAsync` の既存エラーメッセージ粒度を維持
- `Exception` 汎用 catch — `FetchRankingAsync`/`FetchGenreAsync` の既存パターンをカバー

### 2.3 SearchAsync の変更

**Before** (115-174): Narou await → Kakuyomu await → エラー欠落
**After**:

```csharp
[RelayCommand(CanExecute = nameof(CanSearch))]
private async Task SearchAsync()
{
    IsLoading = true;
    HasError = false;
    ErrorMessage = string.Empty;

    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        var searchTarget = "Both";

        var narouTask = SearchNarou
            ? RunSiteSearchAsync(() => _narou.SearchAsync(SearchKeyword, searchTarget, ct), "なろう")
            : Task.FromResult<(List<SearchResult>, string?)>(([], null));
        var kakuyomuTask = SearchKakuyomu
            ? RunSiteSearchAsync(() => _kakuyomu.SearchAsync(SearchKeyword, searchTarget, ct), "カクヨム")
            : Task.FromResult<(List<SearchResult>, string?)>(([], null));

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
        LogHelper.Error(nameof(SearchViewModel), $"Search failed: {ex.Message}");
        HasError = true;
        ErrorMessage = "通信エラーが発生しました";
    }
    finally
    {
        IsLoading = false;
    }
}
```

**改善点**:
- Narou と Kakuyomu を `Task.WhenAll` で並列実行 → レイテンシ最大半減
- エラーが両方発生した場合、改行結合で両方表示（以前: 先方のみ表示、後方は無視）
- `CancellationTokenSource(30s)` で全体タイムアウトを設定、API 呼び出しにトークン伝搬

### 2.4 FetchRankingAsync の変更

**Before** (179-228): 逐次 + 個別 try-catch
**After**:

```csharp
[RelayCommand]
private async Task FetchRankingAsync()
{
    IsLoading = true;
    HasError = false;
    ErrorMessage = string.Empty;
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        var period = (RankingPeriod)Math.Clamp(RankingPeriodIndex, 0, 3);

        var narouTask = SearchNarou
            ? RunSiteSearchAsync(() =>
            {
                int? bg = null;
                if (SelectedNarouBigGenre is not null && int.TryParse(SelectedNarouBigGenre.Id, out var bgv)) bg = bgv;
                return _narou.FetchRankingAsync(period, bg, 30, ct);
            }, "なろう")
            : Task.FromResult<(List<SearchResult>, string?)>(([], null));

        var kakuyomuTask = SearchKakuyomu
            ? RunSiteSearchAsync(() =>
            {
                var periodSlug = period switch
                {
                    RankingPeriod.Daily => "daily",
                    RankingPeriod.Weekly => "weekly",
                    RankingPeriod.Monthly => "monthly",
                    _ => "weekly",
                };
                return _kakuyomu.FetchRankingAsync(
                    SelectedKakuyomuGenre?.Id ?? "all", periodSlug, ct);
            }, "カクヨム")
            : Task.FromResult<(List<SearchResult>, string?)>(([], null));

        var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

        var allHits = siteResults.SelectMany(r => r.hits).ToList();
        foreach (var r in siteResults)
        {
            if (r.error is not null)
                LogHelper.Warn(nameof(SearchViewModel), r.error);
        }

        await ShowResultsAsync(allHits);
    }
    finally { IsLoading = false; }
}
```

### 2.5 FetchGenreAsync の変更

**Before** (231-269): 逐次 + 個別 try-catch
**After**:

```csharp
[RelayCommand]
private async Task FetchGenreAsync()
{
    IsLoading = true;
    HasError = false;
    ErrorMessage = string.Empty;
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        var narouTask = SearchNarou && SelectedNarouBigGenre is not null && int.TryParse(SelectedNarouBigGenre.Id, out var bg)
            ? RunSiteSearchAsync(() => _narou.FetchByGenreAsync(bg, "weeklypoint", 30, ct), "なろう")
            : Task.FromResult<(List<SearchResult>, string?)>(([], null));

        var kakuyomuTask = SearchKakuyomu && SelectedKakuyomuGenre is not null
            ? RunSiteSearchAsync(() => _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre.Id, "weekly", ct), "カクヨム")
            : Task.FromResult<(List<SearchResult>, string?)>(([], null));

        var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

        var allHits = siteResults.SelectMany(r => r.hits).ToList();
        foreach (var r in siteResults)
        {
            if (r.error is not null)
                LogHelper.Warn(nameof(SearchViewModel), r.error);
        }

        await ShowResultsAsync(allHits);
    }
    finally { IsLoading = false; }
}
```

### 2.6 RegisterAsync の軽微修正（321 行目）

**Before**:
```csharp
_ = Task.Run(() => _prefetch.EnqueueNovelAsync(dbNovel.Id));
```

**After**:
```csharp
_ = _prefetch.EnqueueNovelAsync(dbNovel.Id);
```

`EnqueueNovelAsync` は最初の行が `await _novelRepo.GetByIdAsync(...)` であり、同期的なブロッキング処理がないため `Task.Run` は不要。`ConfigureAwait(false)` により継続はスレッドプールで実行される。fire-and-forget パターンは維持。

---

## 3. 完全な実装コード

### 3.1 `Views/SettingsPage.xaml`（全文）

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:LanobeReader.ViewModels"
             x:Class="LanobeReader.Views.SettingsPage"
             x:DataType="vm:SettingsViewModel"
             Title="設定">

    <ScrollView>
        <VerticalStackLayout Padding="16" Spacing="24">

            <!-- Cache settings -->
            <VerticalStackLayout Spacing="8">
                <Label Text="キャッシュ設定" Style="{StaticResource SectionHeaderLabel}" />

                <Label FontSize="14">
                    <Label.Text>
                        <Binding Path="CacheMonths" StringFormat="保存期間: {0}ヶ月" />
                    </Label.Text>
                </Label>
                <Slider Minimum="1" Maximum="24" Value="{Binding CacheMonths}"
                        Style="{StaticResource ThemedSlider}" />

                <Button Text="キャッシュをすべてクリア" Command="{Binding ClearCacheCommand}"
                        BackgroundColor="#F44336" TextColor="White" />
            </VerticalStackLayout>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Update settings -->
            <VerticalStackLayout Spacing="8">
                <Label Text="更新設定" Style="{StaticResource SectionHeaderLabel}" />

                <Label FontSize="14">
                    <Label.Text>
                        <Binding Path="UpdateIntervalHours" StringFormat="チェック間隔: {0}時間" />
                    </Label.Text>
                </Label>
                <Slider Minimum="1" Maximum="24" Value="{Binding UpdateIntervalHours}"
                        Style="{StaticResource ThemedSlider}" />
            </VerticalStackLayout>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Reading settings -->
            <VerticalStackLayout Spacing="8">
                <Label Text="読書設定" Style="{StaticResource SectionHeaderLabel}" />

                <!-- Font size -->
                <Label FontSize="14">
                    <Label.Text>
                        <Binding Path="FontSizeSp" StringFormat="フォントサイズ: {0}sp" />
                    </Label.Text>
                </Label>
                <Slider Minimum="12" Maximum="24" Value="{Binding FontSizeSp}"
                        Style="{StaticResource ThemedSlider}" />

                <!-- Preview -->
                <Frame Padding="12" CornerRadius="8" HasShadow="True">
                    <Label Text="{Binding PreviewText}" FontSize="{Binding FontSizeSp}" />
                </Frame>

                <!-- Background theme -->
                <Label Text="背景色テーマ" FontSize="14" Margin="0,8,0,0" />
                <HorizontalStackLayout Spacing="8"
                                       RadioButtonGroup.GroupName="Theme"
                                       RadioButtonGroup.SelectedValue="{Binding BackgroundTheme, Mode=TwoWay}">
                    <RadioButton Content="白">
                        <RadioButton.Value><x:Int32>0</x:Int32></RadioButton.Value>
                    </RadioButton>
                    <RadioButton Content="黒">
                        <RadioButton.Value><x:Int32>1</x:Int32></RadioButton.Value>
                    </RadioButton>
                    <RadioButton Content="セピア">
                        <RadioButton.Value><x:Int32>2</x:Int32></RadioButton.Value>
                    </RadioButton>
                </HorizontalStackLayout>

                <!-- Line spacing -->
                <Label Text="行間" FontSize="14" Margin="0,8,0,0" />
                <HorizontalStackLayout Spacing="8"
                                       RadioButtonGroup.GroupName="Spacing"
                                       RadioButtonGroup.SelectedValue="{Binding LineSpacing, Mode=TwoWay}">
                    <RadioButton Content="狭">
                        <RadioButton.Value><x:Int32>0</x:Int32></RadioButton.Value>
                    </RadioButton>
                    <RadioButton Content="普通">
                        <RadioButton.Value><x:Int32>1</x:Int32></RadioButton.Value>
                    </RadioButton>
                    <RadioButton Content="広">
                        <RadioButton.Value><x:Int32>2</x:Int32></RadioButton.Value>
                    </RadioButton>
                </HorizontalStackLayout>

                <!-- Vertical writing -->
                <HorizontalStackLayout Spacing="8" Margin="0,8,0,0">
                    <Switch IsToggled="{Binding VerticalWriting}" />
                    <Label Text="縦書き表示" FontSize="14" VerticalOptions="Center" />
                </HorizontalStackLayout>
            </VerticalStackLayout>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Network / Prefetch settings -->
            <VerticalStackLayout Spacing="8">
                <Label Text="通信設定" Style="{StaticResource SectionHeaderLabel}" />

                <HorizontalStackLayout Spacing="8">
                    <Switch IsToggled="{Binding PrefetchEnabled}" />
                    <Label Text="Wi-Fi接続時にバックグラウンド先読みする" FontSize="13" VerticalOptions="Center" />
                </HorizontalStackLayout>
                <Label FontSize="11" TextColor="Gray"
                       Text="モバイル通信時は常に先読みしません" />

                <Label FontSize="14" Margin="0,8,0,0">
                    <Label.Text>
                        <Binding Path="RequestDelayMs" StringFormat="リクエスト間ディレイ: {0}ms" />
                    </Label.Text>
                </Label>
                <Slider Minimum="500" Maximum="2000" Value="{Binding RequestDelayMs}"
                        Style="{StaticResource ThemedSlider}" />
            </VerticalStackLayout>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Paging settings -->
            <VerticalStackLayout Spacing="8">
                <Label Text="目次ページ設定" Style="{StaticResource SectionHeaderLabel}" />

                <Label FontSize="14">
                    <Label.Text>
                        <Binding Path="EpisodesPerPage" StringFormat="1ページ件数: {0}件" />
                    </Label.Text>
                </Label>
                <Slider Minimum="10" Maximum="100" Value="{Binding EpisodesPerPage}"
                        Style="{StaticResource ThemedSlider}" />
            </VerticalStackLayout>

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

### 3.2 `Views/SettingsPage.xaml.cs`（全文）

```csharp
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
```

### 3.3 `ViewModels/SearchViewModel.cs`（全文）

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using LanobeReader.Services.Kakuyomu;
using LanobeReader.Services.Narou;

namespace LanobeReader.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly INovelServiceFactory _serviceFactory;
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly NarouApiService _narou;
    private readonly KakuyomuApiService _kakuyomu;
    private readonly PrefetchService _prefetch;

    public SearchViewModel(
        INovelServiceFactory serviceFactory,
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        NarouApiService narou,
        KakuyomuApiService kakuyomu,
        PrefetchService prefetch)
    {
        _serviceFactory = serviceFactory;
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _narou = narou;
        _kakuyomu = kakuyomu;
        _prefetch = prefetch;

        NarouBigGenres = new ObservableCollection<GenreInfo>(NarouGenres.BigGenres);
        KakuyomuGenreList = new ObservableCollection<GenreInfo>(KakuyomuGenres.Genres);
        KakuyomuPeriodList = new ObservableCollection<GenreInfo>(KakuyomuGenres.Periods);

        SelectedNarouBigGenre = NarouBigGenres.First();
        SelectedKakuyomuGenre = KakuyomuGenreList.First();
        SelectedKakuyomuPeriod = KakuyomuPeriodList.First();
    }

    // Mode: 0=Keyword, 1=Ranking, 2=Genre browse
    [ObservableProperty]
    private int _mode;

    public bool IsKeywordMode => Mode == 0;
    public bool IsRankingMode => Mode == 1;
    public bool IsGenreMode => Mode == 2;

    partial void OnModeChanged(int value)
    {
        OnPropertyChanged(nameof(IsKeywordMode));
        OnPropertyChanged(nameof(IsRankingMode));
        OnPropertyChanged(nameof(IsGenreMode));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _searchNarou = true;

    [ObservableProperty]
    private bool _searchKakuyomu = true;

    [ObservableProperty]
    private ObservableCollection<SearchResultViewModel> _searchResults = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // Ranking/Genre browse
    public ObservableCollection<GenreInfo> NarouBigGenres { get; }
    public ObservableCollection<GenreInfo> KakuyomuGenreList { get; }
    public ObservableCollection<GenreInfo> KakuyomuPeriodList { get; }

    [ObservableProperty]
    private GenreInfo? _selectedNarouBigGenre;

    [ObservableProperty]
    private GenreInfo? _selectedKakuyomuGenre;

    [ObservableProperty]
    private GenreInfo? _selectedKakuyomuPeriod;

    [ObservableProperty]
    private int _rankingPeriodIndex; // 0=Daily 1=Weekly 2=Monthly 3=Quarterly

    [RelayCommand]
    private void SetModeKeyword() => Mode = 0;

    [RelayCommand]
    private void SetModeRanking() => Mode = 1;

    [RelayCommand]
    private void SetModeGenre() => Mode = 2;

    private static async Task<(List<SearchResult> hits, string? error)> RunSiteSearchAsync(
        Func<Task<List<SearchResult>>> search, string siteName)
    {
        try
        {
            return (await search(), null);
        }
        catch (TaskCanceledException)
        {
            return ([], $"{siteName}の検索がタイムアウトしました");
        }
        catch (HttpRequestException ex)
        {
            return ([], $"{siteName}の通信エラー: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ([], $"{siteName}のエラー: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = cts.Token;
            var searchTarget = "Both";

            var narouTask = SearchNarou
                ? RunSiteSearchAsync(() => _narou.SearchAsync(SearchKeyword, searchTarget, ct), "なろう")
                : Task.FromResult<(List<SearchResult>, string?)>(([], null));
            var kakuyomuTask = SearchKakuyomu
                ? RunSiteSearchAsync(() => _kakuyomu.SearchAsync(SearchKeyword, searchTarget, ct), "カクヨム")
                : Task.FromResult<(List<SearchResult>, string?)>(([], null));

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
            LogHelper.Error(nameof(SearchViewModel), $"Search failed: {ex.Message}");
            HasError = true;
            ErrorMessage = "通信エラーが発生しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchKeyword) && !IsLoading;

    [RelayCommand]
    private async Task FetchRankingAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = cts.Token;
            var period = (RankingPeriod)Math.Clamp(RankingPeriodIndex, 0, 3);

            var narouTask = SearchNarou
                ? RunSiteSearchAsync(() =>
                {
                    int? bg = null;
                    if (SelectedNarouBigGenre is not null && int.TryParse(SelectedNarouBigGenre.Id, out var bgv)) bg = bgv;
                    return _narou.FetchRankingAsync(period, bg, 30, ct);
                }, "なろう")
                : Task.FromResult<(List<SearchResult>, string?)>(([], null));

            var kakuyomuTask = SearchKakuyomu
                ? RunSiteSearchAsync(() =>
                {
                    var periodSlug = period switch
                    {
                        RankingPeriod.Daily => "daily",
                        RankingPeriod.Weekly => "weekly",
                        RankingPeriod.Monthly => "monthly",
                        _ => "weekly",
                    };
                    return _kakuyomu.FetchRankingAsync(
                        SelectedKakuyomuGenre?.Id ?? "all", periodSlug, ct);
                }, "カクヨム")
                : Task.FromResult<(List<SearchResult>, string?)>(([], null));

            var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

            var allHits = siteResults.SelectMany(r => r.hits).ToList();
            foreach (var r in siteResults)
            {
                if (r.error is not null)
                    LogHelper.Warn(nameof(SearchViewModel), r.error);
            }

            await ShowResultsAsync(allHits);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task FetchGenreAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = cts.Token;

            var narouTask = SearchNarou && SelectedNarouBigGenre is not null && int.TryParse(SelectedNarouBigGenre.Id, out var bg)
                ? RunSiteSearchAsync(() => _narou.FetchByGenreAsync(bg, "weeklypoint", 30, ct), "なろう")
                : Task.FromResult<(List<SearchResult>, string?)>(([], null));

            var kakuyomuTask = SearchKakuyomu && SelectedKakuyomuGenre is not null
                ? RunSiteSearchAsync(() => _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre.Id, "weekly", ct), "カクヨム")
                : Task.FromResult<(List<SearchResult>, string?)>(([], null));

            var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

            var allHits = siteResults.SelectMany(r => r.hits).ToList();
            foreach (var r in siteResults)
            {
                if (r.error is not null)
                    LogHelper.Warn(nameof(SearchViewModel), r.error);
            }

            await ShowResultsAsync(allHits);
        }
        finally { IsLoading = false; }
    }

    private async Task ShowResultsAsync(List<SearchResult> results)
    {
        var viewModels = new List<SearchResultViewModel>();
        foreach (var result in results)
        {
            var existing = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
            viewModels.Add(SearchResultViewModel.FromModel(result, existing is not null));
        }
        SearchResults = new ObservableCollection<SearchResultViewModel>(viewModels);
        HasSearched = true;
    }

    [RelayCommand]
    private async Task RegisterAsync(SearchResultViewModel result)
    {
        if (result.IsRegistered || result.IsRegistering) return;

        result.IsRegistering = true;
        try
        {
            var novel = new Novel
            {
                SiteType = (int)result.SiteType,
                NovelId = result.NovelId,
                Title = result.Title,
                Author = result.Author,
                TotalEpisodes = result.TotalEpisodes,
                IsCompleted = result.IsCompleted ? 1 : 0,
                RegisteredAt = DateTime.UtcNow.ToString("o"),
                LastUpdatedAt = DateTime.UtcNow.ToString("o"),
            };

            await _novelRepo.InsertAsync(novel);

            var service = _serviceFactory.GetService(result.SiteType);
            var episodes = await service.FetchEpisodeListAsync(result.NovelId);

            var dbNovel = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
            if (dbNovel is not null)
            {
                foreach (var ep in episodes)
                {
                    ep.NovelId = dbNovel.Id;
                }
                await _episodeRepo.InsertAllAsync(episodes);

                dbNovel.TotalEpisodes = episodes.Count;
                await _novelRepo.UpdateAsync(dbNovel);

                // Auto-enqueue prefetch for newly registered novel (Wi-Fi only)
                _ = _prefetch.EnqueueNovelAsync(dbNovel.Id);
            }

            result.IsRegistered = true;
            result.TotalEpisodes = episodes.Count;
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(SearchViewModel), $"Register failed: {ex.Message}");
            await Shell.Current.DisplayAlert("エラー", $"登録に失敗しました: {ex.Message}", "OK");
        }
        finally
        {
            result.IsRegistering = false;
        }
    }
}
```

---

## 4. コミット計画

### Commit 1: `refactor(SettingsPage): replace codebehind with RadioButtonGroup.SelectedValue binding (H3)`

**ファイル**:
1. `Views/SettingsPage.xaml` — RadioButton を `RadioButtonGroup.SelectedValue` + `x:Int32` バインディングに置換
2. `Views/SettingsPage.xaml.cs` — 56 行 → 21 行に削減

**ビルド検証**: `dotnet build` 成功を確認

### Commit 2: `perf(SearchViewModel): parallelize dual-site search with Task.WhenAll (H6)`

**ファイル**:
1. `ViewModels/SearchViewModel.cs` — `RunSiteSearchAsync` ヘルパー追加、`SearchAsync`/`FetchRankingAsync`/`FetchGenreAsync` 並列化 + CancellationToken 伝搬、`RegisterAsync` の `Task.Run` 除去

**ビルド検証**: `dotnet build` 成功を確認

---

## 5. 検証チェックリスト

### H3: SettingsPage

- [ ] 設定画面を開く → ラジオボタンが DB 保存値に一致する
- [ ] テーマ「黒」を選択 → `BackgroundTheme` が 1 に変わり DB に保存される
- [ ] テーマ「セピア」→「白」と切り替え → 正しく反映
- [ ] 行間「狭」→「広」と切り替え → 正しく反映
- [ ] 設定画面を閉じて再度開く → 前回の選択が復元される
- [ ] リーダー画面で反映を確認（テーマ色・行間）
- [ ] `NovelListPage` の未読バッジが正常に表示される（`IntToBoolConverter` 未変更の確認）

### H6: SearchViewModel

- [ ] キーワード検索（両サイト有効）→ 結果が表示される
- [ ] キーワード検索（片方のみ有効）→ 有効なサイトの結果のみ
- [ ] ネットワークエラーシミュレーション → エラーメッセージが表示される
- [ ] 両サイトでエラー → 2 行のエラーメッセージが表示される（改行結合）
- [ ] 30 秒タイムアウト → タイムアウトメッセージが表示される
- [ ] ランキング取得 → 結果が表示される
- [ ] ジャンル検索 → 結果が表示される
- [ ] 小説登録 → 登録成功・先読みキューに追加される
