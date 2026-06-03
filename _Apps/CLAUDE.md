# CLAUDE.md

このファイルは Claude Code が本プロジェクトを実装する際に常時参照する規約ファイルです。
**何を作るか**は `新刊チェッカー_要件定義書.md` を正とし、本ファイルは**どう作るか・どこに何があるか・既存資産は何か**を定義します。両者が食い違う場合、ライブラリ・実装詳細・コーディング規約は本ファイルを優先してください。

> ℹ️ **記入状況（2026-06-01 更新）**: 旧 `<!-- TODO: 開発者記入 -->` プレースホルダ（NuGet バージョン・命名規則の独自規約・既存ライブラリ §5）はリポジトリ内の既存 TBird.* 資産を調査して記入済み。残る開発者の手作業は **秘密情報の実値配置**（§6 の `Secrets.cs` をローカルに作成。Git 管理外）と、実装着手時の **§2「実装時に公式ドキュメントで確認すべき項目」の検証**のみ。記載と実コードが食い違った場合は実コード（既存ライブラリのシグネチャ）を正とし、本ファイルを更新すること。

---

## 1. プロジェクト概要（要約）

- **アプリ**: ライトノベル・コミックの新刊チェッカー（Android、個人用）
- **やること**: 追跡シリーズを登録 → 定期的に楽天Kobo APIで新刊（予約含む）を自動チェック → ローカル通知。あわせて発売予定表・ランキング・お気に入り・購入済管理・Googleカレンダー連携。
- **詳細仕様**: `新刊チェッカー_要件定義書.md` を参照。

---

## 2. 技術スタック

| 項目 | 内容 |
|---|---|
| 言語 | C# 12 |
| FW | .NET 9 / .NET MAUI |
| TFM | `net9.0-android`（全プロジェクト） |
| 最小OS | Android 13 / API 33 |
| IDE | Visual Studio 2022（MAUIワークロード） |
| アーキテクチャ | MVVM（CommunityToolkit.Mvvm）＋ Microsoft.Extensions.DependencyInjection |

### NuGetパッケージ
| パッケージ | 用途 | バージョン |
|---|---|---|
| CommunityToolkit.Mvvm | MVVM | `8.*`（TBird.Maui が参照済み。App は TBird.Maui 経由で推移取得。直接参照する場合も同系列に揃える） |
| System.Text.Json | JSON | 標準（.NET 9 同梱。別途参照不要） |
| sqlite-net-pcl | SQLite ORM | `1.*`（+ `SQLitePCLRaw.bundle_green 2.*`。TBird.Maui.DB が参照済みで推移取得。直接参照不要） |
| Xamarin.AndroidX.Work.Runtime | WorkManager | `2.11.2`（net9.0-android35.0 対応。App が直接参照） |
| Plugin.LocalNotification | ローカル通知 | `13.0.0`（net9.0-android35.0 対応。**14.x 以降は .NET 10 専用のため net9 では使用不可**。App が直接参照） |
| CommunityToolkit.Maui | トースト/スナックバー | `11.*`（net9 対応。App が直接参照。`builder.UseMauiCommunityToolkit()` が必要） |
| Xamarin.AndroidX.SavedState (+ .SavedState.Ktx) | 重複クラス回避の明示ピン | `1.3.1.1`（**core/ktx を同一版に揃えないと dex 段で `androidx.savedstate.ViewKt` が重複しビルド不能**。App が両方を明示参照） |

> バージョン確認日: 2026-06-01（nuget.org）。`CommunityToolkit.Mvvm` / `sqlite-net-pcl`（+ `SQLitePCLRaw.bundle_green`）は §5 の既存ライブラリ（TBird.Maui / TBird.Maui.DB）経由で取り込まれるため、App から直接 PackageReference する必要はない。新規追加の2つ（Work.Runtime / LocalNotification）は App プロジェクトが直接参照する。`Plugin.LocalNotification` は最新の 14.x が .NET 10 専用に切り替わったため、net9 では **13.0.0 で固定**すること（floating `14.*` は復元不能）。

---

## 3. ソリューション構成

