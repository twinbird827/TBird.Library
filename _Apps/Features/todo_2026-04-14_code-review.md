# コードレビュー TODO (2026-04-14)

_Apps フォルダ全57ファイルの徹底レビュー結果。

---

## 🔴 BUG（確定・高確率のバグ）— 12件

- [ ] **B1** `UpdateCheckService.cs:101-106` — `catch` 内 `throw` で foreach 中断。1小説のHTTPエラーで残り全チェックがスキップ＋エラーフラグリセット到達不能
- [ ] **B2** `UpdateCheckWorker.cs:34,39` — `Task.Run().GetAwaiter().GetResult()` のデッドロックリスク。`ListenableWorker` への移行が安全
- [ ] **B3** `UpdateCheckWorker.cs:55-59` — 一時的ネットワーク障害に `Result.InvokeFailure()` → `Result.InvokeRetry()` が適切
- [ ] **B4** `UpdateCheckWorker.cs:23-34` — DB未初期化のまま `CheckAllAsync` 実行。アプリ停止中 Worker 起動でクラッシュ
- [ ] **B5** `NotificationHelper.cs:58` — Android 13+ `POST_NOTIFICATIONS` ランタイム権限チェックなし。通知が黙って無視される
- [ ] **B6** `NotificationHelper.cs:71` — エラー通知ID固定値 `9999`。novel.Id=9999 と衝突、複数エラーは上書き
- [ ] **B7** `NetworkPolicyService.cs:102-116` — `GetStreamAsync` で Stream 返却前にセマフォ解放。レート制限が機能しない
- [ ] **B8** `NarouApiService.cs:BuildRtype` — `RankingPeriod.Yearly`/`All` が switch 未処理→デフォルト Daily 扱い
- [ ] **B9** `KakuyomuApiService.cs:205-221` — `FetchEpisodeContentAsync` が毎回TOCページをフェッチ。100話で100回TOC取得
- [ ] **B10** `BackgroundJobQueue.cs:76-78` — 新 `CancellationTokenSource` 作成時に前回の `Dispose` なし。CTSリーク
- [ ] **B11** `SettingsViewModel.cs:49-59` — `InitializeAsync` でDB読込値をプロパティ代入→ `OnXxxChanged` 発火→同じ値をDB書き戻し（9項目）
- [ ] **B12** `NovelListViewModel.cs:78-82` — `OnSortKeyChanged` + 行58 で初期化時に `LoadNovelsAsync` が2回走る

---

## 🟡 RISK（潜在的リスク・脆弱なコード）— 14件

- [ ] **R1** `NovelRepository.cs:167-178` — `DeleteAsync` がトランザクションなしで3段階カスケード削除。途中失敗でデータ不整合
- [ ] **R2** `NetworkPolicyService.cs:19-29` — `Dictionary<SiteType, DateTime>` がスレッドセーフでない。異なるサイトへの同時アクセスでクラッシュ可能性
- [ ] **R3** `KakuyomuApiService.cs:142-147` — `JsonDocument.Parse()` が `using` なし。大きなHTMLでバッファリーク
- [ ] **R4** `KakuyomuApiService.cs:97-130` — Apollo State の chapter 列挙順が JSON 仕様上は未保証。`ParseEpisodeIds` と `ParseEpisodes` の独立パースで順序不一致リスク
- [ ] **R5** `NarouApiService.cs:252` — `DateTime.Today`/`DateTime.Now` 使用。JST以外の端末でランキング日付ズレ
- [ ] **R6** `App.xaml.cs:69-72` — `Shell.Current` 未設定タイミングでのアクセス（`InitializeAppAsync` はバックグラウンド実行）
- [ ] **R7** `ReaderStyleResolver.cs:15-17` — `Application.Current!` null 抑制。アプリ終了中/バックグラウンド遷移中に NRE
- [ ] **R8** `ReaderHtmlBuilder.cs:42` — `content.Split('\n')` が `\r\n` を正しく処理しない。行末 `\r` 残留
- [ ] **R9** `ReaderViewModel.cs:159-160` — 前後エピソード取得が `EpisodeNo ± 1` 固定。欠番でナビゲーション不能
- [ ] **R10** `ReaderViewModel.cs:211-214` — `finally` で `ScrollToTop` 呼び出し。エラー時にスクロール位置消失
- [ ] **R11** `NovelListViewModel.cs:52-57` — リロード判定が件数一致のみ。既読状態やプロパティ変更が反映されない
- [ ] **R12** `EpisodeCacheRepository.cs:62-69` — ISO 8601 文字列の SQLite 辞書順比較。タイムゾーン不一致で不正確
- [ ] **R13** `SearchViewModel.cs:308-343` — `RegisterAsync` で `CancellationToken` なし `FetchEpisodeListAsync` 呼び出し
- [ ] **R14** `App.xaml.cs:63` — `DeleteExpiredAsync` が fire-and-forget。例外で `UnobservedTaskException`

