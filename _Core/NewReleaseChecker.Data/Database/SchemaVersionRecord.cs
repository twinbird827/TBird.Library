using SQLite;

namespace NewReleaseChecker.Data.Database;

/// <summary>スキーマバージョンを 1 行で保持するメタテーブル。</summary>
[Table("SchemaVersion")]
public sealed class SchemaVersionRecord
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public int Version { get; set; }
}