```
_Apps/                          # app-new-book-checker ブランチのアプリ本体（このフォルダ直下）
├── App.sln
├── NewReleaseChecker.App/      # MAUI UI 層
│   ├── Views/                  # XAML（SCR-001〜011）
│   ├── ViewModels/             # 各画面の ViewModel
│   ├── App.xaml(.cs)
│   ├── AppShell.xaml(.cs)      # ボトムタブ定義
│   ├── MauiProgram.cs          # DI 登録
│   └── Platforms/Android/      # WorkManager 実装・通知のAndroid固有処理・権限
├── NewReleaseChecker.Core/     # ドメイン層（再利用可能）
│   ├── Models/                 # Series, Book 等のモデル
│   ├── Services/               # 新刊チェック共通サービス等
│   └── Abstractions/           # インターフェース群
└── NewReleaseChecker.Data/     # データ層（再利用可能基盤）
    ├── Database/               # SQLite アクセス
    └── Api/                    # 楽天Kobo API クライアント
```

### レイヤー依存ルール
- `App` → `Core` → `Data`（一方向）。`Core` は UI に依存しない。
- 汎用部品（API通信・SQLiteアクセス・通知・ログ・秘密情報）は**インターフェース越し**に利用し、差し替え可能にする。実装は `Data` または `Platforms/Android`、抽象は `Core/Abstractions`。

---

## 4. コーディング規約

### 命名規則（.NET標準）
| 対象 | 規則 | 例 |
|---|---|---|
| クラス / メソッド / プロパティ | PascalCase | `SeriesDetailViewModel`, `CheckNewReleasesAsync`, `IsFavorite` |
| privateフィールド | `_camelCase` | `_apiClient` |
| 定数 | PascalCase | `MaxSeriesPerWork` |
| インターフェース | `I` + PascalCase | `IRakutenApiClient` |
| 非同期メソッド | 末尾 `Async` | `LoadAsync` |

**本リポジトリ共通の独自規約（TBird.Library 既存資産に合わせること）**:
- **名前空間**: `NewReleaseChecker.{App|Core|Data}.{機能}`（レイヤー名を第2セグメントに。ルート CLAUDE.md「名前空間規則：TBird.{レイヤー}.{機能}」に倣う）。
- **拡張メソッド**: `{型名}Extension.cs` の命名規則（例: `StringExtension.cs`）。
- **null許容参照型**: 全プロジェクトで `<Nullable>enable</Nullable>`。
- **リソース解放**: 既存 TBird ライブラリ（TBird.Core）を継承するクラスは `TBirdObject` 派生となり `Dispose` が `sealed`。解放は `DisposeManagedResource()` / `DisposeUnmanagedResource()` を override する（ルート CLAUDE.md 参照）。アプリ独自クラスで TBirdObject を継承しない場合は通常の `IDisposable` / `IAsyncDisposable` でよい。
- **1ファイル1型**を基本とし、View（XAML）と ViewModel はファイル名を対応させる（`SeriesListPage.xaml` ↔ `SeriesListViewModel.cs`）。

### 実装パターン（必須）
- **ViewModel**: `CommunityToolkit.Mvvm` の `ObservableObject` / `[ObservableProperty]` / `[RelayCommand]` を使用。
- **ViewModel初期化**: コンストラクタで非同期処理をしない。`InitializeAsync()` を別途用意し、画面表示時（`OnAppearing` 等）に呼ぶ。
- **非同期**: API通信・DBアクセスは `async/await`。UIスレッドをブロックしない。
- **DI**: `MauiProgram.cs` の `builder.Services` に ViewModel・Service・APIクライアント・DBコンテキストを登録。
- **エラーハンドリング**: §8（要件定義書6.7）の方針に従う。手動チェック失敗=トースト、自動=ログのみ、検索=画面メッセージ、DB致命=ダイアログ。

---

## 5. 既存ライブラリ・共通資産

> 既存の自作ライブラリを優先利用する方針（要件定義書 2.2 / 6.6）。以下に把握している資産を記入する。

