using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Maui.Background;

// [DI-LIFETIME: SINGLETON]
// AddTransient/AddScoped causes WiFi handler leak because
// this class subscribes to INetworkPolicy in the ctor without unsubscribe (no IDisposable).
/// <summary>
/// インプロセスの優先度付きバックグラウンドジョブキュー。
///
/// - Wi-Fi 接続時のみ稼働、切断で自動停止、再接続でレジューム
/// - <c>isEnabled</c> delegate による設定 OFF 時の drop
/// - High / Normal の 2 本キューで優先度を扱う（priority aging はしない）
/// - 同一 <typeparamref name="TKey"/> の重複 enqueue を dedup
/// - 連続失敗で同セッションのワーカーをブレーカー停止
/// - バッチクールダウン（一定数処理ごとに sleep）
///
/// dedup HashSet と 2 本キューの Enqueue は同一 lock 内で完結させ、
/// StopWorker / SyncEnqueuedIdsFromQueues との race を避ける設計。
/// </summary>
/// <typeparam name="TJob">ジョブ型（<see cref="BackgroundJobBase"/> 派生）</typeparam>
/// <typeparam name="TKey">dedup キー型（例: int の episode id）</typeparam>
public class PriorityJobQueue<TJob, TKey>
    where TJob : BackgroundJobBase
    where TKey : notnull
{
    private readonly ConcurrentQueue<TJob> _highPriority = new();
    private readonly ConcurrentQueue<TJob> _normalPriority = new();
    private readonly HashSet<TKey> _dedupSet;
    private readonly INetworkPolicy _networkPolicy;
    private readonly Func<TJob, TKey> _keySelector;
    private readonly Func<TJob, CancellationToken, Task> _processor;
    private readonly Func<Task<bool>> _isEnabled;
    private readonly int _batchCooldownThreshold;
    private readonly int _cooldownDelayMs;
    private readonly int _maxConsecutiveFailures;

    private readonly object _startLock = new();
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    private int _consecutiveFailures;

    public PriorityJobQueue(
        INetworkPolicy networkPolicy,
        Func<TJob, TKey> keySelector,
        Func<TJob, CancellationToken, Task> processor,
        Func<Task<bool>> isEnabled,
        IEqualityComparer<TKey>? keyComparer = null,
        int batchCooldownThreshold = 200,
        int cooldownDelayMs = 5000,
        int maxConsecutiveFailures = 5)
    {
        _networkPolicy = networkPolicy;
        _keySelector = keySelector;
        _processor = processor;
        _isEnabled = isEnabled;
        _dedupSet = keyComparer is null ? new HashSet<TKey>() : new HashSet<TKey>(keyComparer);
        _batchCooldownThreshold = batchCooldownThreshold;
        _cooldownDelayMs = cooldownDelayMs;
        _maxConsecutiveFailures = maxConsecutiveFailures;

        _networkPolicy.WifiConnected += (_, _) => EnsureWorkerStarted();
        _networkPolicy.WifiDisconnected += (_, _) => StopWorker();
    }

    public int PendingCount => _highPriority.Count + _normalPriority.Count;

    /// <summary>
    /// 戻り値:
    ///   - false: isEnabled=false で drop
    ///   - false: dedup HashSet 重複で drop
    ///   - true:  実際にキューに積まれた
    /// </summary>
    public async Task<bool> EnqueueAsync(TJob job, JobPriority priority = JobPriority.Normal)
    {
        // 設定 OFF なら drop（HashSet には触れない）
        var enabled = await _isEnabled().ConfigureAwait(false);
        if (!enabled) return false;

        // dedup Add と Enqueue を同一 lock 内で完結させる（race-free 規約）
        lock (_dedupSet)
        {
            if (!_dedupSet.Add(_keySelector(job))) return false;
            var queue = priority == JobPriority.High ? _highPriority : _normalPriority;
            queue.Enqueue(job);
        }

        // EnsureWorkerStarted は別 lock (_startLock) を取るため、_dedupSet lock を抜けてから呼ぶ。
        EnsureWorkerStarted();
        return true;
    }

    public void EnsureWorkerStarted()
    {
        lock (_startLock)
        {
            if (_workerTask is not null && !_workerTask.IsCompleted) return;
            if (!_networkPolicy.IsWifiConnected) return;
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
            // ワーカー完了を待ってから Dispose（Cancel 直後の ObjectDisposedException 回避）
            _ = oldTask.ContinueWith(_ => oldCts.Dispose(), TaskScheduler.Default);
        }
        else
        {
            oldCts.Dispose();
        }
    }

    private void SyncEnqueuedIdsFromQueues()
    {
        // ConcurrentQueue.GetEnumerator はスナップショット。
        // EnqueueAsync が _dedupSet.Add と Queue.Enqueue を同一 lock 内で完結させているため、
        // 本メソッドが lock を取った時点で Queue 列挙の結果は HashSet と整合している
        // (HashSet に居るが Queue に未追加という中間状態は存在しない)。
        lock (_dedupSet)
        {
            var live = _dedupSet.Comparer is null
                ? new HashSet<TKey>()
                : new HashSet<TKey>(_dedupSet.Comparer);
            foreach (var j in _highPriority) live.Add(_keySelector(j));
            foreach (var j in _normalPriority) live.Add(_keySelector(j));
            _dedupSet.Clear();
            foreach (var id in live) _dedupSet.Add(id);
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        try
        {
            // 二重ゲート (1): ループ開始時に isEnabled を再評価
            // （EnqueueAsync 時点では enabled だったが、ワーカー実行待ちのうちに設定 OFF に
            //   変更された場合に、キューに残った job が消化されないようにする）
            var enabled = await _isEnabled().ConfigureAwait(false);
            if (!enabled)
            {
                MessageService.Info("Job processing disabled by setting");
                return;
            }

            _consecutiveFailures = 0;
            int batchCount = 0;

            while (!ct.IsCancellationRequested)
            {
                // 二重ゲート (2): 各イテレーション冒頭で WiFi 接続を check
                // （ConnectivityChanged イベント遅延発火への防御）
                if (!_networkPolicy.IsWifiConnected)
                {
                    MessageService.Info("Wi-Fi disconnected, stopping");
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

                    if (batchCount % _batchCooldownThreshold == 0)
                    {
                        await Task.Delay(_cooldownDelayMs, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常なキャンセル経路。連続失敗カウントを上げずに break。
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    MessageService.Warn($"Job failed ({_consecutiveFailures}): {ex.Message}");
                    if (_consecutiveFailures >= _maxConsecutiveFailures)
                    {
                        MessageService.Warn("Too many consecutive failures, aborting");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Worker loop crashed: {ex.Message}");
        }
    }

    private bool TryDequeue(out TJob? job)
    {
        if (_highPriority.TryDequeue(out job)) return true;
        if (_normalPriority.TryDequeue(out job)) return true;
        job = null;
        return false;
    }

    private async Task ProcessJobAsync(TJob job, CancellationToken ct)
    {
        try
        {
            await _processor(job, ct).ConfigureAwait(false);
        }
        finally
        {
            lock (_dedupSet) { _dedupSet.Remove(_keySelector(job)); }
        }
    }
}
