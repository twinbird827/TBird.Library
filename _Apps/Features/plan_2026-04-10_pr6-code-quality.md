# PR6: コード品質改善（M1部分 + M2 + L5 + L1）

作成日: 2026-04-10
対象: `_Apps` — 定数化、SQL明示化、スタイル統一、Converter統合、カラー定数化
前提: PR5マージ後に実施

## Context

PR1-PR5で機能改善は完了。残る非機能課題（マジックナンバー、SELECT *、インラインスタイル、Converter乱立、ハードコードカラー）をまとめて対応し、コードの保守性を底上げする。

## 変更ファイル一覧

1. `_Apps/Helpers/SettingsKeys.cs` — デフォルト値const追加
2. `_Apps/ViewModels/SettingsViewModel.cs` — リテラル→const参照
3. `_Apps/ViewModels/ReaderViewModel.cs` — リテラル→const参照
4. `_Apps/Services/Background/BackgroundJobQueue.cs` — クラス内const化
5. `_Apps/Services/Database/NovelRepository.cs` — SELECT列明示化
6. `_Apps/Services/Database/EpisodeRepository.cs` — SELECT列明示化
7. `_Apps/Resources/Styles/Colors.xaml` — セマンティックカラー追加
8. `_Apps/Resources/Styles/Converters.xaml` — BoolToColorConverter登録差し替え
9. `_Apps/Resources/Styles/Styles.xaml` — 3スタイル追加
10. `_Apps/Converters/BoolToColorConverter.cs` — **新規**（BoolToGold + BoolToGray統合）
11. `_Apps/Converters/BoolToGoldConverter.cs` — **削除**
12. `_Apps/Converters/BoolToGrayConverter.cs` — **削除**
13. `_Apps/Views/NovelListPage.xaml` — インライン→StaticResource
14. `_Apps/Views/EpisodeListPage.xaml` — インライン→StaticResource
15. `_Apps/Views/SearchPage.xaml` — インライン→StaticResource
16. `_Apps/Views/SettingsPage.xaml` — インライン→StaticResource
17. `_Apps/Views/ReaderPage.xaml` — インライン→StaticResource

---

## M2 — マジックナンバー定数化

### M2-a: SettingsKeys.cs にデフォルト値const追加

```csharp
// 既存のstring constの後に追加
public const int DEFAULT_CACHE_MONTHS = 3;
public const int DEFAULT_UPDATE_INTERVAL_HOURS = 6;
public const int DEFAULT_FONT_SIZE_SP = 16;
public const int DEFAULT_BACKGROUND_THEME = 0;
public const int DEFAULT_LINE_SPACING = 1;
public const int DEFAULT_EPISODES_PER_PAGE = 50;
public const int DEFAULT_PREFETCH_ENABLED = 1;
public const int DEFAULT_REQUEST_DELAY_MS = 800;
public const int DEFAULT_VERTICAL_WRITING = 0;
public const int MIN_REQUEST_DELAY_MS = 500;
public const int MAX_REQUEST_DELAY_MS = 2000;
```

### SettingsViewModel.cs — フィールド初期値とGetIntValueAsync引数を置換

フィールド初期値（6箇所）:
- `_cacheMonths = 3` → `_cacheMonths = SettingsKeys.DEFAULT_CACHE_MONTHS`
- `_updateIntervalHours = 6` → `_updateIntervalHours = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS`
- `_fontSizeSp = 16` → `_fontSizeSp = SettingsKeys.DEFAULT_FONT_SIZE_SP`
- `_lineSpacing = 1` → `_lineSpacing = SettingsKeys.DEFAULT_LINE_SPACING`
- `_episodesPerPage = 50` → `_episodesPerPage = SettingsKeys.DEFAULT_EPISODES_PER_PAGE`
- `_requestDelayMs = 800` → `_requestDelayMs = SettingsKeys.DEFAULT_REQUEST_DELAY_MS`