### ロギング基盤
> 既存の **TBird.Core + TBird.Maui** を利用する。新規ロガーは作らない。`ISecretsProvider` 等と異なり、ロギングは既にインターフェース（`TBird.Core.IMessageService`）＋静的ファサード（`TBird.Core.MessageService`）として完成している。
- **ライブラリ名**: `TBird.Core`（抽象・静的ファサード）＋ `TBird.Maui`（Android 実装）。App は ProjectReference でこれらを参照する。
- **名前空間 / 主要API**:
  - `TBird.Core.IMessageService` — `Error` / `Exception` / `Info` / `Warn` / `Debug` / `Confirm`。各メソッドは `[CallerMemberName] / [CallerFilePath] / [CallerLineNumber]` を自動取得するため、呼出元情報は引数不要。
  - `TBird.Core.MessageService`（static ファサード）— 実コードからはこれを呼ぶ（`MessageService.Info("...")`）。`MessageService.SetService(IMessageService)` で実装を差し替え。`MessageService.Measure(...)` で処理時間計測（`using` で囲んで自動ログ）。
  - `TBird.Maui.MauiMessageService`（`ConsoleMessageService` 継承）— Android 実装。コンストラクタにアプリ名を渡す（ログ行に `[アプリ名]` プレフィックス付与）。
- **DI / 初期化**: `MauiProgram.cs` の起動時に **一度だけ** `MessageService.SetService(new MauiMessageService("NewReleaseChecker"))` を呼ぶ（DI 登録ではなく静的ファサードの差し替え）。以降アプリ全体から `MessageService.*` を呼べる。
- **出力先 / レベル**:
  - 全レベル → Android logcat（`System.Diagnostics.Debug.WriteLine`）。`Debug` レベルは `#if DEBUG` または `CoreSetting.Instance.IsDebug` 時のみ出力。
  - `Error` / `Exception` のみ → **ファイル** `FileSystem.AppDataDirectory/log/yyyy-MM-dd.log`（日付ごとに自動分割＝日次ローテーション。追記方式。書込失敗は握り潰す）。Info/Warn/Debug はファイルに残らない点に注意。
- **使い方の例**:
  ```csharp
  using TBird.Core;
  MessageService.Info($"新刊チェック開始: 手動, 対象={count}件");
  try { /* ... */ }
  catch (Exception ex) { MessageService.Exception(ex); }   // 呼出元は自動付与
  using (MessageService.Measure("CheckNewReleasesAsync")) { /* 計測対象 */ }
  ```
- **要件イベントの記録に必須レベルの目安**: 要件 §6.6 のうち「API通信エラー / DB操作エラー / バックグラウンド失敗・リトライ」は**ファイルに残す必要があるため `Error` または `Exception`** で出すこと（Info だと logcat のみで端末に残らない）。「チェック開始/終了・対象件数・検知件数・通知発行・タスク起動」は `Info` でよい。
- **記録対象イベント（要件で確定済み）**: 新刊チェック開始/終了（手動・自動別、対象シリーズ数、検知件数）/ API通信エラー（ステータス・対象シリーズ）/ 通知発行 / バックグラウンドタスク起動・失敗・リトライ / DB操作エラー。

### UI通知基盤（トースト・ダイアログ等）
> 既存資産の対応状況に**濃淡がある**。エラー状態表示・ダイアログ・通知許可は既存で賄えるが、**トースト/スナックバー専用ラッパーは現状リポジトリに無い**ため、そこだけ新規パッケージ（CommunityToolkit.Maui）を導入する。
- **エラー状態のインライン表示（検索画面エラー等）**: `TBird.Maui.ViewModels.ErrorAwareViewModel` を基底に使う。`[ObservableProperty]` で `HasError` / `ErrorMessage` を公開し、`SetError(msg)` / `ClearError()` を protected で提供。View 側は `HasError` をトリガーにエラーバナーを表示する（要件 §6.7「シリーズ検索失敗＝画面にメッセージ」はこれで実装）。検索画面 VM は `ErrorAwareViewModel` を継承すること。
- **トースト/スナックバー（手動チェック失敗時など）**: **既存ラッパー無し**。`CommunityToolkit.Maui`（`Toast` / `Snackbar`）を App プロジェクトに追加して使う。`builder.UseMauiCommunityToolkit()` を `MauiProgram` で呼ぶ。将来差し替え可能にするため、`Core/Abstractions` に `IUserNotifier`（例: `ShowToastAsync(string)` / `ShowSnackbarAsync(string)`）を定義し、App 層で CommunityToolkit 実装を DI 登録すること（要件「汎用部品はインターフェース越し」方針）。
  - ※ NuGet 表（§2）には未記載。追加時に `CommunityToolkit.Maui`（net9 対応の最新安定版）を §2 の表へ追記すること。
