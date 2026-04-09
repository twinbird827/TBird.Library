# PR3b: Reader CSS ライブ反映経路（PR3a から分離）

作成日: 2026-04-09
対象ブランチ: `app-novelviewer` から派生する作業ブランチ（例: `feature/pr3b-reader-live-css-YYYYMMDD`）
前提: PR3a（[plan_2026-04-09_pr3a-reader-refactor.md](plan_2026-04-09_pr3a-reader-refactor.md)）マージ済み
関連: [audit_2026-04-08_apps-refactor.md](audit_2026-04-08_apps-refactor.md)

---

## 0. 背景

PR3a のレビュー時、H4 の以下の要素が「本 PR では 1 度も発火せずマージされるデッドコード相当」と指摘されたため、PR3a から切り出して本プランに分離した:

- `ReaderWebView.CssVariables` Bindable + `EvaluateJavaScriptAsync` による `:root` CSS 変数の実行時差し替え
- `_htmlLoaded` / `_pendingCss` 遅延適用フラグ
- `ReaderViewModel.ReaderCss` ObservableProperty
- `partial void OnFontSizeChanged` / `OnLineHeightChanged` / `OnBackgroundColorChanged` / `OnTextColorChanged` の 4 本
- `UpdateCssStateIfReady` ヘルパ

PR3a ではこれらを入れても発火経路が存在しなかった（`ReaderViewModel` に `FontSize` 等を再代入するルートがないため）。本 PR では **「設定画面 → Reader へのライブ反映」UI と同時に導入**し、実際に動作検証可能な状態で着地させる。

## 0.1 PR3a で既に整った土台

- `ReaderHtmlBuilder` は CSS カスタムプロパティ（`--reader-fs` / `--reader-lh` / `--reader-bg` / `--reader-fg`）を `:root` に定義済み
- `body` 内の CSS 値は全て `var(--reader-*)` 参照
- `ReaderCssState` record が `Helpers/` に存在
- `ReaderWebView` カスタムコントロールが存在し `HtmlSource` Bindable を持つ

本 PR はこの土台の上に、JS による変数書き換え経路と ViewModel 側の通知ルートを重ねる。

---

## 1. ゴール

1. Reader 表示中にフォントサイズ・行間・テーマを変更したら、**WebView を再ロードせず**に縦書き表示がその場で更新される
2. エピソード切替（HTML 再ロード）時と設定変更時で挙動を切り替える（前者は HTML 焼き込み、後者は JS 実行）
3. 設定画面から Reader へ変更を伝えるルートが実装される（PR3a 時点では未実装）

---

## 2. 設計

### 2.1 `ReaderWebView` への追加

PR3a 版 `ReaderWebView` に以下を追加する。

#### 新規 BindableProperty

| プロパティ | 型 | 役割 |
|---|---|---|
| `CssVariables` | `ReaderCssState?` | 値が変化したら HTML ロード済みなら JS で `:root` の CSS 変数を更新。未ロードなら Navigated 時に保留適用 |

#### 内部状態

- `bool _htmlLoaded` — `Navigated` 成功で true、`HtmlSource` 再代入直前に false
- `ReaderCssState? _pendingCss` — HTML 未ロード中に受け取った CssVariables 値

#### 動作

1. `HtmlSource` 変化 → `_htmlLoaded = false` → `_pendingCss = null`（古い保留を破棄）→ `Source` 再代入
2. `CssVariables` 変化 → `_htmlLoaded == true` なら `ApplyCssAsync` を fire-and-forget、そうでなければ `_pendingCss = state`
3. `Navigated` ハンドラ: `e.Result == Success` なら `_htmlLoaded = true`、`_pendingCss` があれば適用
4. `ApplyCssAsync` 内の例外は `Debug.WriteLine` でログ出力してから握りつぶす（WebView 破棄後の呼び出し等を想定）

> **PR3a レビュー時の指摘反映**:
> - **代入順序**: `ReaderViewModel.RefreshHtml` は `EpisodeHtml` を先に、`ReaderCss` を後に代入する（古い document への無駄 JS を防ぐ）
> - **pending 破棄**: HtmlSource 再代入時に必ず `_pendingCss = null` する（`Navigated` が失敗したケースで古い保留が永久に残るのを防ぐ）
> - **サイレント catch 禁止**: `ApplyCssAsync` の catch で必ず `Debug.WriteLine`
> - **UI スレッド前提コメント**: クラス冒頭に「プロパティ変更通知は UI スレッドからのみ前提」の説明を残す

### 2.2 `ReaderViewModel` への追加

#### 新規 ObservableProperty

```csharp
[ObservableProperty]
private ReaderCssState? _readerCss;
```

#### 初期化タイミング

`LoadSettingsAsync` 末尾（`IsHorizontal = !IsVerticalWriting;` の直後）で:

