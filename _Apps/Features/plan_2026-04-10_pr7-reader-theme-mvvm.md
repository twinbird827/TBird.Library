# PR7: Reader テーマ色のMVVM正規化

作成日: 2026-04-10
対象: `_Apps` — ReaderViewModel, ReaderPage, ReaderWebView, ThemeHelper, Colors.xaml
前提: PR6マージ後に実施

## Context

現在ReaderViewModelがプレゼンテーション詳細を知っている:
- Color型プロパティ（BackgroundColor, TextColor）— ThemeHelper.GetThemeColorsで色を決定
- double型プロパティ（LineHeight）— ThemeHelper.GetLineHeightでインデックス→CSS値変換

正道: VMはセマンティックなインデックス（テーマ種別、行間種別）のみ公開し、View側がDataTrigger/CSS解決で実際の値を決定する。ThemeHelper全体をView層に移行し削除。

## 方針

**Colors.xamlを色の単一定義源**にし、XAML側はDataTrigger、WebView側はApplication.Current.Resourcesから取得。
行間もView側で解決（DataTrigger + WebView内部マッピング）。

## 変更ファイル一覧

1. `_Apps/Resources/Styles/Colors.xaml` — テーマ色6色追加
2. `_Apps/ViewModels/ReaderViewModel.cs` — Color/LineHeight→インデックス公開に変更
3. `_Apps/Views/ReaderPage.xaml` — DataTrigger追加（テーマ色3+行間3=本文6, ページBG3, 計9）
4. `_Apps/Controls/ReaderWebView.cs` — インデックスからCSS値を解決
5. `_Apps/Helpers/ReaderStyleResolver.cs` — **新規**（テーマ色+行間解決、ThemeHelper代替）
6. `_Apps/Helpers/ThemeHelper.cs` — **完全削除**
7. `_Apps/Helpers/ReaderCssState.cs` — プレゼンテーション値→セマンティックインデックスに変更
8. `_Apps/Helpers/ReaderHtmlBuilder.cs` — CSS生成をReaderStyleResolver経由に変更

---

## 詳細

### Colors.xaml — テーマ色追加（PR6で追加したセマンティックカラーの後に）

```xml
<!-- Reader theme colors -->
<Color x:Key="ThemeWhiteBg">#FFFFFF</Color>
<Color x:Key="ThemeWhiteText">#212121</Color>
<Color x:Key="ThemeDarkBg">#121212</Color>
<Color x:Key="ThemeDarkText">#E0E0E0</Color>
<Color x:Key="ThemeSepiaBg">#F5E6C8</Color>
<Color x:Key="ThemeSepiaText">#3E2C1C</Color>
```

### ReaderViewModel — プレゼンテーション値プロパティ廃止

```csharp
// 削除:
// [ObservableProperty] private Color _backgroundColor = Color.FromArgb("#FFFFFF");
// [ObservableProperty] private Color _textColor = Color.FromArgb("#212121");
// [ObservableProperty] private double _lineHeight = 1.7;
// partial void OnBackgroundColorChanged(Color value) => UpdateCssStateIfReady();
// partial void OnTextColorChanged(Color value) => UpdateCssStateIfReady();
// partial void OnLineHeightChanged(double value) => UpdateCssStateIfReady();
// private static string ColorToHex(Color c) => ...;

// 変更: セマンティックインデックスをObservablePropertyとして公開
[ObservableProperty]
private int _backgroundThemeIndex;

[ObservableProperty]
private int _lineSpacingIndex = SettingsKeys.DEFAULT_LINE_SPACING;

partial void OnBackgroundThemeIndexChanged(int value) => UpdateCssStateIfReady();
partial void OnLineSpacingIndexChanged(int value) => UpdateCssStateIfReady();
```

LoadSettingsAsync変更:
```csharp
// Before
var (bg, text) = ThemeHelper.GetThemeColors(_backgroundThemeIndex);
var lh = ThemeHelper.GetLineHeight(lineSpacing);
BackgroundColor = bg; TextColor = text; LineHeight = lh;

// After
BackgroundThemeIndex = await _settingsRepo.GetIntValueAsync(
    SettingsKeys.BACKGROUND_THEME, SettingsKeys.DEFAULT_BACKGROUND_THEME);
LineSpacingIndex = await _settingsRepo.GetIntValueAsync(
    SettingsKeys.LINE_SPACING, SettingsKeys.DEFAULT_LINE_SPACING);
// プレゼンテーション値の設定不要 — Viewが担当
```

BuildCssState変更:
```csharp
// Before
private ReaderCssState BuildCssState() => new(
    FontSizePx: FontSize, LineHeight: LineHeight,
    BackgroundHex: ColorToHex(BackgroundColor),
    ForegroundHex: ColorToHex(TextColor));

// After（セマンティックインデックスを渡す）
private ReaderCssState BuildCssState() => new(
    FontSizePx: FontSize,
    LineSpacingIndex: LineSpacingIndex,
    BackgroundThemeIndex: BackgroundThemeIndex);
```

