using NewReleaseChecker.Core.Models;
using SQLite;
using TBird.Maui.DB;

namespace NewReleaseChecker.Data.Database;

/// <summary>
/// 本アプリの SQLite データベース。TBird.Maui.DB.SqliteDatabaseBase を継承し、
/// Series / Book / SchemaVersion テーブルを管理する（要件 §5 / §6.2）。
/// </summary>
public sealed class NewReleaseDatabase : SqliteDatabaseBase
{
    public const int SchemaVersion = 1;

    public NewReleaseDatabase(string dbPath) : base(dbPath, SchemaVersion)
    {
    }

    /// <summary>既定 DB パス（AppData/newreleasechecker.db3）。</summary>
    public static string DefaultPath =>
        System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "newreleasechecker.db3");

    protected override async Task CreateTablesAsync(SQLiteAsyncConnection conn)
    {
        // 属性（[Unique]/[Indexed]）に従いテーブル・インデックスを作成。冪等。
        await conn.CreateTableAsync<Series>();
        await conn.CreateTableAsync<Book>();
        await conn.CreateTableAsync<SchemaVersionRecord>();
    }

    protected override async Task<int> ReadSchemaVersionAsync(SQLiteAsyncConnection conn)
    {
        var rec = await conn.Table<SchemaVersionRecord>().FirstOrDefaultAsync();
        return rec?.Version ?? 1; // 無レコード時は 1（基底の規約）
    }

    protected override async Task WriteSchemaVersionAsync(SQLiteAsyncConnection conn, int version)
    {
        await conn.ExecuteAsync("DELETE FROM SchemaVersion");
        await conn.InsertAsync(new SchemaVersionRecord { Id = 1, Version = version });
    }
}