注: `_prefetchEnabled = true` はbool型のためint constに直接置換しない。

GetIntValueAsyncの第2引数（9箇所、lines 51-59）:
- 各`GetIntValueAsync(SettingsKeys.XXX, リテラル)` → `GetIntValueAsync(SettingsKeys.XXX, SettingsKeys.DEFAULT_XXX)`

Clamp引数（line 87）:
- `Math.Clamp(value, 500, 2000)` → `Math.Clamp(value, SettingsKeys.MIN_REQUEST_DELAY_MS, SettingsKeys.MAX_REQUEST_DELAY_MS)`

### ReaderViewModel.cs — LoadSettingsAsync内のGetIntValueAsync引数（4箇所、lines 130-133）

- `GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, 16)` → `..., SettingsKeys.DEFAULT_FONT_SIZE_SP)`
- `GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, 0)` → `..., SettingsKeys.DEFAULT_BACKGROUND_THEME)`
- `GetIntValueAsync(SettingsKeys.LINE_SPACING, 1)` → `..., SettingsKeys.DEFAULT_LINE_SPACING)`
- `GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, 0)` → `..., SettingsKeys.DEFAULT_VERTICAL_WRITING)`

注: フィールド初期値 `_fontSize = 16`, `_lineHeight = 1.7`, `_backgroundColor`, `_textColor` は**置換しない**。CSS値/Color型であり設定インデックスとはセマンティクスが異なる。また `_backgroundColor`/`_textColor` はPR7で廃止予定。

### M2-b: BackgroundJobQueue.cs — クラス内const化

```csharp
private const int BatchCooldownThreshold = 200;
private const int CooldownDelayMs = 5000;
private const int MaxConsecutiveFailures = 5;
```

本体の`200`, `5000`, `5`を上記constに置換。

### M2 対象外（理由）
- NarouApiService / KakuyomuApiService: URLは既にクラス冒頭フィールド。タイムアウトはエンドポイントごとに意図的に異なる値→統一constは不適切
- SearchViewModel `TimeSpan.FromSeconds(30)`: PR5でcatchを追加するため競合回避
- ReaderHtmlBuilder: CSS実装詳細。PR3でCSS変数化済み

---

## M2-c: Colors.xaml — セマンティックカラー定数追加

### Colors.xaml に追加（既存Gray950の後に）

```xml
<!-- Semantic colors -->
<Color x:Key="FavoriteGold">#FFC107</Color>
<Color x:Key="UnreadBadge">#FF5722</Color>
<Color x:Key="CachedGreen">#4CAF50</Color>
<Color x:Key="DestructiveRed">#F44336</Color>
<Color x:Key="Overlay">#80000000</Color>
```

注: `#6200EE`は既にPrimaryとして定義済み → 参照を`{StaticResource Primary}`に修正するのみ。

### XAML置換（7箇所）

**NovelListPage.xaml**:
- line 34: `BackgroundColor="#FFC107"` → `BackgroundColor="{StaticResource FavoriteGold}"`
- line 64: `BackgroundColor="#6200EE"` → `BackgroundColor="{StaticResource Primary}"`
- line 82: `BackgroundColor="#FF5722"` → `BackgroundColor="{StaticResource UnreadBadge}"`

**EpisodeListPage.xaml**:
- line 61: `TextColor="#4CAF50"` → `TextColor="{StaticResource CachedGreen}"`

**SettingsPage.xaml**:
- line 25: `BackgroundColor="#F44336"` → `BackgroundColor="{StaticResource DestructiveRed}"`

**ReaderPage.xaml**:
- line 15: `BackgroundColor="#80000000"` → `BackgroundColor="{StaticResource Overlay}"`
- line 51: `BackgroundColor="#80000000"` → `BackgroundColor="{StaticResource Overlay}"`

---

## M2-d: BoolToColorConverter統合（M1部分対応）

### 新規: `_Apps/Converters/BoolToColorConverter.cs`

