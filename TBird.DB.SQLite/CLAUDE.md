# TBird.DB.SQLite

SQLiteデータベースプロバイダー実装（`System.Data.SQLite` ベース、`DbControl` を継承、TFM: `netstandard2.0`）。

## 主要クラス（`_ROOT/`）

- `SQLiteControl`（`DbControl` 継承, partial）
  - 簡易コンストラクタ: `SQLiteControl(datasource, password, readonly, pooling, cachesize, extension)`
  - 接続文字列コンストラクタ: `SQLiteControl(connectionString)`（`key=value;...` 形式）
  - 内部 `Manager` クラスが connectionString 単位で接続を共有（参照カウント方式のプーリング。`CreateConnection` で参照を増やし `Close()` で減らす。0 になると物理接続をクローズ）
- `SQLiteUtil`（static）:
  - `CreateParameter(DbType, value)` / `CreateParameter(DbType, name, value)` — `SQLiteParameter` 生成
  - `ToEscape(value)` — LIKE 句用に `\` `%` `_` をエスケープ
  - `ExistsColumn(this SQLiteControl, table, column)` — `PRAGMA_TABLE_INFO` で列存在チェック
  - `BackupAsync(SQLiteControl src, path)` — `LockAsync` 取得後に `BackupDatabase` でファイルバックアップ

## 開発時の注意

- ネイティブDLL（`sqlite3.exe`, `extension-functions-32/64.dll`）が出力ディレクトリへコピーされる（csproj で `CopyToOutputDirectory=Always`）。コピー漏れは拡張関数（数学関数等）／DB復旧の失敗原因になる
- `password` 指定時は暗号化（PRAGMA key）対応
- `extension=true` で `extension-functions-*.dll` をロード
- 接続は connectionString をキーに `Manager` で共有されるため、**同一 connectionString は同じ物理接続を再利用**する点に注意
