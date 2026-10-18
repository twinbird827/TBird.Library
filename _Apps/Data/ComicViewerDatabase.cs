using ComicViewer.Models;
using SQLite;
using TBird.Maui.DB;

namespace ComicViewer.Data;

public class ComicViewerDatabase : SqliteDatabaseBase
{
    public ComicViewerDatabase() : base(Path.Combine(FileSystem.AppDataDirectory, "ComicViewer.db3"), 1)
    {
    }

    protected override async Task CreateTablesAsync(SQLiteAsyncConnection conn)
    {
        await conn.CreateTableAsync<SmbSource>().ConfigureAwait(false);
        await conn.CreateTableAsync<Favorite>().ConfigureAwait(false);
        await conn.CreateTableAsync<ReadingState>().ConfigureAwait(false);
        await conn.CreateTableAsync<FolderKind>().ConfigureAwait(false);
        await conn.CreateTableAsync<PdfCache>().ConfigureAwait(false);
    }

    // 値を返す PRAGMA を ExecuteAsync すると sqlite-net が "not an error" で必ず落ちる。
    protected override Task<int> ReadSchemaVersionAsync(SQLiteAsyncConnection conn)
        => conn.ExecuteScalarAsync<int>("PRAGMA user_version");

    protected override Task WriteSchemaVersionAsync(SQLiteAsyncConnection conn, int version)
        => conn.ExecuteAsync($"PRAGMA user_version = {version}");

    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
