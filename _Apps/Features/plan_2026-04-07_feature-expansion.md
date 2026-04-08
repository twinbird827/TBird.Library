# 機能拡張 実装計画書

| 項目 | 内容 |
|---|---|
| 作成日 | 2026-04-07 |
| 対象ブランチ | `app-novelviewer` |
| ステータス | **方針確定（着手可）** |
| 対象機能 | 縦書き表示 / 一覧ソート変更 / お気に入り(作品＋話) / 一括ダウンロード / ランキング・ジャンルブラウズ |
| 横断方針 | Wi-Fi接続時の積極先読み・通信間ディレイ・バックグラウンド遅延取得 |

> 本書は実装着手前の計画書です。確認後に修正・着手します。コードはまだ変更していません。

---

## 0. 横断方針（全機能共通）

### 0.1 通信ポリシー（NetworkPolicyService の新設）

新規サービス `Services/Network/NetworkPolicyService.cs` を Singleton で導入し、HTTP リクエストを一元的にスロットリング・ゲートする。

| 概念 | 内容 |
|---|---|
| **接続種別判定** | `Connectivity.Current.ConnectionProfiles` に `WiFi` が含まれるか |
| **積極先読み（Aggressive Mode）** | Wi-Fi 接続時：未キャッシュ話を可能な限り全部取得 |
| **節制モード（Conservative Mode）** | モバイル通信時：手動操作と更新チェックのみ。先読みは停止 |
| **通信間ディレイ** | 同一サイトへの連続リクエスト間に既定 800ms（設定可能 500–2000ms）の `Task.Delay` を挿入 |
| **同時実行制御** | サイト単位に `SemaphoreSlim(1,1)`。並列リクエストを禁止し、必ず直列＋ディレイで送出 |
| **キャンセル** | アプリ停止・ネットワーク切断・モバイル切替時にバックグラウンドジョブを CancellationToken で停止 |
| **失敗時** | 1リクエスト失敗で全停止せず、当該話のみ skip して継続。連続5失敗で当該ジョブ中断 |

```csharp
// イメージシグネチャ（実装はまだ）
public interface INetworkPolicyService
{
    bool IsWifiConnected { get; }
    bool IsOnline { get; }
    event EventHandler<NetworkChangedEventArgs> NetworkChanged;

    // サイト別ゲート。直列化＋ディレイを保証する
    Task<T> ExecuteAsync<T>(SiteType site, Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
```

**重要**: 既存の `NarouApiService` / `KakuyomuApiService` の HTTP 呼び出しを `NetworkPolicyService.ExecuteAsync` でラップする。これにより既存の検索・話一覧取得・本文取得すべてが自動的にディレイ＋直列化される（通常操作の体感は微差、暴走防止が効く）。

### 0.2 バックグラウンドジョブ基盤（BackgroundJobQueue）

新規 `Services/Background/BackgroundJobQueue.cs`（Singleton）。

- **キュー方式**: `Channel<BackgroundJob>` (Bounded, 容量100, FullMode=Wait)
- **ワーカー**: アプリ起動時に 1 本だけ消費 Task を起動（Wi-Fi検出時に動作開始、切断時に一時停止）
- **ジョブ種別**:
  - `PrefetchEpisodesJob(novelId, fromEpisodeNo, toEpisodeNo)` — 未キャッシュ話の本文取得
  - `RefreshNovelMetaJob(novelId)` — 既存の更新チェックを単発実行
  - `FetchRankingJob(siteType, genreId, kind)` — ランキング取得
- **永続化**: ジョブはメモリのみ。アプリ再起動時は「未読＆未キャッシュ」を起動時にスキャンして再投入
- **優先度**: ユーザーが現在開いている小説 > お気に入い > その他
- **進捗通知**: `IObservable<JobProgress>` でNovelListに進捗バッジ（任意、第二段で実装可）

### 0.3 設定キー追加

`SettingsKeys` / `DatabaseService.SeedSettingsAsync` に追加：

| キー | 既定値 | 用途 |
|---|---|---|
| `prefetch_enabled` | "1" | Wi-Fi接続時にバックグラウンド先読みするかのON/OFF（モバイル通信時は本値に関わらず常に先読みしない） |
| `request_delay_ms` | "800" | サイトへのリクエスト間ディレイ（500–2000ms、設定画面で変更可） |
| `vertical_writing` | "0" | 縦書き表示 ON/OFF |
| `novel_sort_key` | "updated_desc" | 小説一覧ソート（後述） |

> **モバイル通信時の先読みポリシー（確定）**: モバイル通信時は `BackgroundJobQueue` の Prefetch 系ジョブを**常に**ディスパッチしない。`prefetch_enabled` 設定は「Wi-Fi 接続時に先読みするか」のON/OFFのみを制御する（モバイル時は設定値に関わらず不可）。手動操作（読書中の本文取得、検索、更新チェック）は通信種別に関わらず動作する。

---

## 1. 縦書き表示

### 1.1 概要
ReaderPage の本文を縦書き（右→左）でレンダリング。設定 ON/OFF をユーザーが切り替え可能。