---

## 🟠 SMELL（コード品質・保守性の問題）— 15件

- [x] **S1** `Episode.cs`, `Novel.cs` — boolean 値が `int` 型。`SearchResult.IsCompleted`(bool) との型不整合
- [ ] **S2** `Episode.cs`, `Novel.cs`, `EpisodeCache.cs` — 日時フィールドが全て `string` 型。フォーマット依存のソートバグリスク → 別PR `feature/refactor-datetime-types` に分離
- [ ] **S3** `Novel.cs:49-53` — `SiteType` が型名(enum)とプロパティ名(int)の両方で使用。可読性低下 → S8 で実用上の読みやすさは改善されたためスキップ
- [x] **S4** `LogHelper.cs` — `Debug.WriteLine` はリリースビルドで除去。本番ログ不可
- [x] **S5** `NovelRepository.cs:171-175` — episode_cache 削除が N+1 クエリ。`EpisodeCacheRepository.DeleteByNovelIdAsync` の一括削除を使うべき
- [x] **S6** `NetworkPolicyService.cs:53-55` — `IsOnline` が例外時に `true` 返却。楽観的すぎ
- [x] **S7** `NetworkPolicyService.cs:120-131` — `EnforceDelayAsync` がリクエスト毎にDB読取。キャッシュすべき
- [x] **S8** `NovelCardViewModel.cs:48`, `SearchResultViewModel.cs:43` — `SiteTypeLabel` が三項演算子ハードコード。新サイトで "カクヨム" 固定
- [x] **S9** `NovelServiceFactory.cs` — ファクトリが具象クラスに依存。新サイト追加にファクトリ変更が必要
- [x] **S10** `SettingsViewModel.cs:62-87` — 全 `OnXxxChanged` が fire-and-forget DB 保存。スライダー高速操作で並列書き込み
- [x] **S11** `Converters.xaml:9-10` — `BoolToGrayConverter` の `FalseColor="Black"` がダークモード非対応
- [x] **S12** `SettingsPage.xaml:59` — 非推奨 `Frame` を使用。`Border` に移行すべき
- [x] **S13** `NovelListPage.xaml:21-25` — `x:Name="EmptyView"` が未使用デッドコード
- [ ] **S14** `MauiProgram.cs:38` — `HttpClient` 直接シングルトン登録。`IHttpClientFactory` 未使用 → 別PR `feature/refactor-http-factory` に分離
- [x] **S15** `App.xaml.cs:77-100` — 既にバックグラウンドスレッドなのに `Task.Run` ラップ

---

## 🔵 REFACTOR（リファクタリング提案）— 8件

- [x] **F1** `SearchViewModel.cs:136-277` — `SearchAsync`/`FetchRankingAsync`/`FetchGenreAsync` の重複をテンプレートメソッドに抽出
- [x] **F2** `KakuyomuApiService.cs` — `ParseEpisodeIds` と `ParseEpisodes` のApollo State走査ロジック重複を統合 → R4 修正時に実装済み
- [x] **F3** `ReaderHtmlBuilder.cs:13-75` — HTML/CSS/JS の `sb.Append()` 連鎖をテンプレート or raw string literal に分離
- [x] **F4** `Episode.cs`, `Novel.cs` — 複合ユニーク制約(`NovelId+EpisodeNo`, `NovelId+SiteType`)の欠如
- [x] **F5** `ReaderCssState.cs` — `BackgroundThemeIndex`/`LineSpacingIndex` のマジックナンバーを enum 化
- [ ] **F6** `ReaderPage.xaml` — テーマ Trigger が ContentPage/Label で同一パターン重複。Style に集約 → skip: ContentPage と Label は型が異なるため単一 Style に集約不可。再利用箇所もない
- [ ] **F7** Views code-behind — ViewModel 参照方法がフィールド保持 / `BindingContext is` キャストで不統一 → skip: 効果小（削減行数10未満）・どちらも安全・is キャストは防御的で可読性は悪くない
- [x] **F8** `HasValueConverter.cs:16` — `IEnumerable.GetEnumerator()` が Dispose されない
