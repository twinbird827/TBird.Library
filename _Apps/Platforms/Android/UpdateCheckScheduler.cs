using Android.Content;
using AndroidX.Work;
using LanobeReader.Helpers;

namespace LanobeReader.Platforms.Android;

public static class UpdateCheckScheduler
{
    public static void SchedulePeriodicCheck(Context context, int intervalHours = 6)
    {
        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();

        var workRequest = new PeriodicWorkRequest.Builder(
                typeof(UpdateCheckWorker),
                TimeSpan.FromHours(intervalHours))
            .SetConstraints(constraints)
            .AddTag(UpdateCheckWorker.WORK_TAG)
            .Build();

        WorkManager.GetInstance(context)!.EnqueueUniquePeriodicWork(
            UpdateCheckWorker.WORK_TAG,
            ExistingPeriodicWorkPolicy.Keep!,
            workRequest);

        LogHelper.Info(nameof(UpdateCheckScheduler), $"Scheduled periodic check every {intervalHours} hours");
    }
}