### 1.2 実装方式（確定：ハイブリッド）

- **横書き時**: 既存の `ScrollView > Label` をそのまま使用（既読検知・スクロール挙動も現状維持）
- **縦書き時のみ**: `WebView` に切り替え、HTML+CSS `writing-mode: vertical-rl;` で表示
- ReaderPage のレイアウトには `Label` と `WebView` の両方を配置し、`IsVerticalWriting` で `IsVisible` を排他制御

### 1.3 設計

- **新規ファイル**: `Helpers/ReaderHtmlBuilder.cs`
  - 入力: 本文(string), FontSize, LineHeight, BackgroundTheme
  - 出力: 縦書き用の完全な HTML 文字列（インライン CSS）
  - CSS: `html, body { writing-mode: vertical-rl; -webkit-writing-mode: vertical-rl; }` ＋テーマ別 `background` / `color` ＋フォント/行間
- **ReaderPage.xaml**:
  - 既存 `ScrollView > Label` はそのまま残す（`IsVisible="{Binding IsHorizontal}"`）
  - 同階層に `WebView`（`IsVisible="{Binding IsVerticalWriting}"`）を追加
- **ReaderPage.xaml.cs**:
  - 横書き時: 従来の `OnScrolled`
  - 縦書き時: `WebView.Navigated` 後に EvaluateJavaScriptAsync で scroll 監視を仕込む。スクロール左端到達で `lanobe://read-end` ナビゲーションを発火 → `Navigating` イベントで Cancel しつつ ReaderViewModel.MarkAsRead() を呼ぶ
- **ReaderViewModel**:
  - `[ObservableProperty] bool IsVerticalWriting`（InverseBoolConverter で `IsHorizontal` をXAML側で生成、または別プロパティ）
  - `IsVerticalWriting` 変更時 / 本文変更時に HTML を再生成し WebView.Source を更新
- **SettingsViewModel / SettingsPage**:
  - 「縦書き表示」Switch を追加。プレビューも切り替え

### 1.4 トレードオフ（受容）
- WebView は縦書きON時のみロードされるためメモリ増は限定的
- 縦書き時のテキスト選択UIは WebView 標準（横書き時の挙動には影響なし）
- 既読検知の JS Bridge は縦書き時のみ。横書きは現行ロジックを温存

### 1.5 影響範囲ファイル
- 新規: `Helpers/ReaderHtmlBuilder.cs`
- 修正: `Views/ReaderPage.xaml`, `Views/ReaderPage.xaml.cs`, `ViewModels/ReaderViewModel.cs`, `ViewModels/SettingsViewModel.cs`, `Views/SettingsPage.xaml`, `Helpers/SettingsKeys.cs`, `Services/Database/DatabaseService.cs`

---

## 2. 一覧内ソート変更

### 2.1 ソート種別

| キー | 表示名 | 内容 |
|---|---|---|
| `updated_desc` | 更新日時(新しい順) | 既定（現状） |
| `updated_asc` | 更新日時(古い順) | |
| `title_asc` | タイトル昇順 | OrderBy(Title) |
| `title_desc` | タイトル降順 | |
| `author_asc` | 作者昇順 | |
| `unread_desc` | 未読話数(多い順) | |
| `registered_desc` | 登録日時(新しい順) | |
| `favorite_first` | お気に入り優先 + 更新日時順 | お気に入りが先頭、その後 updated_desc |

### 2.2 設計

- **NovelListViewModel**:
  - `[ObservableProperty] string SortKey`（初期値は AppSettings から読込み）
  - 変更時に LoadNovelsAsync() を再実行＋ AppSettingsRepository へ保存
  - 並び替えは `Novels` を再構築せず、`NovelRepository.GetAllAsync(SortKey)` で SQL 側に任せる
- **NovelRepository**:
  - `GetAllAsync(string sortKey)` を追加。`switch` で `OrderBy` 句を切替
  - 未読話数ソートは `episodes` との集計が必要 → サブクエリ（`SELECT n.*, (SELECT COUNT(*) FROM episodes WHERE novel_id=n.id AND is_read=0) AS unread FROM novels n ORDER BY unread DESC`）
- **NovelListPage.xaml**:
  - ツールバー右に `ToolbarItem`（並び替えアイコン）を追加
  - タップで Picker / ActionSheet（`DisplayActionSheet`）でソート選択

### 2.3 リスク
- **disadvantage**: 未読数ソートはサブクエリで N+1 にはならないが、登録小説 1000件超で多少遅くなる（許容範囲）

### 2.4 影響範囲ファイル
- 修正: `Services/Database/NovelRepository.cs`, `ViewModels/NovelListViewModel.cs`, `Views/NovelListPage.xaml`, `Helpers/SettingsKeys.cs`

---

## 3. お気に入り（作品＋話）

### 3.1 データモデル

#### 作品お気に入り — `novels` に列追加
```sql
ALTER TABLE novels ADD COLUMN is_favorite INTEGER NOT NULL DEFAULT 0;
ALTER TABLE novels ADD COLUMN favorited_at TEXT NULL;
```