```csharp
ReaderCss = BuildCssState();
```

`BuildCssState()` は PR3a で `RefreshHtml` 内にインライン展開されているロジックを private メソッドに切り出したもの:

```csharp
private ReaderCssState BuildCssState() => new ReaderCssState(
    FontSizePx: FontSize,
    LineHeight: LineHeight,
    BackgroundHex: ColorToHex(BackgroundColor),
    ForegroundHex: ColorToHex(TextColor));
```

#### `RefreshHtml` の修正

**PR3a 版**:
```csharp
private void RefreshHtml()
{
    var state = new ReaderCssState(...);
    EpisodeHtml = ReaderHtmlBuilder.Build(EpisodeContent, state);
}
```

**PR3b 版**（代入順序に注意 — EpisodeHtml 先、ReaderCss 後）:
```csharp
private void RefreshHtml()
{
    var state = BuildCssState();
    EpisodeHtml = ReaderHtmlBuilder.Build(EpisodeContent, state);
    ReaderCss = state; // record の value equality により、同値なら通知なし
}
```

#### partial メソッド 4 本追加

`OnIsVerticalWritingChanged` の近くに:

```csharp
partial void OnFontSizeChanged(double value) => UpdateCssStateIfReady();
partial void OnLineHeightChanged(double value) => UpdateCssStateIfReady();
partial void OnBackgroundColorChanged(Color value) => UpdateCssStateIfReady();
partial void OnTextColorChanged(Color value) => UpdateCssStateIfReady();

private void UpdateCssStateIfReady()
{
    // LoadSettingsAsync 内の初期化フェーズ（ReaderCss == null）では個別
    // プロパティが順次変化するたびに CSS state を作り直すのは無駄なので、
    // ReaderCss が既にセットされている場合のみ更新する。
    if (ReaderCss is not null)
    {
        ReaderCss = BuildCssState();
    }
}
```

### 2.3 設定画面からのライブ反映ルート（本 PR の肝）

PR3a 時点では `ReaderViewModel.FontSize` を再代入する UI が存在しない。本 PR で以下のいずれかの形で通知経路を作る（実装セッション開始時に方針確定すること）:

**方針候補 A**: `MessagingCenter` / `WeakReferenceMessenger` による pub/sub
- 設定画面がフォントサイズ等を保存したタイミングで `SettingsChangedMessage` を publish
- `ReaderViewModel` のコンストラクタで subscribe し、受信時に `FontSize`/`LineHeight`/`BackgroundColor`/`TextColor` を再代入

**方針候補 B**: `ReaderPage.OnAppearing` で `LoadSettingsAsync` を再実行
- 設定画面から Reader に戻ってきたタイミングで設定値を再読込
- フォント等の個別プロパティを再代入すれば partial void 経由で `ReaderCss` が更新される
- ただしページ復帰時のみで、設定画面とのサイドバイサイド表示には非対応（本アプリは Shell 遷移のみなので実質問題なし）

**推奨**: 方針 B（実装が単純で既存アーキテクチャに馴染む）。

### 2.4 `ReaderPage.xaml`

PR3a 版に `CssVariables` バインドを追加する:

```xml
<controls:ReaderWebView Grid.Row="1"
                        IsVisible="{Binding IsVerticalWriting}"
                        HtmlSource="{Binding EpisodeHtml}"
                        CssVariables="{Binding ReaderCss}"
                        Navigating="OnWebViewNavigating" />
```

---

## 3. 変更対象ファイル一覧

| 種類 | パス |
|---|---|
| 変更 | `_Apps/Controls/ReaderWebView.cs` |
| 変更 | `_Apps/ViewModels/ReaderViewModel.cs` |
| 変更 | `_Apps/Views/ReaderPage.xaml` |
| 変更 | `_Apps/Views/ReaderPage.xaml.cs`（方針 B の場合 `OnAppearing` 追加） |

`ReaderCssState` / `ReaderHtmlBuilder` は PR3a で最終形になっているため本 PR では触らない。

---

## 4. 実装詳細

### 4.1 `ReaderWebView` 拡張

