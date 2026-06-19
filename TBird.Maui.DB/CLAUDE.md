# TBird.Maui.DB

SQLite (sqlite-net-pcl) 用の DB 基底クラスとマイグレーション枠組み。

## 開発時の注意

- TFM は `netstandard2.0`（MAUI Android 以外のプロジェクトからも再利用可能）
- `SqliteDatabaseBase` を継承し、`CreateTablesAsync` / `ReadSchemaVersionAsync` / `WriteSchemaVersionAsync` を必ず override する
- `SeedAsync` / `GetMigrations` は virtual 空実装、必要に応じて override
- `Connection` プロパティは常に非 null・同期アクセス可能（Repository が DI コンストラクタで即時取得する規約のため、例外スローしてはならない）
- スキーマ準備は呼出側が各クエリメソッド先頭で `EnsureInitializedAsync()` を呼ぶ規約（多重呼び出し OK、初回のみ実行）
- `IMigration.FromVersion` のセマンティクス: 「この migration が想定する開始バージョン」= 実行すると DB が `FromVersion` → `FromVersion + 1` に上がる
- migration は冪等であること（`CREATE INDEX IF NOT EXISTS` / `DROP INDEX IF EXISTS` / `INSERT OR IGNORE`）
- `EnsureInitializedAsync` は faulted/canceled の init Task を**再実行**する設計（`SqliteDatabaseBase.cs:49-64`、実体 `InitializeInternalAsync` は `:66-94`）。起動時の一時ロック（"database is locked" 等）で 1 度失敗しても以後のクエリが永続固着せず自己回復させるため。**この再試行が安全なのは `CreateTablesAsync` / `SeedAsync` / `ReadSchemaVersionAsync` が冪等で throw 後も再実行できる前提に依存する**。特に `SeedAsync` は `FindAsync`→`Insert`（または `INSERT OR IGNORE`）必須で、`InsertAsync` 直書きすると再試行で UNIQUE 例外→faulted→再試行ループに陥る（`SeedAsync` の冪等規約＝FindAsync→Insert / InsertOrReplace 禁止は `SqliteDatabaseBase.cs:102-106` の XML doc が出所。他アプリへ本ライブラリを流入させる際は各 `SeedAsync` の冪等性を 1 度確認すること）
- 全 migration 成功時のみ `WriteSchemaVersionAsync` を 1 回呼ぶ（途中失敗時は次回起動で再試行）
- `SQLitePCLRaw.bundle_green` の module initializer が自動で `SQLitePCL.Batteries_V2.Init()` を呼ぶため、明示初期化は不要
