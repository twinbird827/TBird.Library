using SQLite;

namespace LanobeReader.Models;

[Table("episode_cache")]
public class EpisodeCache
{
    [PrimaryKey, AutoIncrement]
    [Column("id")]
    public int Id { get; set; }

    [Column("episode_id")]
    [Unique]
    [Indexed(Name = "idx_cache_episode_id")]
    public int EpisodeId { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("cached_at")]
    [Indexed(Name = "idx_cache_cached_at")]
    public string CachedAt { get; set; } = string.Empty;
}
