# _Apps 改善課題一覧（2026-04-08 監査）

作成日: 2026-04-08
対象: `_Apps`（LanobeReader, .NET MAUI Android, app-novelviewer ブランチ）
位置づけ: 個別 PR プラン（`plan_2026-04-08_*.md` 等）から参照される**マスター課題リスト**。各項目は ID で参照する。

構成: ViewModels 7 / Views 5 / Services 12（DB4・API4・Background3・Network1）/ Models 8 / Converters 5 / Helpers 4、総行数 約 3,946 行 / 55 ファイル。

---

## High（優先度・高）

### H1. NovelListViewModel の N+1 クエリ
- **箇所**: [NovelListViewModel.cs:51-71](../ViewModels/NovelListViewModel.cs#L51-L71), [EpisodeRepository.cs:43-47](../Services/Database/EpisodeRepository.cs#L43-L47)
- **現状**: `GetAllAsync(SortKey)` 後に novel ごとに `CountUnreadByNovelIdAsync` を await ループで呼び出し、SQL が `1 + N` 回発行される。
- **改善案**: Repository に `GetAllWithUnreadCountAsync` を追加し、LEFT JOIN + GROUP BY で 1 クエリ化。
- **担当 PR**: PR2（plan_2026-04-08_pr2-data-performance.md）

### H2. ReaderPage コードビハインドの MVVM 違反
- **箇所**: [ReaderPage.xaml.cs:16-24](../Views/ReaderPage.xaml.cs#L16-L24)
- **現状**: ViewModel の PropertyChanged を直接購読し WebView.Source を命令的に差し替え。テスト困難。
- **改善案**: WebView 用の `HtmlSource` AttachedBehavior を作り XAML バインドで完結。
- **担当 PR**: PR3 予定

### H3. SettingsPage のラジオボタン初期化の命令的処理
- **箇所**: [SettingsPage.xaml.cs:22-46](../Views/SettingsPage.xaml.cs#L22-L46)
- **現状**: `_initialized` フラグ + switch で状態復元、ラジオ変更イベントをコードビハインドで処理。
- **改善案**: VM に `SelectedThemeIndex` を追加、`IntToBoolConverter` + ConverterParameter で XAML バインドに統一、コードビハインド全削除。
- **担当 PR**: PR4 予定

### H4. ReaderViewModel / ReaderHtmlBuilder の全文 HTML 再生成
- **箇所**: [ReaderHtmlBuilder.cs:10-50](../Helpers/ReaderHtmlBuilder.cs#L10-L50), [ReaderViewModel.cs:130-132](../ViewModels/ReaderViewModel.cs#L130-L132)
- **現状**: フォントサイズ・行間・テーマ変更のたびに本文全文を再ビルドし WebView 再ロード。長編で UI ブロック。
- **改善案**: 本文 HTML は 1 回のみ生成、`:root{--font-size:…}` 等の CSS 変数で保持、設定変更時は `webview.Eval` で CSS 変数だけ差し替え。
- **担当 PR**: PR3 予定

### H5. EpisodeListViewModel のフィルタ＆ページング重複計算
- **箇所**: [EpisodeListViewModel.cs:125-155](../ViewModels/EpisodeListViewModel.cs#L125-L155)
- **現状**: `FilteredEpisodes()` が遅延列挙で `RecalcPaging` と `LoadPageAsync` の両方から毎回呼ばれ、同じ LINQ が 2 回実行される。
- **改善案**: `_filteredCache: List<Episode>` を導入、フィルタ条件変化時のみ再構築、ページ送りはキャッシュ参照のみ。
- **担当 PR**: PR2（plan_2026-04-08_pr2-data-performance.md）

### H6. SearchViewModel の逐次実行＆エラー状態書き換え
- **箇所**: [SearchViewModel.cs:115-174](../ViewModels/SearchViewModel.cs#L115-L174)
- **現状**: Narou → Kakuyomu を順次 await。`HasError` が後段で上書きされる可能性。
- **改善案**: `Task.WhenAll` で並列化、結果を `(hits, errors)` の構造体に集約、エラーは改行連結。
- **担当 PR**: PR4 予定

### H8. SearchViewModel.ShowResultsAsync の N+1 クエリ
- **箇所**: [SearchViewModel.cs:271-278](../ViewModels/SearchViewModel.cs#L271-L278)
- **現状**: 検索結果ごとに `_novelRepo.GetBySiteAndNovelIdAsync` を個別 await。結果 N 件に対し SQL が N 回発行される。PR4 の並列化で両サイトのヒットが合算されるため、件数増加 → 体感劣化が顕在化しやすい。
- **改善案**: Repository に `GetBySiteAndNovelIdsAsync(List<(int siteType, string novelId)>)` を追加し、`WHERE (SiteType, NovelId) IN (...)` で 1 クエリ化。
- **担当 PR**: 未定（PR4 スコープ外）

### H7. BackgroundJobQueue の重複抑止の競合余地
- **箇所**: [BackgroundJobQueue.cs:51-62](../Services/Background/BackgroundJobQueue.cs#L51-L62)
- **現状**: HashSet + lock。処理中例外で `_enqueuedEpisodeIds` から Remove されない経路の懸念。
- **改善案**: `finally` での Remove 確実化、`ConcurrentDictionary<long,byte>` 化。
- **担当 PR**: 未定（後続）

---

## Medium（優先度・中）

### M1. Converter 乱立
- **箇所**: `_Apps/Converters/` 配下 5 ファイル（BoolToGold / BoolToGray / HasValue / InverseBool / IntToBool）
- **改善案**: `GenericBoolConverter`（`TrueValue` / `FalseValue` プロパティ）に統合。

### M2. マジックナンバー散在
- **箇所**: `ReaderHtmlBuilder`（色コード）、`NarouApiService`（件数・タイムアウト）、`BackgroundJobQueue`（バッチ間隔）
- **改善案**: `AppConstants.cs` または `SettingsKeys.cs` に集約。配色は `Resources/Styles/Colors.xaml`。

### M3. App.xaml.cs 初期化の `_ = Task.Run(...)` 散発
- **箇所**: [App.xaml.cs:60-102](../App.xaml.cs#L60-L102)
- **改善案**: `IAppInitializer` サービスに集約、必須/非必須で段階分け、失敗時 UI 通知。

### M4. NovelCardViewModel と検索結果カードの重複
- **箇所**: `ViewModels/NovelCardViewModel.cs`, 検索結果カード VM
- **改善案**: `ItemCardViewModelBase` を抽出、差分プロパティのみ派生側で保持。

### M5. Repository の EnsureAsync ボイラープレート
- **箇所**: `_Apps/Services/Database/*Repository.cs` 各メソッド冒頭の `await EnsureAsync()`
- **改善案**: `RepositoryBase` に初期化ゲートを実装、派生側は本体処理のみに。
- **関連**: この整理に合わせて `EpisodeRepository.CountUnreadByNovelIdAsync`（H1 対応後に dead code 化）の削除を検討。

### M7. FetchRankingAsync / FetchGenreAsync のエラー不可視
- **箇所**: [SearchViewModel.cs](../ViewModels/SearchViewModel.cs) — `FetchRankingAsync`, `FetchGenreAsync`
- **現状**: 両メソッドはエラー発生時に `LogHelper.Warn` のみで `HasError` / `ErrorMessage` を設定しない。両サイト失敗時、ユーザーには空結果だけが表示されエラーの存在を認知できない。PR4 の並列化で両サイト同時失敗のケースが起きやすくなる。
- **改善案**: `SearchAsync` と同様に `HasError` + `ErrorMessage` でエラーを UI 表示するか、空結果時に「結果がありません」のフォールバック表示を追加。
- **担当 PR**: 未定（PR4 スコープ外 — UI デザインの検討が必要）

### M6. IQueryAttributable の型安全性
- **箇所**: [ReaderViewModel.cs:96-104](../ViewModels/ReaderViewModel.cs#L96-L104), [EpisodeListViewModel.cs:74-81](../ViewModels/EpisodeListViewModel.cs#L74-L81)
- **改善案**: `record ReaderRouteArgs(...)` 等のラッパー + 拡張メソッドで吸収。

---

## Low（優先度・低）

### L1. Styles.xaml の被覆不足
- **箇所**: [Styles.xaml](../Resources/Styles/Styles.xaml)
- **現状**: Card / Status ラベルはあるが Entry / Picker / CheckBox 未整備。

### L2. XAML の GestureRecognizer 重複
- **箇所**: Views/*.xaml
- **改善案**: ControlTemplate / Attached Behavior で再利用化。

### L3. NovelRepository.GetAllAsync のオーバーロード
- **箇所**: [NovelRepository.cs:17-41](../Services/Database/NovelRepository.cs#L17-L41)
- **改善案**: デフォルト引数で 1 本化。

### L5. 生 SQL での `SELECT *` / `SELECT n.*` 使用
- **箇所**:
  - [NovelRepository.cs:33](../Services/Database/NovelRepository.cs#L33) — `unread_desc` ソートの `SELECT n.* FROM novels n ...`
  - [NovelRepository.cs:37](../Services/Database/NovelRepository.cs#L37) — `favorite_first` ソートの `SELECT * FROM novels ...`
  - [EpisodeRepository.cs:33](../Services/Database/EpisodeRepository.cs#L33) — `GetPagedByNovelIdAsync` の `SELECT * FROM episodes ...`
- **現状**: 列順序依存・将来の列追加時に sqlite-net のマッピングが暗黙的に壊れる恐れがある（エラーにならず値が欠落するため検知が遅れる）。
- **改善案**: PR2 で新設する `GetAllWithUnreadCountAsync` と同じく、`Models/Novel.cs` / `Models/Episode.cs` の `[Column]` 宣言に対応する列を明示列挙する。SQL は長くなるが、スキーマ変更時にコンパイラは助けてくれないので明示の方が安全。
- **担当 PR**: 未定（PR2 のスコープ外 — 既存 `GetAllAsync` 温存方針と矛盾するため混ぜない。`EpisodeRepository.GetPagedByNovelIdAsync` も現時点で呼び出し元は限定的なので後続の Repository 整理 PR でまとめて対応）。

### L4. TBirdObject 継承規則準拠チェック
- **箇所**: `_Apps/Services/` 配下の Dispose が必要なサービス
- **改善案**: `DisposeManagedResource` / `DisposeUnmanagedResource` override 準拠を再確認（CLAUDE.md 共通ルール）。

---

## PR 分割状況

| PR | 対応項目 | プランファイル |
|---|---|---|
| PR1 | Converter 土台整理（完了） | - |
| PR2 | H1, H5（完了） | plan_2026-04-08_pr2-data-performance.md |
| PR3 | H2, H4（完了） | plan_2026-04-09_pr3-reader-refactor.md |
| PR4 | H3, H6（完了） | plan_2026-04-09_pr4-settings-search-refactor.md |
| 未定 | H7, M*, L* | 上記 PR 後に必要性を再評価 |