- **ダイアログ（DB致命エラー・確認ダイアログ）**: `Shell.Current.DisplayAlert(title, message, accept, cancel)` を直接使う（既存 `NotificationPermissionService` / `MauiMessageService` も同方式）。**Shell ナビゲーション構成が前提**。`IMessageService.Confirm` は同期 bool のため MAUI の async ダイアログに適合せず、`MauiMessageService` では未実装（常に true）。確認ダイアログが要るときは `DisplayAlert` を直接 await すること（`Confirm` は使わない）。
- **通知許可（POST_NOTIFICATIONS）**: `TBird.Maui.NotificationPermissionService<TPermission>`（名前空間は `TBird.Maui`。旧記載の `TBird.Maui.Services` は実コードと不一致だったため訂正）を使う。App 側で `Permissions.BasePlatformPermission` 派生の `PostNotificationsPermission` を型パラメータに渡し、`await svc.EnsureRequestedAsync()` を起動時（§6.2）に呼ぶ。1セッション1回・Rationale 表示付き。これは「OS通知許可」であり、下記のローカル通知発行（Plugin.LocalNotification）とは別物。

### その他の流用ライブラリ
> 本リポジトリには Android MAUI 向けの自作基盤が既に揃っている。**新規実装の前に必ず流用可否を検討する**こと。App / Core / Data は以下を ProjectReference する。

- **TBird.Maui.DB**（`netstandard2.0`）— SQLite (sqlite-net-pcl) 基底＋マイグレーション枠組み。
  - `SqliteDatabaseBase` を継承して `NewReleaseChecker.Data.Database` の DB クラスを作る。`CreateTablesAsync` / `ReadSchemaVersionAsync` / `WriteSchemaVersionAsync` を override（必須）。`SeedAsync` / `GetMigrations` は必要時のみ override。
  - `Connection` は常に非 null・同期アクセス可（Repository は DI コンストラクタで即取得）。各クエリメソッド先頭で `EnsureInitializedAsync()` を呼ぶ規約（多重呼び出し可、初回のみ実行）。`SQLitePCLRaw.bundle_green` の明示初期化は不要。
  - → 要件 §6.2「テーブル未作成なら作成（マイグレーション）」・§5 のテーブル定義はこれで実装。`Series` / `Book` を sqlite-net 属性付きモデルにし、`Book.ItemNumber` は `[Unique]`、`Book.SeriesId` / `Book.ReleaseDate` に `[Indexed]`。`Book.SeriesId` は `int?`（NULL 許容＝発掘導線の単発お気に入り巻）。
- **TBird.Maui.Background**（`net9.0-android`）— 優先度ジョブキュー / ネットワーク監視 / サイト別レートリミッタ。**全て Singleton 登録必須**（`AddTransient`/`AddScoped` 禁止＝ハンドラリーク）。
  - `SiteRateLimiter` — サイト別に最小間隔を強制。要件 §6.1 / §7.6「1シリーズごとに1秒以上」の**レート制限はこれを流用**（自前 `Task.Delay` を散らさない）。コンストラクタで全 siteKey を事前登録する規約（未登録 siteKey は `ArgumentException`）。
  - `MauiNetworkPolicy`（`INetworkPolicy` 実装）— ネットワーク接続監視。WorkManager の Constraints とは別に、アプリ前景での手動チェック時の接続判定に流用可（`IsOnline` / `IsWifiConnected` / `WifiConnected`・`WifiDisconnected` イベント）。※ライブラリに `NetworkPolicyService` という具象は存在しない。利用側は `INetworkPolicy` 抽象または `MauiNetworkPolicy` を直接 DI する。
  - `PriorityJobQueue<TJob, TKey>` — 重複排除付き優先度キュー。手動チェックの逐次実行制御に流用検討（必須ではない）。
