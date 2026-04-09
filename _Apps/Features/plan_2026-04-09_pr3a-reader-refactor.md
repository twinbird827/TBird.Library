# PR3a: Reader 画面リファクタ（H2 + H4 縮退版）

作成日: 2026-04-09
更新日: 2026-04-09（レビュー反映 — H4 を縮退し JS 経路を PR3b に分離）
対象ブランチ: `app-novelviewer` から派生する作業ブランチ（例: `feature/pr3a-reader-refactor-20260409`）
前提: PR2（plan_2026-04-08_pr2-data-performance.md）マージ済み
関連:
- 全課題マスター一覧は [audit_2026-04-08_apps-refactor.md](audit_2026-04-08_apps-refactor.md) を参照
- 本 PR から分離したライブ反映経路は [plan_2026-04-09_pr3b-reader-live-css.md](plan_2026-04-09_pr3b-reader-live-css.md) を参照

このドキュメントは **追加調査なしで別セッションが実装可能** なレベルを目標に書かれている。

---

## 0. 目的とスコープ

Reader（縦書き WebView）画面の以下 2 点を改善する。

- **H2**: `ReaderPage.xaml.cs` のコードビハインドで ViewModel.PropertyChanged を購読し `WebView.Source` を命令的に差し替えている箇所を、カスタムコントロール（`ReaderWebView`）による XAML バインドに置き換える。
- **H4（縮退版）**: `ReaderHtmlBuilder` が CSS 値（フォントサイズ・行間・配色）を文字列補間で焼き込んでいるのを **CSS カスタムプロパティ（CSS 変数）に切り出す**。同時に `ReaderHtmlBuilder` 内の配色テーブル重複を `ThemeHelper` に一本化する。

### 本 PR では触らない（次 PR に分離）

レビューで「本 PR では動作確認不能なデッドコード」と指摘された以下は **[plan_2026-04-09_pr3b-reader-live-css.md](plan_2026-04-09_pr3b-reader-live-css.md)** に切り出し、設定画面からのライブ反映 UI を実装する PR と同時に導入する:

- `ReaderWebView.CssVariables` Bindable + `EvaluateJavaScriptAsync` による実行時差し替え
- `_htmlLoaded` / `_pendingCss` 遅延適用フラグ
- `ReaderViewModel.ReaderCss` ObservableProperty と `partial void OnFontSizeChanged` 等 4 本
- `UpdateCssStateIfReady` ヘルパ

理由: 現状の `ReaderViewModel` には `FontSize` / `BackgroundColor` 等を**再代入する経路が存在しない**（`LoadSettingsAsync` からの初期化のみ）。上記コードは本 PR では 1 度も発火せず、`ApplyCssAsync` が壊れていてもマージ通過してしまう。

### 本 PR で残す H4 の価値
- **スタイルとコンテンツの分離**: `ReaderHtmlBuilder` は CSS 変数初期値付きの HTML を生成するだけになり、純粋関数化してテスト容易化
- **`ThemeHelper` 重複解消**: `ReaderHtmlBuilder` 内の `(bg, fg)` switch を削除
- **次 PR の土台**: HTML 側に `:root{...}` 定義が入っていれば、後から JS で `--reader-fs` 等を書き換えられる構造が整う

本 PR のスコープは Reader 画面周辺に閉じる。`OnScrolled` / `OnWebViewNavigating` 等の View イベントハンドラ（コードビハインド残存分）は**触らない**。

---

## 1. 事前に把握しておくべき事実（調査済み）

### 1.1 現在の `ReaderPage.xaml.cs` の役割（[ReaderPage.xaml.cs](../Views/ReaderPage.xaml.cs)）

コードビハインドは以下 3 つのハンドラを持つ:

| 行 | ハンドラ | 役割 | 本 PR での扱い |
|---|---|---|---|
| `:13` `:16-25` | `OnViewModelPropertyChanged` | `EpisodeHtml` / `IsVerticalWriting` の変化を購読し `VerticalWebView.Source` に HTML を代入 | **削除** |
| `:27-35` | `OnScrolled` | `ScrollView` のスクロール終端を検知して `MarkAsReadCommand` 実行（横書き時） | **温存** |
| `:37-44` | `OnWebViewNavigating` | `lanobe://read-end` URI を傍受して `MarkAsReadCommand` 実行（縦書き時） | **温存** |

