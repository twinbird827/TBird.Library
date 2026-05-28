using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using TBird.Core;

namespace TBird.Maui.DB;

/// <summary>
/// SQLite (sqlite-net-pcl) を使う MAUI アプリ向けの DB 基底クラス。
///
/// 派生クラスは以下を実装する:
///   - <see cref="CreateTablesAsync"/> (必須): テーブル作成 + 既存カラム追加 + INDEX 再適用
///   - <see cref="SeedAsync"/> (任意): 既定値シード（既存値を上書きしないこと）
///   - <see cref="GetMigrations"/> (任意): IMigration 配列を FromVersion 昇順で返す
///   - <see cref="ReadSchemaVersionAsync"/> / <see cref="WriteSchemaVersionAsync"/> (必須):
///     スキーマバージョンの永続化先（既存ユーザ DB との互換性確保のため abstract）
///
/// 接続オブジェクトは <see cref="Connection"/> から常に非 null・同期アクセス可能。
/// 初期化前後のチェックや例外スローは行わない（Repository の DI 構築時クラッシュ防止）。
/// スキーマ準備の保証は呼出側が <see cref="EnsureInitializedAsync"/> をクエリ前に呼ぶ規約とする。
/// </summary>
public abstract class SqliteDatabaseBase
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly int _currentSchemaVersion;
    private Task? _initTask;
    private readonly object _initLock = new();

    /// <summary>
    /// </summary>
    /// <param name="dbPath">SQLite ファイルの絶対パス（呼び出し側で FileSystem.AppDataDirectory 等から構築）</param>
    /// <param name="currentSchemaVersion">最新のスキーマバージョン</param>
    protected SqliteDatabaseBase(string dbPath, int currentSchemaVersion)
    {
        _connection = new SQLiteAsyncConnection(dbPath);
        _currentSchemaVersion = currentSchemaVersion;
    }

    /// <summary>常に非 null。コンストラクタ生成時点で同期アクセス可能（例外スローしない）。</summary>
    public SQLiteAsyncConnection Connection => _connection;

    /// <summary>
    /// 初回のみ実際の初期化を行う。複数箇所から呼ばれても 1 回しか走らない。
    /// クエリ実行前に必ず呼び出すこと。
    /// </summary>
    public Task EnsureInitializedAsync()
    {
        lock (_initLock)
        {
            return _initTask ??= InitializeInternalAsync();
        }
    }

    private async Task InitializeInternalAsync()
    {
        // 1. テーブル作成 + 既存カラム追加 + INDEX 再適用を派生に委譲
        await CreateTablesAsync(_connection).ConfigureAwait(false);

        // 2. 既定設定のシード（既存値は上書きしない冪等実装が派生側の責務）
        await SeedAsync(_connection).ConfigureAwait(false);

        // 3. スキーマバージョンを読み、必要な migration を順次適用
        var currentVersion = await ReadSchemaVersionAsync(_connection).ConfigureAwait(false);
        if (currentVersion < _currentSchemaVersion)
        {
            MessageService.Info($"Schema migration: v{currentVersion} -> v{_currentSchemaVersion}");
            try
            {
                foreach (var migration in GetMigrations().Where(m => m.FromVersion >= currentVersion))
                {
                    await migration.ExecuteAsync(_connection).ConfigureAwait(false);
                }
                // 全 migration 成功時のみ SetSchemaVersion を 1 回呼ぶ。
                // 途中失敗時は次回起動で再試行される（migration は冪等規約）。
                await WriteSchemaVersionAsync(_connection, _currentSchemaVersion).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageService.Warn($"Schema migration failed, will retry next launch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// CreateTableAsync 呼び出し + EnsureColumnAsync による既存カラム追加 + INDEX 再適用を行う。
    /// 初回 (テーブル無) でもアプリ起動するため、冪等な書き方とすること。
    /// </summary>
    protected abstract Task CreateTablesAsync(SQLiteAsyncConnection conn);

    /// <summary>
    /// 既定値シード。既存ユーザ DB を壊さないため、FindAsync で存在チェックしてから
    /// InsertAsync する規約（InsertOrReplace は禁止）。デフォルトでは何もしない。
    /// </summary>
    protected virtual Task SeedAsync(SQLiteAsyncConnection conn) => Task.CompletedTask;

    /// <summary>
    /// migration 定義。FromVersion 昇順で返す責務は派生側にある。
    /// デフォルトでは空配列を返す（v1 から動かないシンプル DB は override 不要）。
    /// </summary>
    protected virtual IReadOnlyList<IMigration> GetMigrations() => Array.Empty<IMigration>();

    /// <summary>
    /// 現在の DB のスキーマバージョンを返す。無レコード時は 1 を返すこと
    /// （0 や currentSchemaVersion を返すと既存挙動と乖離する）。
    /// </summary>
    protected abstract Task<int> ReadSchemaVersionAsync(SQLiteAsyncConnection conn);

    /// <summary>
    /// スキーマバージョンを永続化する。
    /// </summary>
    protected abstract Task WriteSchemaVersionAsync(SQLiteAsyncConnection conn, int version);

    /// <summary>
    /// PRAGMA table_info で既存カラムの有無を確認し、未存在なら ALTER TABLE ADD COLUMN を実行する。
    /// </summary>
    protected async Task EnsureColumnAsync(SQLiteAsyncConnection conn, string table, string column, string ddlSuffix)
    {
        try
        {
            var cols = await conn.QueryAsync<PragmaColumnInfo>($"PRAGMA table_info({table})").ConfigureAwait(false);
            if (cols.Any(c => string.Equals(c.name, column, StringComparison.OrdinalIgnoreCase))) return;
            await conn.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {ddlSuffix}").ConfigureAwait(false);
            MessageService.Info($"Added column {table}.{column}");
        }
        catch (Exception ex)
        {
            MessageService.Warn($"EnsureColumnAsync {table}.{column} failed: {ex.Message}");
        }
    }

    private class PragmaColumnInfo
    {
        public int cid { get; set; }
        public string name { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public int notnull { get; set; }
        public string? dflt_value { get; set; }
        public int pk { get; set; }
    }
}
