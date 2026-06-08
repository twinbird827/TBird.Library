# TBird.DB

データベース操作の抽象化レイヤー（プロバイダー非依存、TFM: `netstandard2.0`）。

## 主要クラス（`_ROOT/`）

- `IDbControl` / `DbControl`（abstract）: 接続・トランザクション・コマンド実行の抽象基底。新プロバイダーは `DbControl` を継承し `CreateConnection()` を実装（実装例: `TBird.DB.SQLite`, `TBird.DB.SQLServer`）
- `DbDataReaderExtension`: `DbDataReader` から行を取り出す拡張（`GetRows<T>()` / `GetRow<T>()`）
- `DbControlExtension`: `ExecuteScalarAsync<T>(sql, params)` 拡張。`ExecuteScalarAsync` の結果を `DbUtil.GetValue<T>` で型変換して返す
- `DbUtil`: 型変換等のユーティリティ（`GetValue<T>`）

## 開発時の注意

- 非同期実行は `ExecuteReaderAsync()` / `ExecuteNonQueryAsync()` / `ExecuteScalarAsync()` を使用（※過去ドキュメントの `SelectAsync` というメソッドは存在しない）。型付きスカラーが欲しい場合は `ExecuteScalarAsync<T>()` 拡張を使う
- 行マッピングは `ExecuteReaderAsync()` の結果に `GetRows<T>()` / `GetRow<T>()` 拡張を併用するのが定石
- 接続文字列は `key=value;...` 形式の独自書式で、`ToConnectionDictionary()` で辞書化してプロバイダー側が解釈する
- `DbControl` は `TBirdObject` 継承。using もしくは Dispose で確実に破棄すること（接続クローズ）