```csharp
using System.Globalization;

namespace LanobeReader.Converters;

public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.Black;
    public Color FalseColor { get; set; } = Colors.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

### 削除: `BoolToGoldConverter.cs`, `BoolToGrayConverter.cs`

### Converters.xaml 変更（lines 9-10）

```xml
<!-- Before -->
<converters:BoolToGrayConverter x:Key="BoolToGrayConverter" />
<converters:BoolToGoldConverter x:Key="BoolToGoldConverter" />

<!-- After — x:Keyを維持して後方互換。Colors.xamlのリソースを参照 -->
<converters:BoolToColorConverter x:Key="BoolToGrayConverter"
    TrueColor="Gray" FalseColor="Black" />
<converters:BoolToColorConverter x:Key="BoolToGoldConverter"
    TrueColor="{StaticResource FavoriteGold}" FalseColor="{StaticResource Gray200}" />
```

既存XAML参照（BoolToGoldConverter×3箇所、BoolToGrayConverter×2箇所）はx:Key同一のため**変更不要**。

### C#側カラー（対象外）
- **ThemeHelper.cs**: PR7でGetThemeColors削除予定 → PR6では触らない
- **ReaderViewModel.cs**: `#FFFFFF`/`#212121`はPR7で廃止予定 → PR6では触らない

---

## L5 — SELECT * 明示列化

### NovelRepository.cs — 2箇所

**1. unread_descケース（lines 40-43）**:
```csharp
// Before
"SELECT n.* FROM novels n " +
"ORDER BY ..."

// After（GetAllWithUnreadCountAsync lines 55-77の列挙パターン流用）
"SELECT n.id, n.site_type, n.novel_id, n.title, n.author, " +
"n.total_episodes, n.is_completed, n.last_updated_at, " +
"n.registered_at, n.has_unconfirmed_update, n.has_check_error, " +
"n.is_favorite, n.favorited_at " +
"FROM novels n " +
"ORDER BY ..."
```

**2. favorite_firstケース（lines 44-46）**:
```csharp
// Before
"SELECT * FROM novels ORDER BY is_favorite DESC, last_updated_at DESC"

// After
"SELECT id, site_type, novel_id, title, author, " +
"total_episodes, is_completed, last_updated_at, " +
"registered_at, has_unconfirmed_update, has_check_error, " +
"is_favorite, favorited_at " +
"FROM novels ORDER BY is_favorite DESC, last_updated_at DESC"
```

### EpisodeRepository.cs — 1箇所

**GetPagedByNovelIdAsync（lines 32-34）**:
```csharp
// Before
"SELECT * FROM episodes WHERE novel_id = ? ORDER BY episode_no LIMIT ? OFFSET ?"

// After
"SELECT id, novel_id, episode_no, chapter_name, title, " +
"is_read, read_at, published_at, is_favorite, favorited_at " +
"FROM episodes WHERE novel_id = ? ORDER BY episode_no LIMIT ? OFFSET ?"
```

列名参照: Novel=13列、Episode=10列（Model定義から確認済み）

---

## L1 — Styles.xaml拡張 + インラインスタイル統一

### Styles.xaml — 3スタイル追加（MetaLabelの後、line 54付近）

```xml
<!-- Standard body text size -->
<Style x:Key="BodyLabel" TargetType="Label">
    <Setter Property="FontSize" Value="14" />
</Style>

<!-- Small meta (gray) label - supplementary info -->
<Style x:Key="SmallMetaLabel" TargetType="Label">
    <Setter Property="FontSize" Value="11" />
    <Setter Property="TextColor" Value="Gray" />
</Style>

<!-- Badge label (white on colored background) -->
<Style x:Key="BadgeLabel" TargetType="Label">
    <Setter Property="FontSize" Value="11" />
    <Setter Property="TextColor" Value="White" />
</Style>
```