### ReaderCssState — 構造変更

```csharp
// Before
public sealed record ReaderCssState(
    double FontSizePx, double LineHeight,
    string BackgroundHex, string ForegroundHex);

// After
public sealed record ReaderCssState(
    double FontSizePx,
    int LineSpacingIndex,
    int BackgroundThemeIndex);
```

### ReaderPage.xaml — DataTrigger

ContentPageのBackgroundColor:
```xml
<ContentPage ...>
    <ContentPage.Triggers>
        <DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}" Value="0">
            <Setter Property="BackgroundColor" Value="{StaticResource ThemeWhiteBg}" />
        </DataTrigger>
        <DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}" Value="1">
            <Setter Property="BackgroundColor" Value="{StaticResource ThemeDarkBg}" />
        </DataTrigger>
        <DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}" Value="2">
            <Setter Property="BackgroundColor" Value="{StaticResource ThemeSepiaBg}" />
        </DataTrigger>
    </ContentPage.Triggers>
```

本文Label（横書き）のTextColor + LineHeight:
```xml
    <Label Text="{Binding EpisodeContent}" FontSize="{Binding FontSize}" ...>
        <Label.Triggers>
            <!-- TextColor by theme -->
            <DataTrigger TargetType="Label" Binding="{Binding BackgroundThemeIndex}" Value="0">
                <Setter Property="TextColor" Value="{StaticResource ThemeWhiteText}" />
            </DataTrigger>
            <DataTrigger TargetType="Label" Binding="{Binding BackgroundThemeIndex}" Value="1">
                <Setter Property="TextColor" Value="{StaticResource ThemeDarkText}" />
            </DataTrigger>
            <DataTrigger TargetType="Label" Binding="{Binding BackgroundThemeIndex}" Value="2">
                <Setter Property="TextColor" Value="{StaticResource ThemeSepiaText}" />
            </DataTrigger>
            <!-- LineHeight by spacing index -->
            <DataTrigger TargetType="Label" Binding="{Binding LineSpacingIndex}" Value="0">
                <Setter Property="LineHeight" Value="1.4" />
            </DataTrigger>
            <DataTrigger TargetType="Label" Binding="{Binding LineSpacingIndex}" Value="1">
                <Setter Property="LineHeight" Value="1.7" />
            </DataTrigger>
            <DataTrigger TargetType="Label" Binding="{Binding LineSpacingIndex}" Value="2">
                <Setter Property="LineHeight" Value="2.1" />
            </DataTrigger>
        </Label.Triggers>
    </Label>
```

既存の `BackgroundColor="{Binding BackgroundColor}"`, `TextColor="{Binding TextColor}"`, `LineHeight="{Binding LineHeight}"` バインディングは削除。

### ReaderWebView — セマンティックインデックスからCSS値を解決

ApplyCssAsync内でApplication.Current.Resourcesからテーマ色を取得し、行間もインデックスから解決:

```csharp
private static (string bg, string fg) ResolveThemeColors(int themeIndex)
{
    var bgKey = themeIndex switch { 1 => "ThemeDarkBg", 2 => "ThemeSepiaBg", _ => "ThemeWhiteBg" };
    var fgKey = themeIndex switch { 1 => "ThemeDarkText", 2 => "ThemeSepiaText", _ => "ThemeWhiteText" };

    var bg = Application.Current!.Resources.TryGetValue(bgKey, out var b) && b is Color bc
        ? ColorToHex(bc) : "#FFFFFF";
    var fg = Application.Current!.Resources.TryGetValue(fgKey, out var f) && f is Color fc
        ? ColorToHex(fc) : "#212121";
    return (bg, fg);
}

private static double ResolveLineHeight(int lineSpacingIndex) => lineSpacingIndex switch
{
    0 => 1.4,
    2 => 2.1,
    _ => 1.7,
};

private static string ColorToHex(Color c) =>
    $"#{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}";
```

### ReaderHtmlBuilder.cs / ReaderWebView.cs — 共通ヘルパー経由でCSS値を解決

ReaderHtmlBuilder（初回HTML生成）とReaderWebView（ライブCSS差し替え）の両方でテーマ色と行間を解決する必要があるため、共通の`ReaderStyleResolver`を新設。

**新規: `_Apps/Helpers/ReaderStyleResolver.cs`**
```csharp
using System.Globalization;

namespace LanobeReader.Helpers;

public static class ReaderStyleResolver
{
    public static (string bg, string fg) ResolveThemeColors(int themeIndex)
    {
        var bgKey = themeIndex switch { 1 => "ThemeDarkBg", 2 => "ThemeSepiaBg", _ => "ThemeWhiteBg" };
        var fgKey = themeIndex switch { 1 => "ThemeDarkText", 2 => "ThemeSepiaText", _ => "ThemeWhiteText" };

        var bg = Application.Current!.Resources.TryGetValue(bgKey, out var b) && b is Color bc
            ? ColorToHex(bc) : "#FFFFFF";
        var fg = Application.Current!.Resources.TryGetValue(fgKey, out var f) && f is Color fc
            ? ColorToHex(fc) : "#212121";
        return (bg, fg);
    }

    public static double ResolveLineHeight(int lineSpacingIndex) => lineSpacingIndex switch
    {
        0 => 1.4, 2 => 2.1, _ => 1.7,
    };

    public static string ColorToHex(Color c) =>
        $"#{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}";
}
```

