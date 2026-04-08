namespace LanobeReader.Services.Background;

/// <summary>
/// バックグラウンドジョブ基底。ジョブ種別ごとに派生。
/// </summary>
public abstract class BackgroundJob
{
    public int Priority { get; init; }
    public DateTime EnqueuedAt { get; } = DateTime.UtcNow;

    public abstract string Description { get; }
}

/// <summary>
/// 未キャッシュ話を取得してキャッシュ保存するジョブ。
/// </summary>
public class PrefetchEpisodeJob : BackgroundJob
{
    public int NovelDbId { get; init; }
    public int EpisodeDbId { get; init; }
    public int SiteType { get; init; }
    public string SiteNovelId { get; init; } = string.Empty;
    public int EpisodeNo { get; init; }

    public override string Description => $"Prefetch novel={NovelDbId} ep={EpisodeNo}";
}