設計: `BodyLabel`はFontSizeのみ。TextColorを含めないことで、個別ラベルが自由にTextColorを指定可能。

### XAML修正

**SettingsPage.xaml（9箇所）**:
- line 16, 34, 50, 130: `<Label FontSize="14">` → `<Label Style="{StaticResource BodyLabel}">`
- line 64, 80: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`（Margin残す）
- line 98: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`（VerticalOptions残す）
- line 115: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`（Margin残す）
- line 112: `FontSize="11" TextColor="Gray"` → `Style="{StaticResource SmallMetaLabel}"`

**EpisodeListPage.xaml（3箇所）**:
- line 48-50: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`（WidthRequest, TextColor残す）
- line 56-57: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`（TextColor残す）
- line 84-85: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`

**ReaderPage.xaml（1箇所）**:
- line 20-22: `FontSize="14"` → `Style="{StaticResource BodyLabel}"`（TextColor="White"等残す）

**NovelListPage.xaml（3箇所）**:
- line 68: `FontSize="11" TextColor="White"` → `Style="{StaticResource BadgeLabel}"`
- line 87-88: `FontSize="11" TextColor="White"` → `Style="{StaticResource BadgeLabel}"`
- line 90: `FontSize="11" TextColor="Gray"` → `Style="{StaticResource SmallMetaLabel}"`

**SearchPage.xaml（1箇所）**:
- line 95-96: `FontSize="11" TextColor="Gray"` → `Style="{StaticResource SmallMetaLabel}"`

**対象外**（FontSize="14"だがBodyLabel不適用）:
- SearchPage:86（エラーラベル — TextColor="Red"とPadding="16"がセット）
- SearchPage:113（「登録済」— TextColor="Green"のステータスインジケータ）

---

## 変更サマリ

| ファイル | 変更 | 行数 |
|---|---|---|
| SettingsKeys.cs | デフォルト値const 11個追加 | +11 |
| SettingsViewModel.cs | フィールド初期値6 + GetInt9 + Clamp1 | ~16置換 |
| ReaderViewModel.cs | GetInt4箇所 | ~4置換 |
| BackgroundJobQueue.cs | const3個 + リテラル3箇所 | +3/-3 |
| NovelRepository.cs | SELECT列明示化2箇所 | +16/-4 |
| EpisodeRepository.cs | SELECT列明示化1箇所 | +4/-2 |
| Colors.xaml | 5セマンティックカラー追加 | +5 |
| BoolToColorConverter.cs | 新規（統合Converter） | +14 |
| BoolToGoldConverter.cs | 削除 | -14 |
| BoolToGrayConverter.cs | 削除 | -17 |
| Converters.xaml | 登録差し替え | ~2 |
| Styles.xaml | 3スタイル追加 | +15 |
| SettingsPage.xaml | BodyLabel×8, SmallMeta×1, カラー×1 | ~10 |
| EpisodeListPage.xaml | BodyLabel×3, カラー×1 | ~4 |
| ReaderPage.xaml | BodyLabel×1, カラー×2 | ~3 |
| NovelListPage.xaml | BadgeLabel×2, SmallMeta×1, カラー×3 | ~6 |
| SearchPage.xaml | SmallMetaLabel×1 | ~1 |
| **合計** | 17ファイル (新規1, 削除2, 変更14) | ~105行 |

## 検証方法

- `dotnet build _Apps/App.sln` 成功
- 全画面遷移してスタイル崩れがないことを視覚確認（小説リスト→エピソード一覧→リーダー→設定→検索）
- SettingsPage: 各スライダーのデフォルト値が従来と同一（キャッシュ3ヶ月、更新6時間、フォント16sp等）
- NovelListPage: サイトバッジ（白文字）、未読バッジ（白文字）、更新日（灰文字）の見た目が変わらないこと
- SettingsPage: 全ラベルのフォントサイズが従来と同一（14sp）
- EpisodeListPage: エピソード番号・タイトル・ページング表示が崩れないこと