**ReaderHtmlBuilder.cs Build内（lines 22-25）**:
```csharp
// Before
sb.Append($"--reader-fs:{state.FontSizePx.ToString("0.##", inv)}px;");
sb.Append($"--reader-lh:{state.LineHeight.ToString("0.##", inv)};");
sb.Append($"--reader-bg:{state.BackgroundHex};");
sb.Append($"--reader-fg:{state.ForegroundHex};");

// After
var (bgHex, fgHex) = ReaderStyleResolver.ResolveThemeColors(state.BackgroundThemeIndex);
var lh = ReaderStyleResolver.ResolveLineHeight(state.LineSpacingIndex);
sb.Append($"--reader-fs:{state.FontSizePx.ToString("0.##", inv)}px;");
sb.Append($"--reader-lh:{lh.ToString("0.##", inv)};");
sb.Append($"--reader-bg:{bgHex};");
sb.Append($"--reader-fg:{fgHex};");
```

**ReaderWebView.cs ApplyCssAsync（lines 87-96）**:
```csharp
// Before
private async Task ApplyCssAsync(ReaderCssState state)
{
    var inv = CultureInfo.InvariantCulture;
    var js =
        "(function(){var s=document.documentElement.style;" +
        $"s.setProperty('--reader-fs','{state.FontSizePx.ToString("0.##", inv)}px');" +
        $"s.setProperty('--reader-lh','{state.LineHeight.ToString("0.##", inv)}');" +
        $"s.setProperty('--reader-bg','{state.BackgroundHex}');" +
        $"s.setProperty('--reader-fg','{state.ForegroundHex}');" +
        "})();";

// After
private async Task ApplyCssAsync(ReaderCssState state)
{
    var inv = CultureInfo.InvariantCulture;
    var (bgHex, fgHex) = ReaderStyleResolver.ResolveThemeColors(state.BackgroundThemeIndex);
    var lh = ReaderStyleResolver.ResolveLineHeight(state.LineSpacingIndex);
    var js =
        "(function(){var s=document.documentElement.style;" +
        $"s.setProperty('--reader-fs','{state.FontSizePx.ToString("0.##", inv)}px');" +
        $"s.setProperty('--reader-lh','{lh.ToString("0.##", inv)}');" +
        $"s.setProperty('--reader-bg','{bgHex}');" +
        $"s.setProperty('--reader-fg','{fgHex}');" +
        "})();";
```

`using LanobeReader.Helpers;` はReaderWebView.csに既存（line 3）。

### ThemeHelper — 完全削除

全機能がReaderStyleResolverとDataTriggerに移行したため、ThemeHelper.csファイルごと削除。

---

## 注意点

- PR6のColors.xaml変更（セマンティックカラー5色）にテーマ色6色を追加する形
- PR6完了後にapp-novelviewerから分岐
- ReaderViewModelの`_backgroundColor`/`_textColor`/`_lineHeight`フィールドとColorToHexメソッドは本PRで削除されるため、PR6では意図的にconst化対象外としている

## 変更サマリ

| ファイル | 変更 | 行数 |
|---|---|---|
| Colors.xaml | テーマ色6色追加 | +6 |
| ReaderViewModel.cs | Color/LineHeight削除、インデックス公開、BuildCssState変更 | +6/-20 |
| ReaderPage.xaml | DataTrigger 9個追加、Binding 3個削除 | +36/-3 |
| ReaderStyleResolver.cs | 新規（テーマ色+行間解決） | +30 |
| ReaderWebView.cs | ApplyCssAsync変更（ReaderStyleResolver経由） | +4/-4 |
| ReaderCssState.cs | プレゼンテーション値→インデックス | +2/-3 |
| ReaderHtmlBuilder.cs | CSS生成をReaderStyleResolver経由に | +3/-3 |
| ThemeHelper.cs | 完全削除 | -12 |
| **合計** | 8ファイル (新規1, 削除1) | ~100行 |

## 検証方法

- `dotnet build _Apps/App.sln` 成功
- 設定画面でテーマを白→黒→セピアに切り替え → リーダー画面の背景色・文字色が正しく変わること
- 横書き（Label）：DataTriggerで色が反映されること
- 縦書き（WebView）：CSS変数で色が反映されること
- テーマ切り替え後にエピソード遷移 → 色が維持されること
- フォントサイズ・行間変更 → 従来通り動作すること