`OnScrolled` と `OnWebViewNavigating` は Behavior 化することも可能だが別タスク。本 PR では触らない。

### 1.2 現在の `ReaderPage.xaml` の WebView 定義（[ReaderPage.xaml:43-45](../Views/ReaderPage.xaml#L43-L45)）

```xml
<WebView Grid.Row="1" x:Name="VerticalWebView"
         IsVisible="{Binding IsVerticalWriting}"
         Navigating="OnWebViewNavigating" />
```

`x:Name="VerticalWebView"` は `OnViewModelPropertyChanged` から参照されている（H2 で削除）。`Navigating` イベントは温存するので `x:Name` は削除してよい（コードビハインドからの参照が消えるため）。

### 1.3 現在の `ReaderHtmlBuilder.Build` シグネチャ（[ReaderHtmlBuilder.cs:10](../Helpers/ReaderHtmlBuilder.cs#L10)）

```csharp
public static string Build(string content, double fontSizePx, double lineHeight, int backgroundTheme)
```

- `backgroundTheme` から `(bg, fg)` を switch で引く（`ThemeHelper.GetThemeColors` と**重複**）
- `fontSizePx` / `lineHeight` を `body` の `font-size` / `line-height` に焼き込み
- `body` 末尾に `lanobe://read-end` 発火用 JS が入っている（**温存必須**、`OnWebViewNavigating` の相方）

### 1.4 `ReaderViewModel` の現在の挙動

- `RefreshHtml()` は `EpisodeHtml` プロパティ（`ObservableProperty`）を更新 → 現状はコードビハインドが拾って WebView.Source 更新
- `FontSize`, `LineHeight`, `BackgroundColor`, `TextColor`, `IsVerticalWriting` は `[ObservableProperty]`
- `_backgroundThemeIndex` は `int` フィールド（ObservableProperty ではない）。`LoadSettingsAsync` でのみ代入される

### 1.5 `ThemeHelper.GetThemeColors` との重複（[ThemeHelper.cs:5-10](../Helpers/ThemeHelper.cs#L5-L10)）

`ReaderHtmlBuilder` 内の配色テーブルは `ThemeHelper` と完全に同じ値を保持。マジックナンバー重複。H4 対応のついでに `ReaderHtmlBuilder` 側を `ThemeHelper` に寄せる（配色 switch を削除し、呼び出し元 ViewModel が既に保持している `BackgroundColor`/`TextColor` を Hex に変換して渡す）。

### 1.6 DI 構成（[MauiProgram.cs:55](../MauiProgram.cs#L55)）

`builder.Services.AddTransient<ReaderViewModel>();` — コンストラクタ引数の変更なしで進めるため DI 変更は不要。

---

## 2. 設計：カスタムコントロール `ReaderWebView`

H2 をカバーするために `WebView` を継承したカスタムコントロールを 1 個導入する。Attached Property + Behavior の組み合わせより構造が単純で、責務が 1 クラスに閉じる。

**本 PR 版の `ReaderWebView` は `HtmlSource` 1 つの Bindable しか持たない**。CSS ライブ差し替え用の `CssVariables` Bindable は次 PR で追加する。

### 2.1 公開 BindableProperty

| プロパティ | 型 | 役割 |
|---|---|---|
| `HtmlSource` | `string?` | 値が変化したら `this.Source = new HtmlWebViewSource { Html = value }` を実行 |

### 2.2 内部動作

1. `HtmlSource` が変化 → `Source` を再代入 → WebView 再ロード
2. 空文字/null の場合は何もしない（初期化直後の空値を弾く）

### 2.3 `ReaderCssState` の扱い

CSS 値をまとめて `ReaderHtmlBuilder.Build` に渡すためのパラメータオブジェクトとして `ReaderCssState` record を導入する。**ViewModel 内部でビルダー呼び出し時に生成する局所的な DTO** であり、BindableProperty や ObservableProperty にはしない。

```csharp
namespace LanobeReader.Helpers;

public sealed record ReaderCssState(
    double FontSizePx,
    double LineHeight,
    string BackgroundHex, // "#RRGGBB"
    string ForegroundHex);
```

配置先は `Helpers/`（`ReaderHtmlBuilder` と同じレイヤ）。`Controls/` 配下にすると ViewModel → View の参照が発生しレイヤ逆転するため避ける。

---

## 3. 変更対象ファイル一覧