- **TBird.Maui.Web**（`net8.0`）— HTTP transient エラー処理＋AngleSharp 解析。
  - `TransientHttpErrorHelper.IsTransient` — 5xx / 408 / 429 / ステータスなし（DNS/SSL/ソケット層）を transient 判定（4xx は非リトライ）。楽天API の**リトライ要否判定に流用**（要件 §7.4 自動チェックの指数バックオフ補助）。
  - `HttpRequestFailureLogger.Log` — InnerException を最大5段まで展開して `MessageService.Error` 出力。Android の抽象的な例外メッセージ対策。API通信エラーログ（§6.6）に流用。
  - `AngleSharpHelper` — HTML 解析ヘルパ。**楽天API は JSON 応答なので本アプリでは通常不要**（HTML スクレイピングをしない限り使わない）。
- 参照の向き: `Data` → TBird.Maui.DB / TBird.Maui.Web、`App`/`Core` → TBird.Core / TBird.Maui、レート制限・ネット監視が要る層 → TBird.Maui.Background。`net8.0`（Web）・`netstandard2.0`（DB）は `net9.0-android` から問題なく参照可能。
- **WorkManager 本体**（`Xamarin.AndroidX.Work.Runtime`）は流用基盤に**含まれない**新規導入。`Platforms/Android` に `Worker` 実装を置き、共通チェックサービス（§7.1）を呼ぶ。TBird.Maui.Background はあくまで前景キュー/レート制御であり、OS の定期起動（PeriodicWorkRequest）は WorkManager で別途実装する。
  - **Worker の依存解決**: Worker は Android ランタイムが生成しコンストラクタ DI が効かないため、共通チェックサービス・DB・APIクライアント・`SiteRateLimiter`（Singleton）等は `IPlatformApplication.Current.Services` から解決する（前景と同一の Singleton インスタンスを共有）。

---

## 6. 秘密情報の取り扱い（重要）

> 2026 年新仕様への対応で楽天 API は中継サーバー（NewReleaseChecker.Relay）経由に移行（`Android引き継ぎメモ.md`）。**楽天 applicationId / accessKey は Android 側で保持しない**（中継サーバーが保持）。Android が持つ秘密情報は**中継サーバーとの共有シークレット `RelayServerApiKey` のみ**。

- `RelayServerApiKey` を `Secrets` クラスに集約し、全 API リクエストの `X-Relay-Auth` ヘッダで送る（`MauiProgram` で HttpClient のデフォルトヘッダに設定）。値は中継サーバー側 `appsettings.Secrets.json` の `RelayAuth:SharedSecret` と**一致させる**。
- `Secrets.cs` は **`.gitignore` で Git 管理から除外**する。リポジトリには `Secrets.cs.example`（ダミー値の見本）のみコミット。
- **キー値はこのファイルにも要件定義書にも書かない**。開発者がローカルの `Secrets.cs` に直接配置する。
- `Secrets` は `ISecretsProvider` 越しに参照し、将来「環境変数/CI Secretsから読む実装」へ差し替え可能にする。

```csharp
// 例: Core/Abstractions/ISecretsProvider.cs
public interface ISecretsProvider
{
    string RelayServerApiKey { get; } // 中継サーバーとの共有シークレット（X-Relay-Auth で送信）
}

// 例: NewReleaseChecker.App/Secrets.cs （.gitignore 対象。コミットしない）
internal sealed class Secrets : ISecretsProvider
{
    public string RelayServerApiKey => "ここに中継サーバー側と同じ共有シークレットを記入";
}
```

`.gitignore` に以下を追加すること:
```
**/Secrets.cs
```

---

## 7. 中核ロジックの実装指針（最重要）

要件定義書 §3.2・§8 と対応。ここを誤ると致命的なので必ず守ること。

### 7.1 チェック処理は共通サービス化
「タイトル検索 → 除外フィルタ → 著者集合一致判定 → 差分判定 → INSERT → お気に入り自動登録 → 通知」を**単一の共通サービス**として実装し、自動（WorkManager）・手動（更新ボタン）の両方から呼ぶ。手動チェックもバックグラウンド実行で、検知時は通知を出す。

