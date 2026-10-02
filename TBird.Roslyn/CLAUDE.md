# TBird.Roslyn

Roslynコンパイラを使用したC#スクリプティング機能（`.csx` 実行, TFM: `netstandard2.0`）。

## 主要クラス（`_ROOT/`、各 `_dispose.cs` partial で破棄処理を分離）

- `RoslynManager`（シングルトン `Instance`）: `Initialize<T>(parameter)` で `"scripts"` ディレクトリ内の全 `*.csx` をロード、`Add<T>(path, parameter)` で個別追加、`RunAsync()` で全実行（`Task`）、`RunBackground()` は `RunAsync` を待たずに走らせる `async void` ラッパー
- `IRoslynExecuter`（`IDisposable` 継承、`Task RunAsync()`）/ `RoslynExecuter<T>`: 個々の `.csx` スクリプトをコンパイル・実行（`CSharpScript`）。`RoslynManager` は `IList<IRoslynExecuter>` として保持
- `RoslynObject<T>`: スクリプトへ渡すコンテキスト（対象オブジェクト＋タスク管理）
- `RoslynSetting`: 既定 import / 実行間隔等の設定

## 開発時の注意

- 利用側プロジェクトでは FluentValidation の不要カルチャーを除外する csproj 設定が必要（[roslyntest/roslyntest.csproj](../roslyntest/roslyntest.csproj) の `FluentValidationExcludedCultures` を参照。出力 `.csx` のサイズ肥大を防ぐ）
- 使用フロー: `.csx` を `"scripts"` ディレクトリへ配置 → `RoslynManager.Instance.Initialize<T>(parameter)` → `RunAsync()`
- partial クラスで `_dispose.cs` に破棄処理を分離するパターンを採用
