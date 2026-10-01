# TBird.Maui.Background

優先度付きジョブキュー / ネットワーク監視 / サイト別レートリミッタ。

## 開発時の注意

- TFM は `net10.0-android`、`<UseMauiEssentials>true</UseMauiEssentials>` + `Microsoft.Maui.Essentials` PackageReference 明示（.NET 8+ で自動注入が廃止）
- `MauiNetworkPolicy` / `PriorityJobQueue` は **Singleton 前提** 設計。class header の `[DI-LIFETIME: SINGLETON]` タグで明示（`NetworkPolicyService` はライブラリに存在せず、消費アプリ側が用意するラッパーの想定名。`INetworkPolicy.cs` のコメントが参照する具象もこれを指す）
- `AddTransient` / `AddScoped` 登録は **禁止**（コンストラクタで Connectivity / INetworkPolicy のイベントを購読し解除コードを持たないため、ハンドラリーク）
- `INetworkPolicy` は当面 `PriorityJobQueue` 内部の DI 接合専用とし、アプリ層 (Repository / Service / ViewModel) が直接コンストラクタ DI で受け取ることは禁止（消費アプリ側ラッパー経由のみ）
- `PriorityJobQueue<TJob, TKey>` の `TJob` は `BackgroundJobBase` 継承が前提。優先度は `JobPriority` enum で表現し、2 本キュー（高/通常）に振り分ける
- `PriorityJobQueue<TJob, TKey>` の dedup HashSet と 2 本キューの Enqueue は同一 lock 内で完結させる（race-free 規約）
- WorkerLoop 内は `isEnabled` 再評価 + `IsWifiConnected` check の二重ゲート防御を維持（ConnectivityChanged イベント遅延発火への対策）
- `OperationCanceledException` は連続失敗カウントを上げず単純 break（StopWorker の正常キャンセル経路）
- `SiteRateLimiter` の未登録 siteKey は `ArgumentException` で fail-fast（コンストラクタで全 siteKey を事前登録する規約）
- TBird.Maui.Web を ProjectReference（`SiteRateLimiter` が `HttpRequestFailureLogger` を利用するため）