### 7.2 シリーズ同定（著者集合の一致判定）
- チェック時は**シリーズキー（タイトル語）をキーワードに検索**して候補巻を取得し、`正規化後の 登録時著者集合 = 検索結果側の著者集合`（**集合一致**。部分集合 ⊆ ではない）で同定する。著者名は検索キーにしない（同一著者の別シリーズ誤統合を防ぐ）。
- 著者文字列は区切り文字（`/` `,` 空白等）で分割して集合化。
- 比較前に**正規化**（肩書きラベル「原作/作画/イラスト」等の除去・空白除去・全角/半角統一）。**人物名は除去せず保持**し、作画者/イラストレーターの差異でコミカライズを別シリーズとして弁別する。一致(=)方式は著者欄の表記揺れに敏感なため正規化規則は実レスポンスで調整（要件 §3.2.1 / §8 検証事項）。
- 巻数は**抽出も保持もしない**。種別フラグ（本編/外伝/短編集）も**持たない**。
- 副題の有無でシリーズを分けない（タイトル差異ではなく著者集合の一致で同定）。

### 7.3 新刊判定と既存巻更新
- 同定キーは **ItemNumber（Kobo ITEM番号、UNIQUE）**。DBに無い ItemNumber が新刊。
- 新刊 INSERT 時に `IsFavorite=1` をセット。`IsNewDetected=1` は**予約検知（未発売）の新刊のみ**（発売確定の新刊は通知しないため 0 のまま＝降りない固定を防ぐ）。
- 既存巻は毎回書誌情報を上書き更新する。**ただし `IsPurchased / IsFavorite / IsCalendarRegistered / IsNewDetected / DetectedAt` は絶対に上書きしない**。
- `SeriesId` は ItemNumber 全体 UNIQUE のため1巻1シリーズ。SeriesId 設定済みの既存巻は別シリーズのチェックでも変更しない。`SeriesId=NULL`（発掘導線の単発巻）が同定ヒットした時のみ当該シリーズを設定。
- 予約/発売確定はカラムを持たず、`ReleaseDate` と現在日時の比較で都度判定。通知は**予約検知時のみ**。
- 通知発行後に `IsNewDetected` を降ろす。複数検知時は通知を**1件に集約**。

### 7.4 除外フィルタ
- タイトルに除外キーワード（Preferences `exclude_keywords`、初期 `["分冊","単話","話売り"]`）を含む巻は DB に取り込まない。

### 7.5 ReleaseDate
- API の発売日文字列をパースして **ISO8601（"yyyy-MM-dd"）へ正規化**。パース不能時 NULL。
- NULL はソート末尾。予約判定の対象外。

### 7.6 バックグラウンドチェック（ローテーション）
- WorkManager `PeriodicWorkRequest`。ネットワーク必須制約＋指数バックオフ。
- 1回の Work で**最大50シリーズ**（`MaxSeriesPerWork` 定数）。`LastCheckedAt` が NULL を最優先、次いで古い順。対象外は次回 Work へ繰り越し。
- 1シリーズごとに**1秒以上**の間隔（レート制限対策）。
- 10分の実行時間制限内に収める（フォアグラウンドサービス化しない）。

---

## 8. データモデル（要約）

DB は SQLite（sqlite-net-pcl）。テーブルは **Series / Book の2つのみ**。設定は MAUI `Preferences`。

- **Series**: Id, SeriesKey, AuthorSet, MediaType("novel"/"comic"), RegisteredAt, LastCheckedAt
- **Book**: Id, SeriesId(FK, **NULL 許容**＝発掘導線でお気に入り等した単発巻は NULL), ItemNumber(UNIQUE), Isbn, Title, Author, Publisher, ReleaseDate, ImageUrl, ItemUrl, Caption, IsPurchased, IsFavorite, IsCalendarRegistered, IsNewDetected, DetectedAt
- インデックス推奨: `Book.SeriesId`, `Book.ReleaseDate`（ItemNumber は UNIQUE で自動）
- シリーズ削除は**アプリ側ロジックで明示カスケード**（当該 SeriesId の Book を先に削除→Series。`SeriesId=NULL` の単発お気に入り巻は影響を受けない）。
- ランキング・発売予定表（DB未保存）由来の巻にお気に入り/購入済/カレンダー操作した時点で `SeriesId=NULL` で INSERT。お気に入り一覧では「未追跡」として表示（要件 §F-010/§F-014）。

詳細は要件定義書 §5。

---

## 9. 外部API（楽天ウェブサービス）

