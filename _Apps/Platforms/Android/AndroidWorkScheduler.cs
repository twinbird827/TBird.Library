using Android.Content;
using AndroidX.Work;
using Java.Util.Concurrent;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.App.Platforms.Android;

/// <summary>WorkManager による定期チェックの登録/解除（IWorkScheduler 実装）。</summary>
public sealed class AndroidWorkScheduler : IWorkScheduler
{
    private const string UniqueWorkName = "new_release_periodic_check";

    private readonly Context _context;

    public AndroidWorkScheduler(Context context) => _context = context;

    public void Schedule(string interval)
    {
        var hours = CheckIntervals.ToHours(interval);

        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();

        var request = new PeriodicWorkRequest.Builder(
                Java.Lang.Class.FromType(typeof(NewReleaseWorker)), hours, TimeUnit.Hours!)
            .SetConstraints(constraints!)
            .SetBackoffCriteria(BackoffPolicy.Exponential!, PeriodicWorkRequest.MinBackoffMillis, TimeUnit.Milliseconds!)
            .Build();

        WorkManager.GetInstance(_context).EnqueueUniquePeriodicWork(
            UniqueWorkName, ExistingPeriodicWorkPolicy.Update!, (PeriodicWorkRequest)request);
    }

    public void Cancel()
        => WorkManager.GetInstance(_context).CancelUniqueWork(UniqueWorkName);
}
