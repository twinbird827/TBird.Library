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
    public bool IsRead { get; set; }

    [Column("read_at")]
    public string? ReadAt { get; set; }

    [Column("published_at")]
    public string? PublishedAt { get; set; }

    [Column("is_favorite")]
    public bool IsFavorite { get; set; }

    [Column("favorited_at")]
    public string? FavoritedAt { get; set; }

    // サイト側の不透明なエピソード ID（例: Kakuyomu の TableOfContentsChapter 配下 Episode の ID）。
    // 本文取得を episode_no の位置依存解決ではなくこの安定 ID で行うことで、序盤話の削除/並べ替えで
    // 目次がずれても誤った話を表示しなくなる。Narou は URL に episode_no を直接使うため null のまま。
    [Column("site_episode_id")]
    public string? SiteEpisodeId { get; set; }
}