#### 話お気に入り — `episodes` に列追加
```sql
ALTER TABLE episodes ADD COLUMN is_favorite INTEGER NOT NULL DEFAULT 0;
ALTER TABLE episodes ADD COLUMN favorited_at TEXT NULL;
```

- **マイグレーション**: 現状機構なし。`DatabaseService.InitializeAsync` で `PRAGMA table_info(novels)` / `PRAGMA table_info(episodes)` を実行し、対象列がなければ ALTER を発行する簡易マイグレーションを追加
- `Models/Novel.cs` に `IsFavorite`(int), `FavoritedAt`(string?)
- `Models/Episode.cs` に `IsFavorite`(int), `FavoritedAt`(string?)

### 3.2 UI（作品お気に入り）

- **NovelListPage**: カードに星アイコン（`★`/`☆`）。タップでトグル。SwipeView 右スワイプにも「お気に入り切替」追加
- **EpisodeListPage**: ToolbarItem に星ボタン（その作品をお気に入り切替）
- **ソート連動**: `favorite_first` ソートでお気に入り優先表示

### 3.3 UI（話お気に入り）

- **EpisodeListPage**:
  - 各話行の右側に星アイコンを追加（タップでトグル）
  - お気に入りの話のみを絞り込む「★」フィルタトグルを ToolbarItem に追加
- **ReaderPage**:
  - フッターまたはヘッダーに星ボタンを追加。現在表示中の話をお気に入りトグル
  - `[ObservableProperty] bool IsCurrentEpisodeFavorite` を ReaderViewModel に追加
- **EpisodeRepository**:
  - `SetFavoriteAsync(int episodeId, bool isFavorite)` を追加
  - `GetFavoritesByNovelIdAsync(int novelId)` を追加（お気に入り一覧取得）

### 3.4 ユースケース

- 「印象的なシーンを後で読み返したい」用途
- 章単位ではなく話単位で抜粋管理可能
- 将来：お気に入り話のエクスポート機能の素地になる

### 3.5 挙動連動

- **更新チェック優先度**: お気に入り作品は UpdateCheckService で先頭から処理
- **先読み優先度**: BackgroundJobQueue でお気に入り作品ジョブを優先キューへ
- **話お気に入り削除耐性**: キャッシュ自動削除（期限切れ）でも話お気に入りフラグは維持（フラグは episodes テーブル、本文は episode_cache テーブル）

### 3.6 影響範囲ファイル
- 修正: `Models/Novel.cs`, `Models/Episode.cs`, `Services/Database/DatabaseService.cs`, `Services/Database/NovelRepository.cs`, `Services/Database/EpisodeRepository.cs`, `ViewModels/NovelListViewModel.cs`, `ViewModels/EpisodeListViewModel.cs`, `ViewModels/ReaderViewModel.cs`, `Views/NovelListPage.xaml`, `Views/EpisodeListPage.xaml`, `Views/ReaderPage.xaml`, `Services/UpdateCheckService.cs`

---

## 4. 一括ダウンロード（および全体の積極先読み）

### 4.1 対象範囲

| トリガ | 動作 |
|---|---|
| **手動：エピソード一覧の「全話DL」ボタン** | 当該小説の未キャッシュ話をすべてキューに投入 |
| **手動：エピソード長押し → 範囲選択DL** | 選択範囲をキューに投入（第二段で実装、本計画は未着手枠） |
| **自動：登録直後** | 新規登録小説の全話を Wi-Fi接続時のみ自動キューイング |
| **自動：更新チェックで新話検出時** | 検出した新話を自動キューイング |
| **自動：起動時スキャン** | 「未読 ＆ 未キャッシュ」を Wi-Fi接続時にバックグラウンド投入 |

### 4.2 設計

- **新規**: `Services/Background/PrefetchService.cs`
  - `Task EnqueueNovelAsync(int novelId, CancellationToken ct)` — 未キャッシュ話を全件キューへ
  - `Task EnqueueEpisodesAsync(IEnumerable<int> episodeIds, CancellationToken ct)`
- **BackgroundJobQueue ワーカー**:
  - `PrefetchEpisodesJob` を取り出し
  - サイトごとに `NetworkPolicyService.ExecuteAsync(...)` 経由で本文取得（自動ディレイ）
  - 取得した本文を `EpisodeCacheRepository.InsertAsync`
  - **モバイル通信時は Prefetch ジョブを一切実行しない（設定不可・固定）**。Wi-Fi 切断で一時停止、Wi-Fi 再接続で自動レジューム
  - 設定 `prefetch_enabled` が OFF の場合は Wi-Fi 接続中であっても Prefetch を停止
- **エピソード一覧 UI**:
  - ToolbarItem「全話DL」追加
  - タップで `PrefetchService.EnqueueNovelAsync` を呼び、Snackbar で「N話をバックグラウンドで取得します」と通知
  - 各話の右端に「キャッシュ済み●」インジケータ（`EpisodeCacheRepository` で取得済みID集合を保持して bind）

