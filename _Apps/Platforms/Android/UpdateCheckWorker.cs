using Android.Content;
using AndroidX.Work;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Database;

namespace LanobeReader.Platforms.Android;

public class UpdateCheckWorker : Worker
{
    public const string WORK_TAG = "lanobe_update_check";

    public UpdateCheckWorker(Context context, WorkerParameters workerParams) : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null)
            {
                // MainApplication 初期化完了前に Worker が起動した可能性。
                // Retry で WorkManager のバックオフに任せる（次回はプロセスが暖まっている見込み）。
                LogHelper.Warn(nameof(UpdateCheckWorker), "IPlatformApplication.Current is null, retry later");
                return Result.InvokeRetry();
            }

            var dbService = services.GetService<DatabaseService>();
            var novelRepo = services.GetService<NovelRepository>();
            var episodeRepo = services.GetService<EpisodeRepository>();
            var updateCheckService = services.GetService<UpdateCheckService>();

            if (dbService is null || novelRepo is null || episodeRepo is null || updateCheckService is null)
            {
                LogHelper.Error(nameof(UpdateCheckWorker), "Failed to resolve services");
                return Result.InvokeFailure();
            }

            // Ensure DB is initialized (Worker may run before app startup completes)
            dbService.EnsureInitializedAsync().GetAwaiter().GetResult();

            // Worker threads have no SynchronizationContext, so blocking on Task.Run is safe here
            var updates = Task.Run(() => updateCheckService.CheckAllAsync()).GetAwaiter().GetResult();

            foreach (var (novel, newCount) in updates)
            {
                // Get first unread episode for deep link
                var firstUnread = Task.Run(() => episodeRepo.GetFirstUnreadEpisodeAsync(novel.Id)).GetAwaiter().GetResult();
                var episodeId = firstUnread?.Id ?? 0;

                NotificationHelper.ShowUpdateNotification(
                    ApplicationContext,
                    novel.Id, // Use novel ID as notification ID
                    "ラノベリーダ",
                    $"{novel.Title}: {newCount}話更新",
                    novel.Id,
                    episodeId,
                    novel.SiteType,
                    novel.NovelId);
            }

            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(UpdateCheckWorker), $"Unexpected error: {ex.Message}");
            return Result.InvokeFailure();
        }
    }
}
