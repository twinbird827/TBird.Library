# TBird.Core

全プロジェクトの基盤となるコアライブラリ（TFM: `netstandard2.0`）。

## ディレクトリ構成

- `_ROOT/` - 基盤型: `TBirdObject`（全主要型の基底, Dispose は sealed）, `Disposer`, `Locker`, `TaskManager`, `IntervalTimer`, `JsonBase`, `DynamicJson`, `Encrypter`, `Lang`, `WrappingStream`, `CoreSetting`, `PathSetting`
- `Services/` - メッセージ抽象化: `IMessageService` / `MessageService`（静的ファサード, `SetService()` で実装差し替え）/ `ConsoleMessageService` / `MessageType`
- `Extensions/` - `{型名}Extension.cs` 命名（String / Task / IEnumerable / Object / Semaphore / Dictionary / Enum 等）
- `IO/` - `FileUtil` / `DirectoryUtil` / `Directories` / `CsvUtil` / `ZipUtil` / `XmlUtil` / `FileAppendWriter` / `DynamicUtil`
- `Utils/` - `CoreUtil` / `TaskUtil` / `EnumUtil` / `EventUtil`

## 開発時の注意

- `netstandard2.0` のため利用可能 API に制限あり（新しい BCL API は使えないことがある）
- リソース解放は `TBirdObject` 継承先で `DisposeManagedResource()` / `DisposeUnmanagedResource()` を override（`Dispose` 自体は sealed）
- 排他制御は `Locker` パターンを使用
- ログ／メッセージ出力は `MessageService.Info/Warn/Error/Exception(...)` 経由。実行環境に応じ起動時に `MessageService.SetService(...)` で実装を差し替える（Console 版・Service 版・MAUI 版が各プロジェクトに存在）
- `CoreUtil.Execute(params ProcessStartInfo[])`（パイプ版）はいずれかの段が非ゼロ exit code で終了すると `InvalidOperationException` を投げる（silent failure 防止, issue #201）
- `CoreUtil.ExecuteAsync` / sync `Execute(ProcessStartInfo, Action<string>?)` は exit code を返すだけで、非ゼロでも throw しない（検査は呼び出し元の義務）。`action` 内の例外は終了後に再スローされ、起動失敗は `Win32Exception` が伝播する