### 4.3 リソース・マナー配慮（重要）

- 通信間ディレイ（既定800ms）は `NetworkPolicyService` が保証
- 同時 1 リクエスト/サイト
- 1 ジョブあたり最大 200 話で区切り、間に 5 秒のクールダウン
- HTTP エラーで連続 5 回失敗 → ジョブ中断
- **disadvantage**: Wi-Fi切断のたびジョブ停止／再開のため、移動中の利用では完了が遅れる
- **disadvantage**: なろう/カクヨムのサーバー規約上、過度な取得は望ましくない。800ms ディレイは"個人読書範囲"として穏当な値だが、運用次第で 1500ms 程度に上げるべき

### 4.4 影響範囲ファイル
- 新規: `Services/Network/NetworkPolicyService.cs`, `Services/Background/BackgroundJobQueue.cs`, `Services/Background/PrefetchService.cs`, `Services/Background/BackgroundJob.cs`
- 修正: `Services/Narou/NarouApiService.cs`, `Services/Kakuyomu/KakuyomuApiService.cs`（HTTP呼び出しを NetworkPolicyService 経由へ）, `MauiProgram.cs`（DI登録）, `App.xaml.cs`（起動時スキャン）, `ViewModels/EpisodeListViewModel.cs`, `Views/EpisodeListPage.xaml`, `Services/Database/EpisodeCacheRepository.cs`（取得済みID一覧API追加）, `Services/UpdateCheckService.cs`（新話検出後にキューイング）

---

## 5. ランキング・ジャンルブラウズ

### 5.1 サイト別の取得方法

#### なろう
- **API**: `https://api.syosetu.com/rank/rankget/?out=json&rtype={YYYYMMDD-d|w|m|q}` でランキング取得
- **ジャンル**: `https://api.syosetu.com/novelapi/api/?out=json&genre={id}&order={hyoka|daily|weekly|...}` で取得可能
- ジャンルID: 公式定義（恋愛/ファンタジー/SF/その他/ノンジャンル × サブジャンル）を `Models/NarouGenres.cs` に静的定義

#### カクヨム
- **API**: 公式 API なし。ランキングページ HTML をスクレイピング
  - `https://kakuyomu.jp/rankings/all/{daily|weekly|monthly|yearly|entire}`
  - ジャンル別: `https://kakuyomu.jp/rankings/{genre}/{period}` （genre は `fantasy`, `sf`, `love_story` など）
- カテゴリ定義は `Models/KakuyomuGenres.cs` に静的定義

### 5.2 サービス追加

`INovelService` を拡張：

```csharp
Task<List<SearchResult>> FetchRankingAsync(string genreId, RankingPeriod period, CancellationToken ct);
IReadOnlyList<GenreInfo> GetGenres();
```

- `RankingPeriod`: enum (Daily, Weekly, Monthly, Quarterly, Yearly, All)
- `GenreInfo`: { Id, Name }
- 既存 `NarouApiService` / `KakuyomuApiService` に実装。HTTP 呼び出しは `NetworkPolicyService.ExecuteAsync` 経由

### 5.3 UI

- **SearchPage 改修** または **新規 BrowsePage**
  - **採用**: SearchPage にタブを追加（既存「キーワード検索」＋新「ランキング」「ジャンル」）
  - `TabbedPage` ではなく、上部に `SegmentedControl` 風の Border+Buttons を置きコンテンツ切替
- **ランキングタブ**:
  - サイト選択（なろう/カクヨム/両方）
  - 期間選択 Picker（日/週/月/四半期/年/累計）
  - ジャンル選択 Picker（「全て」もあり）
  - 結果は既存の SearchResult カードをそのまま再利用
- **ジャンルタブ**:
  - サイト選択 → ジャンル一覧（CollectionView）→ タップで該当ジャンルの新着 or ランキングへ

### 5.4 キャッシュ

- ランキング結果は短期メモリキャッシュ（10分）。`Dictionary<string, (DateTime fetchedAt, List<SearchResult> data)>` を SearchViewModel に保持
- DB には保存しない（鮮度優先）

### 5.5 リスク
- **disadvantage**: カクヨムランキングは HTML スクレイピングのためサイト構造変更で壊れやすい（既存話一覧と同じリスク）
- **disadvantage**: なろうランキングAPIは結果が ncode のみのため、続けて novelapi で詳細取得が必要 → ディレイにより一覧表示が秒単位で遅くなる。**対策**: 1リクエストで複数 ncode 指定可能（`ncode=N1234AA-N5678BB`）の仕様を活用してまとめ取得

### 5.6 影響範囲ファイル
- 新規: `Models/NarouGenres.cs`, `Models/KakuyomuGenres.cs`, `Models/RankingPeriod.cs`, `Models/GenreInfo.cs`
- 修正: `Services/INovelService.cs`, `Services/Narou/NarouApiService.cs`, `Services/Kakuyomu/KakuyomuApiService.cs`, `ViewModels/SearchViewModel.cs`, `Views/SearchPage.xaml`

