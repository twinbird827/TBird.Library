using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Database;
using TBird.Maui.Background;

namespace LanobeReader.Services.Background;

/// <summary>
/// PrefetchEpisodeJob 用の薄い <see cref="PriorityJobQueue{TJob, TKey}"/> ラッパー。
/// 主要ロジック（dedup / 2 本キュー / WiFi ゲート / 連続失敗ブレーカー / バッチクールダウン）は
/// lib 側 <see cref="PriorityJobQueue{TJob, TKey}"/> に閉じ込められている。
/// このラッパーが残る目的:
///   (a) コンストラクタで EpisodeContentService(本文取得+キャッシュ可否判定+保存を集約したファサード)
///       をキャプチャした processor delegate を組み立てる場所として機能する
///   (b) Service 層 (PrefetchService / UpdateCheckService) の DI シグネチャを変えないため
///       既存呼出側コードを無修正で済ませる
/// </summary>
public class BackgroundJobQueue
{
    private readonly PriorityJobQueue<PrefetchEpisodeJob, int> _queue;

    public BackgroundJobQueue(
        INetworkPolicy networkPolicy,
        AppSettingsRepository settingsRepo,
        EpisodeContentService contentService)
    {
        _queue = new PriorityJobQueue<PrefetchEpisodeJob, int>(
            networkPolicy: networkPolicy,
            keySelector: j => j.EpisodeDbId,
            processor: async (job, ct) =>
            {
                // 取得・キャッシュ可否判定・保存は EpisodeContentService に集約。命中時は内部でネットワークへ
                // 出ない(キャッシュ命中=早期 return 相当)ため、ここでの事前命中チェックは不要。
                await contentService.GetContentAsync(
                    job.EpisodeDbId, (SiteType)job.SiteType, job.SiteNovelId, job.EpisodeNo, job.SiteEpisodeId,
                    networkAllowed: true, ct).ConfigureAwait(false);
            },
            isEnabled: async () =>
            {
                var v = await settingsRepo.GetIntValueAsync(
                    SettingsKeys.PREFETCH_ENABLED,
                    SettingsKeys.DEFAULT_PREFETCH_ENABLED).ConfigureAwait(false);
                return v != 0;
            });
    }

    public int PendingCount => _queue.PendingCount;

    public Task<bool> EnqueueAsync(PrefetchEpisodeJob job, JobPriority priority = JobPriority.Normal)
        => _queue.EnqueueAsync(job, priority);

    public void EnsureWorkerStarted() => _queue.EnsureWorkerStarted();

    public void StopWorker() => _queue.StopWorker();
}