```csharp
using System.Diagnostics;
using System.Globalization;
using LanobeReader.Helpers;

namespace LanobeReader.Controls;

/// <summary>
/// Reader 画面の縦書き表示用 WebView。
///
/// 本コントロールのプロパティ変更通知は UI スレッドからのみ発生する前提で実装されている
/// （MAUI の BindableProperty 既定動作）。別スレッドから HtmlSource / CssVariables を
/// 書き換える場合は呼び出し側で Dispatcher 経由にすること。
/// </summary>
public sealed class ReaderWebView : WebView
{
    private bool _htmlLoaded;
    private ReaderCssState? _pendingCss;

    public ReaderWebView()
    {
        Navigated += OnNavigated;
    }

    // --- HtmlSource ---

    public static readonly BindableProperty HtmlSourceProperty = BindableProperty.Create(
        nameof(HtmlSource), typeof(string), typeof(ReaderWebView),
        default(string), propertyChanged: OnHtmlSourceChanged);

    public string? HtmlSource
    {
        get => (string?)GetValue(HtmlSourceProperty);
        set => SetValue(HtmlSourceProperty, value);
    }

    private static void OnHtmlSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ReaderWebView)bindable;
        var html = newValue as string;
        if (string.IsNullOrEmpty(html)) return;

        // 新しい HTML をロードする直前に、古い document 向けの保留 CSS を破棄する。
        // Navigated が失敗したケースで _pendingCss が永久に残るのを防ぐ。
        self._htmlLoaded = false;
        self._pendingCss = null;
        self.Source = new HtmlWebViewSource { Html = html };
    }

    // --- CssVariables ---

    public static readonly BindableProperty CssVariablesProperty = BindableProperty.Create(
        nameof(CssVariables), typeof(ReaderCssState), typeof(ReaderWebView),
        default(ReaderCssState), propertyChanged: OnCssVariablesChanged);

    public ReaderCssState? CssVariables
    {
        get => (ReaderCssState?)GetValue(CssVariablesProperty);
        set => SetValue(CssVariablesProperty, value);
    }

    private static void OnCssVariablesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ReaderWebView)bindable;
        if (newValue is not ReaderCssState state) return;

        if (self._htmlLoaded)
        {
            _ = self.ApplyCssAsync(state);
        }
        else
        {
            // HTML 未ロード時は保留。Navigated 成功時にまとめて適用する。
            // 初回は HTML 側の :root{} に同じ値が埋まっているため厳密には不要だが、
            // 後から値が変わった場合の安全網として保持する。
            self._pendingCss = state;
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success) return;
        _htmlLoaded = true;
        if (_pendingCss is not null)
        {
            var s = _pendingCss;
            _pendingCss = null;
            _ = ApplyCssAsync(s);
        }
    }

    private async Task ApplyCssAsync(ReaderCssState state)
    {
        var inv = CultureInfo.InvariantCulture;
        // 変数名は ReaderHtmlBuilder の :root 定義と一致させること
        var js =
            "(function(){var s=document.documentElement.style;" +
            $"s.setProperty('--reader-fs','{state.FontSizePx.ToString("0.##", inv)}px');" +
            $"s.setProperty('--reader-lh','{state.LineHeight.ToString("0.##", inv)}');" +
            $"s.setProperty('--reader-bg','{state.BackgroundHex}');" +
            $"s.setProperty('--reader-fg','{state.ForegroundHex}');" +
            "})();";
        try
        {
            await EvaluateJavaScriptAsync(js);
        }
        catch (Exception ex)
        {
            // WebView 破棄後などに到達する可能性がある。致命的ではないが、
            // 将来のデバッグのためログは必ず残す（サイレント catch にしない）。
            Debug.WriteLine($"[ReaderWebView] ApplyCssAsync failed: {ex}");
        }
    }
}
```

### 4.2 `ReaderViewModel` 変更点

§2.2 参照。要点:
- `ReaderCss` ObservableProperty 追加
- `BuildCssState()` を private メソッドに切り出し
- `LoadSettingsAsync` 末尾で `ReaderCss = BuildCssState();`
- `RefreshHtml` は **EpisodeHtml 先・ReaderCss 後** の順
- `partial void On{FontSize|LineHeight|BackgroundColor|TextColor}Changed` 4 本 + `UpdateCssStateIfReady`

### 4.3 設定画面からの反映（方針 B 採用時）

`ReaderPage.xaml.cs`:

```csharp
protected override void OnAppearing()
{
    base.OnAppearing();
    if (BindingContext is ReaderViewModel vm)
    {
        _ = vm.ReloadSettingsAsync();
    }
}
```

`ReaderViewModel` に public な `ReloadSettingsAsync` を公開（既存 `LoadSettingsAsync` を public 化 or ラップ）:

```csharp
public Task ReloadSettingsAsync() => LoadSettingsAsync();
```

`LoadSettingsAsync` 内で `FontSize = fontSizeSp;` 等の代入が走ると、`ReaderCss != null` なので partial void 経由で `UpdateCssStateIfReady` → `ReaderCss` 更新 → WebView に JS が流れる。

> **注**: `LoadSettingsAsync` では 4 プロパティが順次更新されるため、partial void が 4 回発火して `ReaderCss` も 4 回更新される（うち 3 回は中間状態）。CSS 変数の適用は冪等なので視覚的な問題はないが、気になるなら `LoadSettingsAsync` 内で一時的にフラグを立てて partial void を抑制し、末尾でまとめて `ReaderCss = BuildCssState();` する形にできる。