---

## 6. 実装順序（推奨）

| Step | 内容 | 理由 |
|---|---|---|
| **S1** | NetworkPolicyService（ディレイ＋直列化）と既存サービスへの組み込み | 他機能の前提。先に入れて既存動作の安定性を確認 |
| **S2** | BackgroundJobQueue + PrefetchService 基盤 | 一括DLとランキングの両方が依存 |
| **S3** | お気に入り（DBマイグレーション含む） | 小規模・独立性高く検証しやすい |
| **S4** | 一覧ソート変更 | お気に入り後にやると `favorite_first` が自然に組み込める |
| **S5** | 一括ダウンロード（UI＋自動先読み発火） | S1/S2 の上に乗るだけ |
| **S6** | 縦書き表示（ReaderPage の WebView化） | リーダー画面に大きく手を入れるので独立した PR が望ましい |
| **S7** | ランキング・ジャンルブラウズ | 最後。他機能に依存しない独立タブ追加 |

---

## 7. 残課題・確認事項

ユーザー確認をお願いしたい点：

1. **WebView化のトレードオフ**: ReaderPage を縦書き対応のため WebView に置換することを許容するか？（横書き時は現行 Label のままにするハイブリッド案を本書では推奨）
→ハイブリッド案で
2. **DBマイグレーション方針**: 簡易ALTER方式で進めて良いか？（`PRAGMA table_info` で列の有無を見て ALTER を発行する独自実装）
→OK
3. **通信ディレイの既定値**: 800ms で良いか？（カクヨム HTML スクレイピング多用なら 1500ms 推奨）
→デフォ800msで設定変更できるようにする
4. **モバイル通信時の挙動**: 「先読みは Wi-Fi 限定」を既定にするが、ユーザーがOFFにできる設定を出して良いか？
→Wifiでも先読みしないってこと？であればOFFにできる設定を出してよい。モバイル通信時も先読み可能にするかどうかの設定は不要→モバイル通信は先読み絶対不可
5. **ランキング機能のサイト**: 第一弾はなろうのみに絞るか？（カクヨムは公式APIなしで脆い）
→どっちも
6. **PR 分割**: 上記 S1〜S7 を個別 PR にするか、まとめて1本にするか？
→まとめて1本

追加要望→作品内のお気に入り話を設定できるようにしてほしい
---

## 8. 着手前チェックリスト

- [x] 上記 7 の確認事項にユーザー回答を得る（2026-04-07 完了）
- [ ] `app-novelviewer` から派生ブランチを作成（CLAUDE.md ルール準拠）
- [ ] S1〜S7 を 1本の PR にまとめる方針で実装。Step 単位ではコミット粒度を分ける
- [ ] requirements_lanovereader.md の該当セクションは本実装後に追記更新

## 9. 確定事項サマリ（2026-04-07 ユーザー回答）

| 項目 | 確定内容 |
|---|---|
| 縦書き実装 | ハイブリッド（横書き=Label / 縦書き=WebView） |
| DBマイグレーション | PRAGMA + ALTER 簡易方式 |
| 通信ディレイ | 既定 800ms。設定画面で 500–2000ms 可変 |
| モバイル通信時の先読み | **絶対不可（設定項目なし、固定）**。`prefetch_enabled` は Wi-Fi時のON/OFFのみ |
| ランキング対応サイト | なろう＋カクヨム両方 |
| PR 構成 | S1〜S7 まとめて 1 本 |
| 追加要望 | **作品内のお気に入り話**機能を追加（§3 に統合済） |

---

## 10. 事前調査結果（実装時の追加調査不要レベル）

調査日: 2026-04-07。本節の情報は実装時にそのまま参照可能。

### 10.1 既存 HTTP 通信ポイント（S1 NetworkPolicyService 組込先）

| サービス | メソッド | 行 | 呼出 | TO | 連続 |
|---|---|---|---|---|---|
| NarouApiService | SearchAsync | 40 | GetStringAsync | 5s | 単発 |
| NarouApiService | FetchEpisodeListAsync | 82 | GetStringAsync (while ループ) | 10s | **複数(?p=1,2,...)** |
| NarouApiService | FetchEpisodeContentAsync | 141 | GetStringAsync | 5s | 単発 |
| NarouApiService | FetchNovelInfoAsync | 164 | GetStringAsync | 30s | 単発 |
| KakuyomuApiService | SearchAsync | 32 | GetStringAsync | 5s | 単発 |
| KakuyomuApiService | FetchEpisodeListAsync | 75 | GetStringAsync | 10s | 単発 |
| KakuyomuApiService | FetchEpisodeContentAsync | 197/208 | GetStringAsync | 10s | **2回(TOC→本文)** |
| KakuyomuApiService | FetchNovelInfoAsync | 236 | GetStringAsync | 30s | 単発 |

