# TBird.DB.SQLServer

SQL Serverデータベースプロバイダー実装（`Microsoft.Data.SqlClient` ベース、`DbControl` を継承、TFM: `netstandard2.0`）。

## 主要クラス（`_ROOT/`）

- `SQLServerControl`（`DbControl` 継承）: `SQLServerControl(connectionString)`。`CreateConnection()` で独自書式の接続文字列を `SqlConnectionStringBuilder` に変換
- `SQLServerUtil`: `CreateParameter(DbType type, object value)` で `SqlParameter` を生成するユーティリティ

## 開発時の注意

- 接続文字列は `key=value;...` の独自書式。解釈されるキー（[SQLServerControl.cs:16-25](_ROOT/SQLServerControl.cs#L16-L25)）:
  - `datasource`（必須, サーバ）
  - `userid` / `password`（SQL認証。省略時は Windows 認証相当）
  - `initialcatalog`（既定 `master`）
  - `connecttimeout`（既定 `15000`）
  - `trustservercertificate`（既定 `false`）
- 依存: `Microsoft.Data.SqlClient`（`System.Data.SqlClient` ではない）
