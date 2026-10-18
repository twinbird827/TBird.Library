using SQLite;

namespace ComicViewer.Models;

[Table("SmbSource")]
public class SmbSource
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = "";

    [NotNull]
    public string Host { get; set; } = "";

    public int Port { get; set; } = 445;

    [NotNull]
    public string UserName { get; set; } = "";

    [NotNull]
    public string Share { get; set; } = "";

    [NotNull]
    public string RootPath { get; set; } = "";

    [Ignore]
    public string SourceKey => $"smb:{Id}";
}

[Table("Favorite")]
public class Favorite
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed(Name = "UX_Favorite_Source_Path", Order = 1, Unique = true)]
    public string SourceKey { get; set; } = "";

    [NotNull, Indexed(Name = "UX_Favorite_Source_Path", Order = 2, Unique = true)]
    public string Path { get; set; } = "";

    [NotNull]
    public string Name { get; set; } = "";

    public long CreatedAt { get; set; }
}

[Table("ReadingState")]
public class ReadingState
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed(Name = "UX_ReadingState_Source_Path", Order = 1, Unique = true)]
    public string SourceKey { get; set; } = "";

    [NotNull, Indexed(Name = "UX_ReadingState_Source_Path", Order = 2, Unique = true)]
    public string Path { get; set; } = "";

    public int PageIndex { get; set; }

    public int PageCount { get; set; }

    public bool IsRead { get; set; }

    [Indexed(Name = "IX_ReadingState_LastOpenedAt")]
    public long? LastOpenedAt { get; set; }
}

[Table("FolderKind")]
public class FolderKind
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed(Name = "UX_FolderKind_Source_Path", Order = 1, Unique = true)]
    public string SourceKey { get; set; } = "";

    [NotNull, Indexed(Name = "UX_FolderKind_Source_Path", Order = 2, Unique = true)]
    public string Path { get; set; } = "";

    /// <summary>0 = 閲覧用フォルダ、1 = 画像だけの漫画</summary>
    public int Kind { get; set; }
}

[Table("PdfCache")]
public class PdfCache
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed(Name = "UX_PdfCache_Source_Path", Order = 1, Unique = true)]
    public string SourceKey { get; set; } = "";

    [NotNull, Indexed(Name = "UX_PdfCache_Source_Path", Order = 2, Unique = true)]
    public string Path { get; set; } = "";

    public long Size { get; set; }

    public long ModifiedAt { get; set; }

    [NotNull]
    public string LocalFile { get; set; } = "";

    [Indexed(Name = "IX_PdfCache_LastAccessedAt")]
    public long LastAccessedAt { get; set; }
}