**ディレイ挿入対象（重要）**:
- NarouApiService.cs:82 の while ループ内、ページ取得後に `await NetworkPolicyService.DelayAsync(SiteType.Narou, ct)` を挿入
- KakuyomuApiService.cs:208 の 2 回目 GetStringAsync 直前に同様

**実装方針**: 各 `_httpClient.GetStringAsync(url, cts.Token)` 呼出を `_networkPolicy.GetStringAsync(SiteType.X, url, cts.Token)` に置換するラッパ方式が最小侵襲。`NetworkPolicyService` 内部で SemaphoreSlim ＋ Delay を実施。

### 10.2 DB マイグレーション（S3 お気に入り）

#### sqlite-net-pcl の挙動確認済み事項
- `CreateTableAsync<T>()` は `CreateFlags.None` 既定。**既存テーブルへの列追加は行わない**
- ALTER TABLE は `_db.ExecuteAsync("ALTER TABLE ... ADD COLUMN ...")` で直接発行可
- PRAGMA は `_db.QueryAsync<ColumnInfo>("PRAGMA table_info(novels)")` で取得可

#### 実装する Helper（DatabaseService 内に private 追加）
```csharp
private class ColumnInfo { public int cid; public string name; public string type; public int notnull; public string? dflt_value; public int pk; }

private async Task EnsureColumnAsync(string table, string column, string ddlSuffix)
{
    var cols = await _db.QueryAsync<ColumnInfo>($"PRAGMA table_info({table})");
    if (!cols.Any(c => string.Equals(c.name, column, StringComparison.OrdinalIgnoreCase)))
        await _db.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {ddlSuffix}");
}
```

#### InitializeAsync に追加する呼出（CreateTableAsync の後、SeedSettingsAsync の前）
```csharp
await EnsureColumnAsync("novels",   "is_favorite",  "INTEGER NOT NULL DEFAULT 0");
await EnsureColumnAsync("novels",   "favorited_at", "TEXT NULL");
await EnsureColumnAsync("episodes", "is_favorite",  "INTEGER NOT NULL DEFAULT 0");
await EnsureColumnAsync("episodes", "favorited_at", "TEXT NULL");
```

#### 既存テーブル定義（追加列の参考）
- `Novel` (Models/Novel.cs): Id/SiteType/NovelId/Title/Author/TotalEpisodes/IsCompleted/LastUpdatedAt/RegisteredAt/HasUnconfirmedUpdate/HasCheckError、`[Table("novels")]`
- `Episode` (Models/Episode.cs): Id/NovelId([Indexed])/EpisodeNo/ChapterName?/Title/IsRead/ReadAt?/PublishedAt?、`[Table("episodes")]`
- いずれも `[Column("snake_case")]` で命名

### 10.3 ソート用 SQL（S4 一覧ソート変更）

`NovelRepository.GetAllAsync(string sortKey)` の switch 実装案：

| sortKey | クエリ |
|---|---|
| `updated_desc` | `Table<Novel>().OrderByDescending(n => n.LastUpdatedAt)` |
| `updated_asc` | `OrderBy(n => n.LastUpdatedAt)` |
| `title_asc` | `OrderBy(n => n.Title)` |
| `title_desc` | `OrderByDescending(n => n.Title)` |
| `author_asc` | `OrderBy(n => n.Author)` |
| `registered_desc` | `OrderByDescending(n => n.RegisteredAt)` |
| `unread_desc` | 生SQL: `SELECT n.*, (SELECT COUNT(*) FROM episodes e WHERE e.novel_id=n.id AND e.is_read=0) AS unread FROM novels n ORDER BY unread DESC, n.last_updated_at DESC` |
| `favorite_first` | 生SQL: `SELECT * FROM novels ORDER BY is_favorite DESC, last_updated_at DESC` |

unread_desc は `_db.QueryAsync<Novel>(sql)` で実行（unread 列は Novel に `[Ignore]` 付き UnreadCount を設けて受ける、または専用 DTO）。

### 10.4 ReaderPage 既読検知（S6 縦書き対応）

#### 現状（横書き Label）
- ReaderPage.xaml.cs:15-24 `OnScrolled`: `scrollView.ScrollY + Height >= ContentSize.Height - 10` で MarkAsReadCommand 発火（毎フレーム呼ばれるため ViewModel 側で IsRead 重複防止）

#### 縦書き WebView の追加実装
- **HTML ロード**: `webView.Source = new HtmlWebViewSource { Html = ReaderHtmlBuilder.Build(...) };`（`Microsoft.Maui.Controls.HtmlWebViewSource`）
- **JS → C# 通信**: `Navigating` イベント＋カスタムスキーム方式（HybridWebView 不要）
  ```csharp
  webView.Navigating += (s, e) => {
      if (e.Url?.StartsWith("lanobe://read-end") == true) {
          e.Cancel = true;
          _viewModel.MarkAsReadCommand.Execute(null);
      }
  };
  ```
