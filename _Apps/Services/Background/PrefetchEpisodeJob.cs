using TBird.Maui.Background;

namespace LanobeReader.Services.Background;

/// <summary>
/// 未キャッシュ話を取得してキャッシュ保存するジョブ。
/// </summary>
public class PrefetchEpisodeJob : BackgroundJobBase
{
    public int NovelDbId { get; init; }
    public int EpisodeDbId { get; init; }
    public int SiteType { get; init; }
    public string SiteNovelId { get; init; } = string.Empty;
    public int EpisodeNo { get; init; }

    public override string Description => $"Prefetch novel={NovelDbId} ep={EpisodeNo}";
}
