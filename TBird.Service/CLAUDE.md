# TBird.Service

Windowsサービス基盤（コンソールフォールバック付き、TFM: .NET Framework 4.8）。

## 主要クラス（`_ROOT/`）

- `ServiceManager`（abstract, `ServiceBase` 継承）: `IntervalTimer` の async コールバック駆動。未初期化なら `await StartProcess()` → 成功後は毎回 `await TickProcess()`。`TickProcess` 内で未捕捉例外が出たら `StopProcess()` して `_startasync=false` に戻し、次周期で再初期化する自己回復パターン
  - 派生側のシグネチャ: `StartProcess()` は `virtual Task<bool>`（既定 true、開始処理）、`TickProcess()` は **abstract `Task`**（必須実装）、`StopProcess()` は `virtual void`（既定 no-op）
  - `ToStartResult(bool)` は `StartProcess` の戻り値を組み立てるヘルパ
- `ServiceRunner`: エントリポイント。`Environment.UserInteractive` を見てコンソール実行とサービス実行を切替。`/i` インストール・`/u` アンインストールを処理
- `ServiceSetting`（シングルトン）: ServiceName / DisplayName / Interval / StartType 等
- `ServiceMessageService`: `IMessageService` の EventLog 実装

## 開発時の注意

- .NET Framework 4.8 のレガシー形式 csproj（SDK形式でない、`ServiceBase` / `ServiceProcessInstaller` のため）
- サービス作成: `ServiceManager` を継承し最低限 `TickProcess()`（abstract）を実装。初期化が要るなら `StartProcess()` も override。`ServiceRunner.Run(instance, args)` を Main から呼ぶ
- 同一 exe をコンソール（対話）でも実行可能（デバッグ用）。`/i` インストール・`/u` アンインストールは要管理者権限