- **JS 側スクロール監視**（HTML テンプレートに埋め込み）:
  ```js
  window.addEventListener('scroll', function() {
      // 縦書きは scrollLeft が負値。左端到達 = scrollLeft が最小値
      var el = document.scrollingElement;
      var maxNeg = -(el.scrollWidth - el.clientWidth);
      if (el.scrollLeft <= maxNeg + 10) {
          window.location.href = 'lanobe://read-end';
      }
  }, { passive: true });
  ```
- **Android WebView での `writing-mode: vertical-rl` サポート**: Android System WebView 70+ (Android 9 以降) で問題なし。本アプリの最低 API 34（Android 14）は十分カバー
- **WebView インスタンス**: 1 ページで `Source` を差し替える方式（毎話 new しない）

#### ReaderPage.xaml への追加
既存 `ScrollView`（行 31 付近）と同じ Grid 行に WebView を並べ、IsVisible で排他：
```xml
<ScrollView IsVisible="{Binding IsHorizontal}" .../>
<WebView    IsVisible="{Binding IsVerticalWriting}" x:Name="VerticalWebView" Navigating="OnWebViewNavigating"/>
```

### 10.5 Wi-Fi 検出と WorkManager 制約（S1/S2/S5）

#### MAUI Connectivity（前景・ViewModel 側）
- 名前空間: `Microsoft.Maui.Networking`
- Wi-Fi 判定: `Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi)`
- 変化検知: `Connectivity.ConnectivityChanged += (s,e) => { ... }`（`e.ConnectionProfiles`）
- パーミッション: `ACCESS_NETWORK_STATE`（既に AndroidManifest.xml に記載済み）

#### Android ConnectivityManager（バックグラウンド・Worker 側で確実に動かす場合）
```csharp
var cm = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);
var caps = cm.GetNetworkCapabilities(cm.ActiveNetwork);
bool isWifi = caps?.HasTransport(TransportType.Wifi) ?? false;
```
本アプリの BackgroundJobQueue は MAUI 起動済プロセス内で動くので **MAUI Connectivity で十分**。WorkManager Worker 内のみ、保険として直接 API を使うか検討。

#### WorkManager の Wi-Fi 限定化
- 現状: `UpdateCheckScheduler.cs:11-13` で `NetworkType.Connected`
- **更新チェックそのものはモバイル通信でも動かしたい**ため、`NetworkType.Connected` のままで OK（変更不要）
- 一方、Prefetch 系のジョブは BackgroundJobQueue（インプロセス）の責任で「Wi-Fi なら走る／そうでなければ走らない」を判定する。WorkManager の制約変更は不要

### 10.6 なろうランキング・ジャンル（S7）

#### ランキング API
- URL: `https://api.syosetu.com/rank/rankget/`
- 必須: `out=json`, `gzip=5`, `rtype=YYYYMMDD-{d|w|m|q}`
- 制約:
  - `d` = 日間（任意の日付）
  - `w` = 週間（**火曜日のみ**）
  - `m` = 月間（**毎月1日のみ**）
  - `q` = 四半期（**毎月1日のみ**）
- レスポンス: `[{ ncode, pt, rank }, ...]` 最大300件（**作品メタ情報なし**）
- gzip 必須（HTTP圧縮ではなくレスポンスボディ自体）→ `GZipStream` で解凍

```csharp
// 実装スケッチ
using var stream = await _http.GetStreamAsync(url, ct);
using var gz = new GZipStream(stream, CompressionMode.Decompress);
using var sr = new StreamReader(gz);
var json = await sr.ReadToEndAsync();
```

#### 直近確定日の決定
- 日間: 当日 08:00 以降 → 前日。それ以前 → 2 日前
- 週間: `DateTime.Today` から直近の火曜日へ後退
- 月間/四半期: 当月 1 日

#### ジャンルメタの取得（rankget の結果を novelapi で詳細取得）
- URL: `https://api.syosetu.com/novelapi/api/`
- 複数 ncode: `ncode=N1234AB-N5678CD-N9999EF`（ハイフン区切り）
- `lim` 最大500、`of=t-n-w-s-bg-g-k` でタイトル/ncode/作者/あらすじ/biggenre/genre/キーワード
- レート制限: 80,000 req/24h/IP、400MB/24h/IP。`gzip=5` 必須

#### ジャンル ID（実装時にそのまま定数化可能）
| 大ジャンル biggenre | 値 |
|---|---|
| 恋愛 | 1 |
| ファンタジー | 2 |
| 文芸 | 3 |
| SF | 4 |
| その他 | 99 |
| ノンジャンル | 98 |

| サブジャンル genre | 値 |
|---|---|
| 異世界恋愛 | 101 |
| 現実世界恋愛 | 102 |
| ハイファンタジー | 201 |
| ローファンタジー | 202 |
| 純文学 | 301 |
| ヒューマンドラマ | 302 |
| 歴史 | 303 |
| 推理 | 304 |
| ホラー | 305 |
| アクション | 306 |
| コメディー | 307 |
| VRゲーム | 401 |
| 宇宙 | 402 |
| 空想科学 | 403 |
| パニック | 404 |
| 童話 | 9901 |
| 詩 | 9902 |
| エッセイ | 9903 |
| リプレイ | 9904 |
| その他 | 9999 |
| ノンジャンル | 9801 |

