namespace LanobeReader.Models;

public class SearchResult
{
    public SiteType SiteType { get; set; }
    public string NovelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int TotalEpisodes { get; set; }
    public bool IsCompleted { get; set; }
    public string? LastUpdatedAt { get; set; }
}
