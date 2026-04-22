using SQLite;

namespace LanobeReader.Models;

[Table("novels")]
public class Novel
{
    [PrimaryKey, AutoIncrement]
    [Column("id")]
    public int Id { get; set; }

    [Column("site_type")]
    public int SiteType { get; set; }

    [Column("novel_id")]
    public string NovelId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("author")]
    public string Author { get; set; } = string.Empty;

    [Column("total_episodes")]
    public int TotalEpisodes { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("last_updated_at")]
    public string? LastUpdatedAt { get; set; }

    [Column("registered_at")]
    public string RegisteredAt { get; set; } = string.Empty;

    [Column("has_unconfirmed_update")]
    public bool HasUnconfirmedUpdate { get; set; }

    [Column("has_check_error")]
    public bool HasCheckError { get; set; }

    [Column("is_favorite")]
    public bool IsFavorite { get; set; }

    [Column("favorited_at")]
    public string? FavoritedAt { get; set; }

    [Ignore]
    public SiteType SiteTypeEnum
    {
        get => (SiteType)SiteType;
        set => SiteType = (int)value;
    }
}
