# TBird.Console

コンソールアプリケーションの基底クラスライブラリ（TFM: `net8.0`）。

## 主要クラス（`_ROOT/`）

- `ConsoleExecuter`（abstract, `TBirdObject` 継承）: エントリポイント `Execute(string[] args)` が引数解析→`Process()` 呼出→例外時 `Pause()` を行う
- `ConsoleAsyncExecuter`（abstract, `ConsoleExecuter` 継承）: 非同期版。`Process` は sealed 済み、代わりに `ProcessAsync(options, args)` を override

## 開発時の注意

- 同期処理は `ConsoleExecuter` を継承し **`Process(Dictionary<string,string> options, string[] args)`** を実装。非同期は `ConsoleAsyncExecuter` を継承し **`ProcessAsync(...)`** を実装（`MainExecute` というメソッドは存在しない）
- 引数解析（[ConsoleExecuter.cs:9](_ROOT/ConsoleExecuter.cs#L9), [L32-L39](_ROOT/ConsoleExecuter.cs#L32-L39)）:
  - **`/` 始まりがオプション**（`/KEY=VALUE` 形式、キーは大文字化されて `options` 辞書へ。値なしは空文字）
  - `/` なしはパラメータ（`args` 配列へ）
- 補完が必要なオプションは `GetOptions()` を override し `SetOption()` で対話入力を促す
- 終了コードは `GetErrorCode(ex)` を override（既定 -1）
- 例外時は `System.Console.Read()` で一時停止する。**`/H` オプション付きで起動すると停止しない**（ヘッドレス/バッチ用、[L77-L80](_ROOT/ConsoleExecuter.cs#L77-L80)）
