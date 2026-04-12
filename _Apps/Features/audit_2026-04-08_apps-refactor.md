# _Apps 改善課題一覧（2026-04-08 監査）

作成日: 2026-04-08
対象: `_Apps`（LanobeReader, .NET MAUI Android, app-novelviewer ブランチ）
位置づけ: 個別 PR プラン（`plan_2026-04-08_*.md` 等）から参照される**マスター課題リスト**。各項目は ID で参照する。

構成: ViewModels 7 / Views 5 / Services 12（DB4・API4・Background3・Network1）/ Models 8 / Converters 4 / Helpers 4、総行数 約 3,946 行 / 55 ファイル。
最終更新: 2026-04-12（PR7 完了時点）

---

## High（優先度・高）

### H1. NovelListViewModel の N+1 クエリ ✅
- **箇所**: [NovelListViewModel.cs:51-71](../ViewModels/NovelListViewModel.cs#L51-L71), [EpisodeRepository.cs:43-47](../Services/Database/EpisodeRepository.cs#L43-L47)
- **改善案**: Repository に `GetAllWithUnreadCountAsync` を追加し、LEFT JOIN + GROUP BY で 1 クエリ化。
- **担当 PR**: PR2（完了）

### H2. ReaderPage コードビハインドの MVVM 違反 ✅
- **箇所**: [ReaderPage.xaml.cs:16-24](../Views/ReaderPage.xaml.cs#L16-L24)
- **改善案**: WebView 用の `HtmlSource` AttachedBehavior を作り XAML バインドで完結。
- **担当 PR**: PR3（完了）

### H3. SettingsPage のラジオボタン初期化の命令的処理 ✅
- **箇所**: [SettingsPage.xaml.cs:22-46](../Views/SettingsPage.xaml.cs#L22-L46)
- **改善案**: `RadioButtonGroup.SelectedValue` バインドに統一、コードビハインド全削除。
- **担当 PR**: PR4（完了）

### H4. ReaderViewModel / ReaderHtmlBuilder の全文 HTML 再生成 ✅
- **箇所**: [ReaderHtmlBuilder.cs:10-50](../Helpers/ReaderHtmlBuilder.cs#L10-L50), [ReaderViewModel.cs:130-132](../ViewModels/ReaderViewModel.cs#L130-L132)
- **改善案**: CSS 変数で保持、設定変更時は `webview.Eval` で CSS 変数だけ差し替え。
- **担当 PR**: PR3（完了）

### H5. EpisodeListViewModel のフィルタ＆ページング重複計算 ✅
- **箇所**: [EpisodeListViewModel.cs:125-155](../ViewModels/EpisodeListViewModel.cs#L125-L155)
- **改善案**: `_filteredCache: List<Episode>` を導入、フィルタ条件変化時のみ再構築。
- **担当 PR**: PR2（完了）

### H6. SearchViewModel の逐次実行＆エラー状態書き換え ✅
- **箇所**: [SearchViewModel.cs:115-174](../ViewModels/SearchViewModel.cs#L115-L174)
- **改善案**: `Task.WhenAll` で並列化、結果を `(hits, errors)` の構造体に集約。
- **担当 PR**: PR4（完了）

### H7. BackgroundJobQueue の重複抑止の競合余地 — 対応不要
- **箇所**: [BackgroundJobQueue.cs:51-62](../Services/Background/BackgroundJobQueue.cs#L51-L62)
- **調査結果**: `finally` ブロックで `Remove` 済み。競合余地なし。

### H8. SearchViewModel.ShowResultsAsync の N+1 クエリ ✅
- **箇所**: [SearchViewModel.cs:271-278](../ViewModels/SearchViewModel.cs#L271-L278)
- **改善案**: `GetExistingSiteNovelIdsAsync` で全 Novel の (SiteType, NovelId) を 1 クエリ取得、HashSet で O(1) 判定。
- **担当 PR**: PR5（完了）

---

## Medium（優先度・中）

### M1. Converter 乱立 — 部分対応
- **箇所**: `_Apps/Converters/` 配下
- **対応**: PR6 で `BoolToGoldConverter` + `BoolToGrayConverter` → `BoolToColorConverter`（TrueColor/FalseColor プロパティ）に統合。5→4 ファイルに削減。
- **残り**: HasValue / InverseBool / IntToBool は用途が異なるため統合不要。

### M2. マジックナンバー散在 — 部分対応
- **対応**: PR6 で以下を実施:
  - `SettingsKeys.cs` にデフォルト値 const 11 個追加、SettingsVM/ReaderVM のリテラル置換
  - `BackgroundJobQueue.cs` のバッチ閾値・クールダウン・失敗上限をクラス内 const に抽出
  - `Colors.xaml` にセマンティックカラー 5 色追加、View 7 箇所のハードコード hex を `StaticResource` に置換
  - PR7 で Reader テーマ色 6 色を `Colors.xaml` に追加、`ThemeHelper` のハードコード色を削除
- **残り**: NarouApiService / KakuyomuApiService のタイムアウトはエンドポイントごとに意図的に異なる値のため統一 const 不適切。現状維持。

### M3. App.xaml.cs 初期化の `_ = Task.Run(...)` 散発 — 現状維持
- **箇所**: [App.xaml.cs:60-102](../App.xaml.cs#L60-L102)
- **判断**: 現状の await（必須） → `Task.Run`（非必須）の区分けは暗黙的だが実質的に正しい。各 `Task.Run` にローカル try-catch あり。`IAppInitializer` 抽出は初期化フローが安定している現時点では過剰抽象化。

### M4. NovelCardViewModel と検索結果カードの重複 — 対応不要
- **調査結果**: 検索結果カード VM は独立ファイルとして存在しない。実際の VM 重複なし。

### M5. Repository の EnsureAsync ボイラープレート — 対応不要
- **調査結果**: `EnsureAsync` ラッパーの有無はスタイル差。機能的に正しく動作しており、統一しても効果薄。

### M6. IQueryAttributable の型安全性 — 現状維持
- **箇所**: [ReaderViewModel.cs:96-104](../ViewModels/ReaderViewModel.cs#L96-L104), [EpisodeListViewModel.cs:74-81](../ViewModels/EpisodeListViewModel.cs#L74-L81)
- **判断**: .NET MAUI Shell の制約上 `IDictionary<string, object>` は避けられない。ルート数が少なく（2-3 画面）、型安全ラッパーの ROI が低い。

### M7. FetchRankingAsync / FetchGenreAsync のエラー不可視 ✅
- **箇所**: [SearchViewModel.cs](../ViewModels/SearchViewModel.cs) — `FetchRankingAsync`, `FetchGenreAsync`
- **改善案**: `SearchAsync` と同じ `HasError` + `ErrorMessage` パターンを適用。外側 `catch(Exception)` も追加。
- **担当 PR**: PR5（完了）

---

## Low（優先度・低）

### L1. Styles.xaml の被覆不足 — 部分対応
- **箇所**: [Styles.xaml](../Resources/Styles/Styles.xaml)
- **対応**: PR6 で `BodyLabel`(FontSize=14) / `SmallMetaLabel`(FontSize=11+Gray) / `BadgeLabel`(FontSize=11+White) を追加。Settings×9, EpisodeList×3, Reader×1, NovelList×3, Search×1 = 計 17 箇所のインラインスタイルを StaticResource に置換。
- **残り**: Entry / Picker / CheckBox はデフォルトスタイルで問題が生じていないため現状維持。

### L2. XAML の GestureRecognizer 重複 — 現状維持
- **箇所**: Views/*.xaml
- **判断**: 使用箇所が限定的で、ControlTemplate / AttachedBehavior 化の ROI が低い。

### L3. NovelRepository.GetAllAsync のオーバーロード — 対応不要
- **箇所**: [NovelRepository.cs:17-41](../Services/Database/NovelRepository.cs#L17-L41)
- **判断**: 4 行の変更。単独 PR の価値なし。

### L4. TBirdObject 継承規則準拠チェック — 対応不要
- **調査結果**: TBirdObject 継承規則に違反するサービスは見つからず。

### L5. 生 SQL での `SELECT *` / `SELECT n.*` 使用 ✅
- **箇所**: NovelRepository.cs 2 箇所、EpisodeRepository.cs 1 箇所
- **改善案**: 明示列挙に置換（Novel=13 列、Episode=10 列）。
- **担当 PR**: PR6（完了）

---

## PR 分割状況

| PR | 対応項目 | プランファイル | ステータス |
|---|---|---|---|
| PR1 | Converter 土台整理 | - | ✅ 完了 |
| PR2 | H1, H5 | plan_2026-04-08_pr2-data-performance.md | ✅ 完了 |
| PR3 | H2, H4 | plan_2026-04-09_pr3-reader-refactor.md | ✅ 完了 |
| PR4 | H3, H6 | plan_2026-04-09_pr4-settings-search-refactor.md | ✅ 完了 |
| PR5 | H8, M7 | plan_2026-04-10_pr5-search-optimization.md | ✅ 完了 |
| PR6 | M1(部分), M2(部分), L1(部分), L5 | plan_2026-04-10_pr6-code-quality.md | ✅ 完了 |
| PR7 | テーマ色 MVVM 正規化 | plan_2026-04-10_pr7-reader-theme-mvvm.md | ✅ 完了 |

### 対応不要として閉じた項目

| 項目 | 理由 |
|---|---|
| H7 | `finally` ブロックで `Remove` 済み。競合余地なし |
| M4 | 実際の VM 重複なし |
| M5 | `EnsureAsync` ラッパーの有無はスタイル差。機能的に正しい |
| L3 | 4 行の変更。単独 PR の価値なし |
| L4 | TBirdObject 違反なし |

### 現状維持（必要に迫られたら再検討）

| 項目 | 理由 |
|---|---|
| M1(残り) | HasValue / InverseBool / IntToBool は用途が異なり統合不要 |
| M2(API 側) | タイムアウトはエンドポイントごとに意図的に異なる値 |
| M3 | 初期化フローが安定しており `IAppInitializer` 抽出は過剰抽象化 |
| M6 | Shell 制約上 `IDictionary` は不可避。ルート数が少なく ROI 低 |
| L2 | GestureRecognizer 使用箇所が限定的 |