| 種類 | パス |
|---|---|
| 新規 | `_Apps/Controls/ReaderWebView.cs` |
| 新規 | `_Apps/Helpers/ReaderCssState.cs` |
| 変更 | `_Apps/Helpers/ReaderHtmlBuilder.cs` |
| 変更 | `_Apps/ViewModels/ReaderViewModel.cs` |
| 変更 | `_Apps/Views/ReaderPage.xaml` |
| 変更 | `_Apps/Views/ReaderPage.xaml.cs` |

**触らない**: MauiProgram.cs、Converters、Styles、他 VM、他 Service、Models、DB。

---

## 4. 実装詳細

### 4.1 `_Apps/Helpers/ReaderCssState.cs`（新規）

```csharp
namespace LanobeReader.Helpers;

/// <summary>
/// ReaderHtmlBuilder に渡す CSS カスタムプロパティ値のスナップショット。
/// 将来、ReaderWebView から実行時差し替え用 Bindable としても使えるよう
/// record の value equality を採用している。
/// </summary>
public sealed record ReaderCssState(
    double FontSizePx,
    double LineHeight,
    string BackgroundHex,
    string ForegroundHex);
```

### 4.2 `_Apps/Controls/ReaderWebView.cs`（新規）

```csharp
namespace LanobeReader.Controls;

/// <summary>
/// Reader 画面の縦書き表示用 WebView。
/// HtmlSource (string) を bind すると HTML を再ロードする。
/// プロパティ変更通知は UI スレッドからのみ発生する前提で実装している
/// （MAUI の BindableProperty 既定動作）。
/// 将来、CSS 変数のライブ差し替え用 CssVariables Bindable を追加する予定
/// （plan_2026-04-09_pr3b-reader-live-css.md）。
/// </summary>
public sealed class ReaderWebView : WebView
{
    public static readonly BindableProperty HtmlSourceProperty = BindableProperty.Create(
        nameof(HtmlSource),
        typeof(string),
        typeof(ReaderWebView),
        default(string),
        propertyChanged: OnHtmlSourceChanged);

    public string? HtmlSource
    {
        get => (string?)GetValue(HtmlSourceProperty);
        set => SetValue(HtmlSourceProperty, value);
    }

    private static void OnHtmlSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ReaderWebView)bindable;
        var html = newValue as string;
        if (string.IsNullOrEmpty(html))
        {
            return;
        }
        self.Source = new HtmlWebViewSource { Html = html };
    }
}
```

### 4.3 `_Apps/Helpers/ReaderHtmlBuilder.cs`（全面書き換え）

```csharp
using System.Globalization;
using System.Text;

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
        var sb = new StringBuilder(content.Length + 1024);

        sb.Append("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<style>");
        sb.Append(":root{");
        sb.Append($"--reader-fs:{state.FontSizePx.ToString("0.##", inv)}px;");
        sb.Append($"--reader-lh:{state.LineHeight.ToString("0.##", inv)};");
        sb.Append($"--reader-bg:{state.BackgroundHex};");
        sb.Append($"--reader-fg:{state.ForegroundHex};");
        sb.Append("}");
        sb.Append("html,body{margin:0;padding:0;height:100%;}");
        sb.Append("body{");
        sb.Append("background:var(--reader-bg);color:var(--reader-fg);");
        sb.Append("font-family:serif;");
        sb.Append("writing-mode:vertical-rl;-webkit-writing-mode:vertical-rl;");
        sb.Append("font-size:var(--reader-fs);");
        sb.Append("line-height:var(--reader-lh);");
        sb.Append("padding:16px;box-sizing:border-box;overflow-x:auto;overflow-y:hidden;");
        sb.Append("-webkit-tap-highlight-color:transparent;");
        sb.Append("}");
        sb.Append("p{margin:0 0 1em 0;text-indent:1em;}");
        sb.Append("</style></head><body>");

        foreach (var line in content.Split('\n'))
        {
            sb.Append("<p>");
            sb.Append(System.Net.WebUtility.HtmlEncode(line));
            sb.Append("</p>");
        }

        // 既存の read-end 検知 JS（lanobe://read-end URI 発火）。
        // OnWebViewNavigating 側と連動しているため変更禁止。
        sb.Append("<script>");
        sb.Append("(function(){var fired=false;function check(){if(fired)return;");
        sb.Append("var el=document.scrollingElement||document.documentElement;");
        sb.Append("var maxNeg=-(el.scrollWidth-el.clientWidth);");
        sb.Append("if(el.scrollLeft<=maxNeg+10){fired=true;location.href='lanobe://read-end';}}");
        sb.Append("window.addEventListener('scroll',check,{passive:true});setTimeout(check,100);})();");
        sb.Append("</script>");
        sb.Append("</body></html>");

        return sb.ToString();
    }
}
```

