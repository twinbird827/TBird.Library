# TBird.Plugin

動的DLL読み込みによるプラグインシステム（TFM: `netstandard2.0`）。

## 主要クラス（`_ROOT/`）

- `IPlugin`（`IDisposable` 継承）: `Initialize()`（初期化）, `Run()`（定期実行処理）, `Interval`（実行間隔）
- `PluginManager`（シングルトン `Instance`）: `"plugins"` ディレクトリの DLL をリフレクションで走査し `IPlugin` 実装を検出・ロード
- `PluginExecuter`: 個々のプラグインを `IntervalTimer` でラップし、`Interval` ごとに `Run()` を駆動

## 開発時の注意

- プラグイン作成手順: `IPlugin` を実装 → DLL ビルド → `"plugins"` ディレクトリに配置 → `PluginManager` が起動時に自動検出
- ライフサイクル: `Initialize()` → `IntervalTimer` 駆動の `Run()` 繰返し → `Dispose()`。`IDisposable` を確実に実装すること
- プラグイン内の例外は `MessageService` 経由で処理される