---

## 5. ビルド・動作確認手順

### 5.1 ビルド
```bash
dotnet build /c/Work/Github/TBird.Library/_Apps/App.sln --no-restore
```

### 5.2 手動チェックリスト

#### ライブ反映の検証（本 PR のコア）
- [ ] 縦書き Reader を開いた状態で設定画面に遷移 → フォントサイズを変更 → Reader に戻る → **WebView をリロードせずに**フォントサイズが変わっている
  - 検証方法: 変更前にスクロール位置を覚えておき、戻った後もスクロール位置が維持されていることを確認（リロードされていれば先頭に戻る）
- [ ] 同様に行間変更が反映される
- [ ] 同様にテーマ（背景色・文字色）変更が反映される
- [ ] 設定を連続で複数回変更しても正しく反映される
- [ ] `Debug.WriteLine` の `ApplyCssAsync failed` ログが出ていない

#### `_pendingCss` 破棄の検証
- [ ] 縦書きで別エピソードに遷移した直後にテーマ変更 → 新エピソードに正しく反映される（古いエピソードへの保留 JS が走らない）

#### PR3a 回帰
- [ ] PR3a の §5.2 全項目が引き続き通る

### 5.3 Navigated 失敗時の挙動確認（任意）

`e.Result != Success` を人為的に再現するのは難しいため、本 PR では `_pendingCss = null` を HtmlSource 再代入時に確実に実行していることをコードレビューで担保する。

---

## 6. リスク一覧と対策

| # | リスク | 対策 |
|---|---|---|
| R1 | `EvaluateJavaScriptAsync` がプラットフォーム（iOS/Android/Windows）で挙動差がある | 手動チェックリストを主要 2 プラットフォーム以上で実行。失敗時は `Debug.WriteLine` ログで検知 |
| R2 | `Navigated` イベントが HTML ロード失敗時に `_htmlLoaded = true` にならず `_pendingCss` が永久保留 | HtmlSource 再代入時に `_pendingCss = null` で破棄する安全網で対処済み |
| R3 | `LoadSettingsAsync` 内で 4 プロパティが順次更新され partial void が 4 回発火 | `ReaderCssState` は record value equality で同値通知を抑制。視覚的問題なし。気になれば一時抑制フラグを導入 |
| R4 | `RefreshHtml` の代入順序を誤ると古い document に無駄 JS が流れる | **EpisodeHtml 先・ReaderCss 後** をコード内コメントで明記 |
| R5 | `Color.ToHex()` のバージョン差問題（PR3a と共通） | PR3a で導入済みの自前フォーマット `ColorToHex` をそのまま使う |

---

## 7. コミット分割方針

1. **`feat(ReaderWebView): add CssVariables bindable with deferred apply`**
   - `ReaderWebView.cs` に `CssVariables` Bindable、`_htmlLoaded` / `_pendingCss`、`ApplyCssAsync` 追加
   - `OnHtmlSourceChanged` に `_pendingCss = null` 追加

2. **`feat(ReaderViewModel): propagate setting changes to ReaderCss`**
   - `ReaderCss` ObservableProperty、`BuildCssState`、partial void 4 本、`UpdateCssStateIfReady` 追加
   - `RefreshHtml` を「EpisodeHtml 先・ReaderCss 後」順に修正
   - `LoadSettingsAsync` 末尾で初期 `ReaderCss` セット
   - `ReloadSettingsAsync` public メソッド追加

3. **`feat(ReaderPage): reload settings on appearing for live CSS update`**
   - `ReaderPage.xaml` に `CssVariables` バインド追加
   - `ReaderPage.xaml.cs` の `OnAppearing` オーバーライドで `ReloadSettingsAsync` 呼び出し
   - 手動チェックリスト §5.2 全項目を実施

---

## 8. スコープ外（やらないこと）

- フォントサイズ等を Reader 画面内の UI（スライダー等）から直接変更する機能（設定画面経由のみ）
- 他の Reader 設定項目（マージン、段組み等）の追加
- `OnScrolled` / `OnWebViewNavigating` の Behavior 化
- `EpisodeContent.Split('\n')` の CRLF 対応
- 他の H / M / L 課題

---

## 9. 完了条件（Definition of Done）

1. §5.2 のライブ反映チェックリスト全通過（特にスクロール位置維持で「リロードされていない」ことを確認）
2. `dotnet build` 警告ゼロ
3. PR3a の手動チェックリストも引き続き通る
4. `ApplyCssAsync` の catch で `Debug.WriteLine` が実装されていること
5. `OnHtmlSourceChanged` で `_pendingCss = null` が実装されていること
6. `RefreshHtml` 内の代入順序が「EpisodeHtml → ReaderCss」になっていること
7. ベースブランチは PR3a マージ後の `app-novelviewer`
