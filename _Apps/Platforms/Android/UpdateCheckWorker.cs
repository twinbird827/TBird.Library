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
            // Resolve services from DI - we need to create instances manually for WorkManager
            var dbService = IPlatformApplication.Current?.Services.GetService<DatabaseService>();
            var novelRepo = IPlatformApplication.Current?.Services.GetService<NovelRepository>();
            var episodeRepo = IPlatformApplication.Current?.Services.GetService<EpisodeRepository>();
            var updateCheckService = IPlatformApplication.Current?.Services.GetService<UpdateCheckService>();

            if (dbService is null || novelRepo is null || episodeRepo is null || updateCheckService is null)
            {
                LogHelper.Error(nameof(UpdateCheckWorker), "Failed to resolve services");
                return Result.InvokeFailure();
            }

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogHelper.Warn(nameof(UpdateCheckWorker), $"Update check failed: {ex.Message}");
            NotificationHelper.ShowErrorNotification(ApplicationContext, "更新チェックに失敗しました");
            return Result.InvokeFailure();
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(UpdateCheckWorker), $"Unexpected error: {ex.Message}");
            return Result.InvokeFailure();
        }
    }
}