**変更点サマリ**:
- シグネチャ `Build(string content, double fontSizePx, double lineHeight, int backgroundTheme)` → `Build(string content, ReaderCssState state)`
- 配色テーブル削除（`ThemeHelper` に一本化 — 呼び出し元で `ReaderViewModel.BackgroundColor`/`TextColor` を Hex に変換して渡す）
- CSS 値は全て `var(--reader-*)` 参照、初期値は `:root` ブロックに集約
- `lanobe://read-end` 発火 JS は**完全温存**

### 4.4 `_Apps/ViewModels/ReaderViewModel.cs`（部分変更）

using は追加不要（`ReaderCssState` は `LanobeReader.Helpers` 配下で、既に `using LanobeReader.Helpers;` 済み）。

#### 4.4.1 `RefreshHtml` を新シグネチャ対応に変更

**Before**:
```csharp
private void RefreshHtml()
{
    EpisodeHtml = ReaderHtmlBuilder.Build(EpisodeContent, FontSize, LineHeight, _backgroundThemeIndex);
}
```

**After**:
```csharp
private void RefreshHtml()
{
    var state = new ReaderCssState(
        FontSizePx: FontSize,
        LineHeight: LineHeight,
        BackgroundHex: ColorToHex(BackgroundColor),
        ForegroundHex: ColorToHex(TextColor));
    EpisodeHtml = ReaderHtmlBuilder.Build(EpisodeContent, state);
}

private static string ColorToHex(Color c) =>
    $"#{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}";
```

> **注**: `Color.ToHex()` は MAUI バージョンにより `#RRGGBB` / `#RRGGBBAA` の返却形式が変わりうるため、Alpha なし前提で自前フォーマットする。`ThemeHelper.GetThemeColors` が返す 3 色はいずれも不透明のため問題なし。

#### 4.4.2 `_backgroundThemeIndex` フィールドの扱い

`RefreshHtml` 変更後は直接参照しなくなるが、`LoadSettingsAsync` 内で `ThemeHelper.GetThemeColors(_backgroundThemeIndex)` の引数としてまだ使われるので**温存**。

### 4.5 `_Apps/Views/ReaderPage.xaml`（差分）

#### 4.5.1 名前空間追加

`ContentPage` ルート要素に:
```xml
xmlns:controls="clr-namespace:LanobeReader.Controls"
```

#### 4.5.2 WebView の置き換え

**Before**:
```xml
<WebView Grid.Row="1" x:Name="VerticalWebView"
         IsVisible="{Binding IsVerticalWriting}"
         Navigating="OnWebViewNavigating" />
```

**After**:
```xml
<controls:ReaderWebView Grid.Row="1"
                        IsVisible="{Binding IsVerticalWriting}"
                        HtmlSource="{Binding EpisodeHtml}"
                        Navigating="OnWebViewNavigating" />
```

`x:Name` はコードビハインドから参照されなくなるため削除。

### 4.6 `_Apps/Views/ReaderPage.xaml.cs`（差分）

#### 4.6.1 `_viewModel` フィールドと PropertyChanged 購読を削除

**Before**:
```csharp
public partial class ReaderPage : ContentPage
{
    private readonly ReaderViewModel _viewModel;

    public ReaderPage(ReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReaderViewModel.EpisodeHtml) or nameof(ReaderViewModel.IsVerticalWriting))
        {
            if (_viewModel.IsVerticalWriting && !string.IsNullOrEmpty(_viewModel.EpisodeHtml))
            {
                VerticalWebView.Source = new HtmlWebViewSource { Html = _viewModel.EpisodeHtml };
            }
        }
    }

    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView) return;
        if (scrollView.ScrollY + scrollView.Height >= scrollView.ContentSize.Height - 10)
        {
            await _viewModel.MarkAsReadCommand.ExecuteAsync(null);
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("lanobe://read-end", StringComparison.OrdinalIgnoreCase) == true)
        {
            e.Cancel = true;
            await _viewModel.MarkAsReadCommand.ExecuteAsync(null);
        }
    }
}
```