#### `order` 値（ジャンルブラウズで使用想定）
`new` / `favnovelcnt` / `hyoka` / `dailypoint` / `weeklypoint` / `monthlypoint` / `quarterpoint` / `yearlypoint` / `lengthdesc`

### 10.7 カクヨムランキング（S7）

#### URL パターン
`https://kakuyomu.jp/rankings/{genre}/{period}`

| period | 値 |
|---|---|
| 日間 | `daily` |
| 週間 | `weekly` |
| 月間 | `monthly` |
| 年間 | `yearly` |
| 累計 | `entire` |

| genre slug | 表示名 |
|---|---|
| `all` | 総合 |
| `fantasy` | 異世界ファンタジー |
| `action` | 現代ファンタジー |
| `sf` | SF |
| `love_story` | 恋愛 |
| `romance` | ラブコメ |
| `drama` | 現代ドラマ |
| `horror` | ホラー |
| `mystery` | ミステリー |
| `nonfiction` | エッセイ・ノンフィクション |
| `history` | 歴史・時代・伝奇 |
| `criticism` | 創作論・評論 |
| `others` | 詩・童話・その他 |

オプションクエリ: `?work_variation=long`（長編絞込）、`&page=N`（ページネーション、1始まり）

#### HTML 解析方針
- `__NEXT_DATA__` は **無し**（既存の作品ページとは違い SSR HTML）
- AngleSharp で `a[href^="/works/"]` を全取得 → href から workId を正規表現抽出
- タイトル: アンカーのテキスト
- 作者: 同カード内の `a[href^="/users/"]` のテキスト
- カクヨム既存 SearchAsync の DOM 解析パターンを踏襲

#### リダイレクト
HTTP→HTTPS、または work_variation 付与で 302 が発生する場合あり。HttpClient はデフォルトで自動追従するため特別対応不要。

### 10.8 SearchPage タブ追加位置（S7）

現在の構造（SearchPage.xaml）：
- `Grid RowDefinitions="Auto,Auto,*"`：(0)検索バー (1)サイトCheckBox (2)結果

**改修案**: 最上部に `RowDefinitions="Auto,Auto,Auto,*"` でセグメント切替バーを追加し、表示モード（キーワード/ランキング/ジャンル）を切替。各モードのコンテンツは `Grid` を 3 つ重ねて IsVisible で排他。

### 10.9 SettingsPage 追加項目位置（S6/S1）

`SettingsPage.xaml` の構造は ScrollView > VerticalStackLayout に各設定セクションが BoxView で区切られている。
- **「読書設定」セクション内**に：縦書き表示 Switch を追加
- **「更新設定」セクションの後**に新セクション「通信設定」を追加：
  - リクエスト間ディレイ Slider（500–2000ms）
  - Wi-Fi 接続時の先読み Switch (`prefetch_enabled`)
- **「読書設定」内**に：小説一覧の並び順 Picker（`novel_sort_key`）

`SettingsViewModel.cs` の partial void OnXxxChanged パターンに合わせ、新設定も fire-and-forget で `_settingsRepo.SetValueAsync` を呼ぶ。

### 10.10 BackgroundJobQueue 起動位置（S2）

- DI: `MauiProgram.cs:42` の後に `AddSingleton<BackgroundJobQueue>()` / `AddSingleton<NetworkPolicyService>()` / `AddSingleton<PrefetchService>()`
- 起動: `App.xaml.cs:49 InitializeAppAsync` の DB 初期化後、`_ = backgroundJobQueue.StartAsync()` を Task.Run で開始
- 起動時の未読＆未キャッシュスキャン: 同 InitializeAppAsync 末尾で `Task.Run(() => prefetchService.EnqueueAllUnreadAsync())`
- 停止: アプリ終了時の特別処理は不要（プロセス終了で自動）。CancellationTokenSource を Singleton で保持し `App.OnSleep` で Cancel

### 10.11 既存 ViewModel パターン（参考）

- `[ObservableProperty] private bool _isLoading;` ＋ `[NotifyCanExecuteChangedFor(nameof(XxxCommand))]`
- `[RelayCommand(CanExecute = nameof(CanXxx))] private async Task XxxAsync()`
- `partial void OnXxxChanged(T value) => _ = _repo.SetValueAsync(...)` （fire-and-forget）
- ナビゲーション引数は `IQueryAttributable.ApplyQueryAttributes` で受け取り、`_ = InitializeAsync()` で発火
- 全 VM/Page は Transient、Service/Repo は Singleton

### 10.12 影響を受けないことを確認した既存機能

- 既存の更新チェック（UpdateCheckService）はモバイル通信時も動作継続（Prefetch とは別経路）
- ReaderPage 横書きモードの既読判定ロジックは無変更（縦書き時のみ JS Bridge を使用）
- WorkManager の `NetworkType.Connected` 制約は変更不要

---

**現状**: 本書作成のみ。コードは未変更。指示を受けてから着手します。
