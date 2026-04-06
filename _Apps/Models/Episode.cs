using SQLite;

namespace LanobeReader.Models;

[Table("episodes")]
public class Episode
{
    [PrimaryKey, AutoIncrement]
    [Column("id")]
    public int Id { get; set; }

    [Column("novel_id")]
    [Indexed(Name = "idx_episodes_novel_id")]
    public int NovelId { get; set; }

    [Column("episode_no")]
    public int EpisodeNo { get; set; }

    [Column("chapter_name")]
    public string? ChapterName { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("is_read")]
    public int IsRead { get; set; }

    [Column("read_at")]
    public string? ReadAt { get; set; }

    [Column("published_at")]
    public string? PublishedAt { get; set; }
}