**After**:
```csharp
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class ReaderPage : ContentPage
{
    public ReaderPage(ReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView) return;
        if (scrollView.ScrollY + scrollView.Height >= scrollView.ContentSize.Height - 10)
        {
            if (BindingContext is ReaderViewModel vm)
            {
                await vm.MarkAsReadCommand.ExecuteAsync(null);
            }
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("lanobe://read-end", StringComparison.OrdinalIgnoreCase) == true)
        {
            e.Cancel = true;
            if (BindingContext is ReaderViewModel vm)
            {
                await vm.MarkAsReadCommand.ExecuteAsync(null);
            }
        }
    }
}
```

**変更点**:
- `_viewModel` フィールド削除（`BindingContext` 経由で取得）
- `OnViewModelPropertyChanged` 完全削除
- コンストラクタの PropertyChanged 購読削除
- `VerticalWebView` 参照削除

---

## 5. ビルド・動作確認手順

### 5.1 ビルド
```bash
dotnet build /c/Work/Github/TBird.Library/_Apps/App.sln --no-restore
```
- 警告ゼロ
- `ReaderHtmlBuilder.Build` のシグネチャ変更に伴う呼び出し元コンパイルエラーが `ReaderViewModel.RefreshHtml` 以外にないこと（Grep `ReaderHtmlBuilder` で事前確認）

### 5.2 手動チェックリスト

#### H2 検証（コードビハインド除去）
- [ ] 縦書き設定 OFF の状態で小説を開く → 横書きで表示される
- [ ] 縦書き設定 ON の状態で小説を開く → 縦書きで表示される
- [ ] 設定画面で縦書きを OFF→ON 切り替え → Reader 画面で縦書きに切り替わる
- [ ] 次の話へ遷移 → 縦書き WebView に新しい本文が表示される
- [ ] 縦書き状態で最後までスクロール → `lanobe://read-end` 発火 → 既読化される
- [ ] 横書き状態で最後までスクロール → `OnScrolled` 発火 → 既読化される
- [ ] ★（お気に入り）ボタンが機能する（他 Binding が壊れていないことの確認）
- [ ] **縦書き ON → OFF → 別エピソード遷移 → 再度 ON** で正しく新エピソードが縦書き表示される（古い HTML が残らない）

#### H4 検証（CSS 変数化）
- [ ] 縦書き表示で既定テーマ（白背景）の配色が従来と同じ
- [ ] 縦書き表示でダークテーマ（`#121212` / `#E0E0E0`）が従来と同じ
- [ ] 縦書き表示でセピアテーマ（`#F5E6C8` / `#3E2C1C`）が従来と同じ
- [ ] 縦書き表示のフォントサイズ・行間が設定値どおり
- [ ] `ReaderHtmlBuilder.Build` の戻り値をデバッガのウォッチで検査し、`<style>:root{--reader-fs:...;--reader-lh:...;--reader-bg:...;--reader-fg:...}` が含まれていること
- [ ] `body` 内の CSS 値が `var(--reader-*)` 参照になっていること

#### 回帰確認
- [ ] 小説一覧 → 詳細 → 縦書き読書 → 目次 → 前/次 → 既読化 まで動作
- [ ] オフライン状態でキャッシュありのエピソードを開ける
- [ ] オフライン状態でキャッシュなしのエピソードを開くとエラーダイアログが出る

### 5.3 パフォーマンス

H4（縮退版）に直接的なパフォーマンス効果はない。計測不要。

---

## 6. リスク一覧と対策

| # | リスク | 発生箇所 | 対策 |
|---|---|---|---|
| R1 | `HtmlSource` 空文字で初期化時に WebView が白紙のまま | `ReaderWebView.OnHtmlSourceChanged` | 空文字/null で早期 return。`LoadEpisodeAsync` 完了時に `EpisodeHtml` が確定し、その時点でバインドが発火する |
| R2 | `_Apps/Controls/` フォルダが存在しない | 新規ファイル配置 | `Write` ツールは存在しないフォルダを自動作成する。問題なし |
| R3 | `OnWebViewNavigating` ハンドラが `ReaderWebView` 派生型でも正しく動く | `ReaderPage.xaml` | `WebView.Navigating` イベントは継承先でも同じく発火する。MAUI 標準動作 |
| R4 | PropertyChanged 購読を削除したことで `IsVerticalWriting` の視覚切替が効かなくなる | `ReaderPage.xaml` | `WebView` 側は `IsVisible="{Binding IsVerticalWriting}"` で表示/非表示が切り替わるため問題なし。旧ハンドラは WebView 表示切替ではなく「Source 再代入」が主目的だった |
| R5 | `Color.ToHex()` が `#AARRGGBB` / `#RRGGBBAA` を返すバージョン差で色が壊れる | `ColorToHex` | `ToHex()` は使わず `$"#{R:X2}{G:X2}{B:X2}"` で自前フォーマット |

