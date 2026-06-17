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

    /// <summary>
    /// この小説を最後に更新チェックした日時(ISO8601 "o", UTC)。null=未チェック。
    /// 更新チェックを「古い順(未チェック優先)」で回し、3分上限等で打ち切られても
    /// 次回が続きから拾えるようにするため (ラウンドロビン)。
    /// </summary>
    [Column("last_checked_at")]
    public string? LastCheckedAt { get; set; }

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
