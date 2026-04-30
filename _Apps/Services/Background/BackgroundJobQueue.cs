using System.Collections.Concurrent;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Database;
using LanobeReader.Services.Network;

namespace LanobeReader.Services.Background;

/// <summary>
/// インプロセスのバックグラウンドジョブキュー。
/// - Wi-Fi接続時のみ稼働、モバイル通信時や切断時は自動停止（レジューム対応）
/// - 設定 prefetch_enabled が OFF の場合も停止
/// - ジョブは優先度付きで直列処理（NetworkPolicyService 経由で適切にディレイ）
/// - 連続5失敗で同セッションの処理を中断
/// </summary>
public class BackgroundJobQueue
{
    private readonly ConcurrentQueue<PrefetchEpisodeJob> _highPriority = new();
    private readonly ConcurrentQueue<PrefetchEpisodeJob> _normalPriority = new();
    private readonly NetworkPolicyService _network;
    private readonly AppSettingsRepository _settingsRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly INovelServiceFactory _serviceFactory;

    private const int BatchCooldownThreshold = 200;
    private const int CooldownDelayMs = 5000;
    private const int MaxConsecutiveFailures = 5;

    private readonly object _startLock = new();
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    private int _consecutiveFailures;
    private readonly HashSet<int> _enqueuedEpisodeIds = new();

    public BackgroundJobQueue(
        NetworkPolicyService network,
        AppSettingsRepository settingsRepo,
        EpisodeCacheRepository cacheRepo,
        EpisodeRepository episodeRepo,
        INovelServiceFactory serviceFactory)
    {
        _network = network;
        _settingsRepo = settingsRepo;
        _cacheRepo = cacheRepo;
        _episodeRepo = episodeRepo;
        _serviceFactory = serviceFactory;

        _network.WifiConnected += (_, _) => EnsureWorkerStarted();
        _network.WifiDisconnected += (_, _) => StopWorker();
    }

    public int PendingCount => _highPriority.Count + _normalPriority.Count;

    public async Task EnqueueAsync(PrefetchEpisodeJob job)
    {
        // 設定 OFF なら drop。GetIntValueAsync は内部で LoadAllAsync 完了を保証するため race なし。
        var enabled = await _settingsRepo.GetIntValueAsync(
            SettingsKeys.PREFETCH_ENABLED,
            SettingsKeys.DEFAULT_PREFETCH_ENABLED).ConfigureAwait(false);
        if (enabled == 0) return;

        // HashSet.Add と Queue.Enqueue を同一 lock 内で完結させる。
        // 旧 Enqueue は Add 後 lock を抜けてから Enqueue していたため、StopWorker →
        // SyncEnqueuedIdsFromQueues が割り込むと「HashSet にも Queue にもない job」が発生する race があった。
        lock (_enqueuedEpisodeIds)
        {
            if (!_enqueuedEpisodeIds.Add(job.EpisodeDbId)) return;
            if (job.Priority > 0) _highPriority.Enqueue(job);
            else _normalPriority.Enqueue(job);
        }

        EnsureWorkerStarted();
    }

    public void EnsureWorkerStarted()
    {
        lock (_startLock)
        {
            if (_workerTask is not null && !_workerTask.IsCompleted) return;
            if (!_network.IsWifiConnected) return;
            if (PendingCount == 0) return;

            _workerCts?.Dispose();
            _workerCts = new CancellationTokenSource();
            var ct = _workerCts.Token;
            _workerTask = Task.Run(() => WorkerLoopAsync(ct));
        }
    }

    public void StopWorker()
    {
        CancellationTokenSource? oldCts;
        Task? oldTask;
        lock (_startLock)
        {
            oldCts = _workerCts;
            oldTask = _workerTask;
            _workerCts = null;
            _workerTask = null;
        }
        if (oldCts is null) return;
        try { oldCts.Cancel(); }
        catch (ObjectDisposedException) { return; }

        // キューに残っている job の dedup HashSet を再開可能な状態に戻す。
        // キュー本体は消さない（Wi-Fi 復帰時にそのまま再消費される）。
        SyncEnqueuedIdsFromQueues();

        if (oldTask is not null)
        {
            _ = oldTask.ContinueWith(_ => oldCts.Dispose(), TaskScheduler.Default);
        }
        else
        {
            oldCts.Dispose();
        }
    }

    private void SyncEnqueuedIdsFromQueues()
    {
        // ConcurrentQueue.GetEnumerator はスナップショットを返す。
        // EnqueueAsync が HashSet.Add と Queue.Enqueue を同一 lock 内で完結させているため、
        // 本メソッドが lock を取った時点で Queue 列挙の結果は HashSet と整合している
        // (HashSet に居るが Queue に未追加という中間状態は存在しない)。
        lock (_enqueuedEpisodeIds)
        {
            var live = new HashSet<int>();
            foreach (var j in _highPriority) live.Add(j.EpisodeDbId);
            foreach (var j in _normalPriority) live.Add(j.EpisodeDbId);
            _enqueuedEpisodeIds.Clear();
            foreach (var id in live) _enqueuedEpisodeIds.Add(id);
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            // Gate check
            var enabled = await _settingsRepo.GetIntValueAsync(SettingsKeys.PREFETCH_ENABLED, 1).ConfigureAwait(false);
            if (enabled == 0)
            {
                LogHelper.Info(nameof(BackgroundJobQueue), "Prefetch disabled by setting");
                return;
            }

            _consecutiveFailures = 0;
            int batchCount = 0;

            while (!ct.IsCancellationRequested)
            {
                if (!_network.IsWifiConnected)
                {
                    LogHelper.Info(nameof(BackgroundJobQueue), "Wi-Fi disconnected, stopping");
                    break;
                }

                if (!TryDequeue(out var job))
                {
                    break;
                }

                try
                {
                    await ProcessJobAsync(job!, ct).ConfigureAwait(false);
                    _consecutiveFailures = 0;
                    batchCount++;

                    if (batchCount % BatchCooldownThreshold == 0)
                    {
                        await Task.Delay(CooldownDelayMs, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    LogHelper.Warn(nameof(BackgroundJobQueue), $"Job failed ({_consecutiveFailures}): {ex.Message}");
                    if (_consecutiveFailures >= MaxConsecutiveFailures)
                    {
                        LogHelper.Warn(nameof(BackgroundJobQueue), "Too many consecutive failures, aborting");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(BackgroundJobQueue), $"Worker loop crashed: {ex.Message}");
        }
    }

    private bool TryDequeue(out PrefetchEpisodeJob? job)
    {
        if (_highPriority.TryDequeue(out job)) return true;
        if (_normalPriority.TryDequeue(out job)) return true;
        job = null;
        return false;
    }

    private async Task ProcessJobAsync(PrefetchEpisodeJob job, CancellationToken ct)
    {
        try
        {
            // 既にキャッシュ済みならスキップ
            var cached = await _cacheRepo.GetByEpisodeIdAsync(job.EpisodeDbId).ConfigureAwait(false);
            if (cached is not null) return;

            var service = _serviceFactory.GetService((SiteType)job.SiteType);
            var content = await service.FetchEpisodeContentAsync(job.SiteNovelId, job.EpisodeNo, ct).ConfigureAwait(false);

            await _cacheRepo.InsertAsync(new EpisodeCache
            {
                EpisodeId = job.EpisodeDbId,
                Content = content,
                CachedAt = DateTime.UtcNow.ToString("o"),
            }).ConfigureAwait(false);
        }
        finally
        {
            lock (_enqueuedEpisodeIds) { _enqueuedEpisodeIds.Remove(job.EpisodeDbId); }
        }
    }
}
