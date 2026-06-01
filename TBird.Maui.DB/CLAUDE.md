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
- 全 migration 成功時のみ `WriteSchemaVersionAsync` を 1 回呼ぶ（途中失敗時は次回起動で再試行）
- `SQLitePCLRaw.bundle_green` の module initializer が自動で `SQLitePCL.Batteries_V2.Init()` を呼ぶため、明示初期化は不要
