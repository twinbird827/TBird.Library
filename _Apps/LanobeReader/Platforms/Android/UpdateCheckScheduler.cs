using Android.Content;
using Android.OS;
using AndroidX.Work;
using LanobeReader.Helpers;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

public static class UpdateCheckScheduler
{
    public const string ONETIME_WORK = "lanobe_update_check_once";

    /// <summary>
    /// アラーム発火時に一回限りの更新チェックを実行する。API 31+ は通知不要の expedited ジョブで
    /// Doze 中も実行。古い OS は通常ワークにフォールバック。
    /// Replace を使い、ネットワーク制約で滞留した古いインスタンスへ後続発火が併合されて
    /// 握り潰される事態を防ぐ(実際の重複実行は UpdateCheckService 側の単一実行ガードが抑止)。
    /// </summary>
    public static void EnqueueOneTimeCheck(Context context)
    {
        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();

        var builder = new OneTimeWorkRequest.Builder(typeof(UpdateCheckWorker))
            .SetConstraints(constraints)
            .AddTag(UpdateCheckWorker.WORK_TAG);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            builder.SetExpedited(OutOfQuotaPolicy.RunAsNonExpeditedWorkRequest!);
        }

        WorkManager.GetInstance(context)!.EnqueueUniqueWork(
            ONETIME_WORK, ExistingWorkPolicy.Replace!, builder.Build());

        MessageService.Info("Enqueued one-time update check (alarm)");
    }

    public static void SchedulePeriodicCheck(Context context, int intervalHours = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS)
    {
        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();

        var workRequest = new PeriodicWorkRequest.Builder(
                typeof(UpdateCheckWorker),
                TimeSpan.FromHours(intervalHours))
            .SetConstraints(constraints)
            // Worker が Retry を返した場合(DI 未初期化等)の再試行間隔。指数バックオフ・初期 30 秒。
            .SetBackoffCriteria(BackoffPolicy.Exponential!, TimeSpan.FromSeconds(30))
            .AddTag(UpdateCheckWorker.WORK_TAG)
            .Build();

        WorkManager.GetInstance(context)!.EnqueueUniquePeriodicWork(
            UpdateCheckWorker.WORK_TAG,
            ExistingPeriodicWorkPolicy.Update!,
            workRequest);

        MessageService.Info($"Scheduled periodic check every {intervalHours} hours");
    }
}