> 2026 年新仕様により Android から楽天 API への直接アクセスは不可。**すべて中継サーバー（NewReleaseChecker.Relay, `https://kaz.server-on.net:60344`）経由**で利用する（`Android引き継ぎメモ.md` / `中継サーバー_要件定義書.md` 参照）。

- **楽天Kobo電子書籍検索API**: 検索・新刊チェック・発売予定表・ランキング。中継の `POST /api/kobo/search` を叩く（本文 JSON のキー名・値は楽天クエリと 1:1。`formatVersion` は付けず既定の `Items[].Item` ラップ構造＝DTO と整合）。REST/JSON、標準HttpClient + System.Text.Json。
- **楽天Koboジャンル検索API**: 発売予定表・ランキングのジャンルメニュー生成。中継の `POST /api/kobo/genres`。
- 認証: 中継サーバーとの共有シークレット `X-Relay-Auth`（Secrets 経由）。楽天 applicationId/accessKey/Referer/Origin は中継サーバーが付与する。
- レート制限: リクエスト間隔1秒以上（`SiteRateLimiter`。siteKey=`relay-kobo`）、タイムアウト20秒（中継→楽天の上流15秒＋余裕）。中継側でも上限超過時は 429（透過）。
- レスポンスは中継サーバーが透過するため、楽天 API のレスポンス DTO・パース処理はそのまま使える（無改修）。

### ⚠️ 実装時に公式ドキュメントで確認すべき項目
1. 正確なエンドポイントURL・APIバージョン。
2. パラメータ名（`koboGenreId`, `salesType`, `sort` 等）と値。
3. レスポンスの発売日フィールド名・形式（和暦/未定文字列の有無）→ パース・正規化を調整。
4. 著者フィールドの区切り文字・肩書き混入の有無 → 分割・正規化を調整。
5. ランキング取得用 sort 値、ジャンルID体系。
6. Koboアプリへの URL スキーム直接遷移の可否。

---

## 10. 画面（要約）

ボトムタブ5つ: シリーズ / お気に入り / 発売予定 / ランキング / 設定。
全11画面（SCR-001〜011）の詳細・遷移は要件定義書 §4。

- 巻詳細（SCR-006）はシリーズ・ランキング・発売予定表から**兼用**。追跡/お気に入り状況でアクションを動的切替。
- カレンダー追加は**巻詳細でのみ**（一覧はバッジ表示のみ）。未発売なら常時表示。インテント方式（OAuth不要）。
- シリーズ一覧は新刊バッジではなく**未購入件数バッジ**（0冊は非表示）。

---

## 11. やってはいけないこと（アンチパターン）

- ❌ ユーザーフラグ列（IsPurchased 等）を書誌更新で上書きする。
- ❌ 巻数を抽出・保持する／種別フラグを実装する（要件で撤廃済み）。
- ❌ 副題の有無でシリーズを分ける。
- ❌ 秘密情報（applicationId 等）をコミットする／このファイルや要件定義書に書く。
- ❌ ViewModel コンストラクタで非同期処理を行う。
- ❌ バックグラウンドからカレンダー登録を試みる（インテント方式は前景のみ）。
- ❌ 1回の Work で全シリーズを無制限にチェックする（10分制限・レート制限に抵触）。
- ❌ 自動チェック失敗時にユーザー通知を出す（ログのみ）。
- ❌ Android アプリから楽天 API（`openapi.rakuten.co.jp` 等）に直接アクセスする（必ず中継サーバー経由）。
- ❌ Android アプリで楽天 `applicationId` / `accessKey` を保持する（中継サーバー側のみが持つ）。
- ❌ Android アプリで Referer / Origin ヘッダを操作する（中継サーバー任せ）。

---

## 12. 未決事項（将来課題・実装に影響しない）

| # | 項目 | 扱い |
|---|---|---|
| TBD-001 | バックアップ/リストア | 将来追加。当面は再登録で対応 |
| TBD-002 | Google Play 公開 | 公開を決めた段階で別途 |
| TBD-003 | アフィリエイトID利用 | 初期空。設定すれば自動付与 |
| TBD-004 | カレンダー自動化（OAuth） | インターフェースで差し替え可能 |
| TBD-005 | チェック優先度 | 現状は全シリーズ平等ローテーション |
