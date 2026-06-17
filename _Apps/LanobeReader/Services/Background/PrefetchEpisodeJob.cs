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
    // サイト側エピソード ID(あれば)。本文取得の位置依存解決を避けるため API へ渡す。
    public string? SiteEpisodeId { get; init; }

    public override string Description => $"Prefetch novel={NovelDbId} ep={EpisodeNo}";
}