### 事前に走らせる Grep（実装セッション開始時）

```text
pattern: ReaderHtmlBuilder\.Build
path:    c:\Work\Github\TBird.Library\_Apps
期待:    ReaderViewModel.cs の 1 箇所のみ
```
```text
pattern: VerticalWebView
path:    c:\Work\Github\TBird.Library\_Apps
期待:    ReaderPage.xaml / ReaderPage.xaml.cs のみ（本 PR で両方から除去）
```

---

## 7. コミット分割方針

2 コミットに分割する。各コミット後にビルドが通ること。

1. **`refactor(Reader): extract CSS custom properties and unify theme (H4)`**
   - `_Apps/Helpers/ReaderCssState.cs` 新規
   - `_Apps/Helpers/ReaderHtmlBuilder.cs` 書き換え（配色テーブル削除 → `ThemeHelper` 一本化、CSS 変数化）
   - `_Apps/ViewModels/ReaderViewModel.cs` の `RefreshHtml` 書き換え、`ColorToHex` 追加
   - この時点で XAML は旧 `WebView` のまま、コードビハインドも旧 `OnViewModelPropertyChanged` 経由で Source 更新が動き続ける → ビルド・動作とも既存と同等

2. **`refactor(ReaderPage): introduce ReaderWebView and remove PropertyChanged codebehind (H2)`**
   - `_Apps/Controls/ReaderWebView.cs` 新規
   - `_Apps/Views/ReaderPage.xaml` の WebView を `controls:ReaderWebView` に差替、`HtmlSource` バインド追加、`x:Name` 削除
   - `_Apps/Views/ReaderPage.xaml.cs` から `_viewModel` フィールド・PropertyChanged 購読・`OnViewModelPropertyChanged` 削除、残ハンドラは `BindingContext` 経由に
   - 手動チェックリスト §5.2 全項目を実施

---

## 8. スコープ外（やらないこと）

- **CSS 変数のライブ差し替え経路**（`CssVariables` Bindable / `EvaluateJavaScriptAsync` / `_pendingCss` / `partial void OnFontSizeChanged` 等 / `ReaderCss` ObservableProperty）
  → [plan_2026-04-09_pr3b-reader-live-css.md](plan_2026-04-09_pr3b-reader-live-css.md) で扱う
- `OnScrolled` / `OnWebViewNavigating` の Behavior 化（L2 相当）
- 設定画面からの Reader ライブ反映 UI（別 PR）
- `ReaderViewModel._backgroundThemeIndex` の `ObservableProperty` 化
- `Helpers/ReaderHtmlBuilder` のテスト追加（テスト基盤なし）
- `EpisodeContent.Split('\n')` の CRLF 対応（既存バグ、別 PR）
- 他の H / M / L 課題（[audit_2026-04-08_apps-refactor.md](audit_2026-04-08_apps-refactor.md) 参照）

---

## 9. 完了条件（Definition of Done）

1. 変更/新規は §3 の 6 ファイルのみ（`git diff --stat` で確認）
2. `dotnet build` が警告ゼロ
3. §5.2 手動チェックリスト全通過（特に 3 テーマの配色・縦書きスクロール既読化・縦書き ON→OFF→別エピソード→ON 遷移）
4. 2 コミット構成
5. PR 本文に本ファイル（`_Apps/Features/plan_2026-04-09_pr3a-reader-refactor.md`）と後続プラン（`plan_2026-04-09_pr3b-reader-live-css.md`）へのリンクを明記し、「H4 は CSS 変数化＋ThemeHelper 統合までの縮退版。ライブ反映経路は PR3b で実装」と書く
6. ベースブランチは `app-novelviewer`
