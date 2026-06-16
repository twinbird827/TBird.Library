using Android.Content;
using LanobeReader.Helpers;
using LanobeReader.Services;
using LanobeReader.Services.Database;
using Microsoft.Extensions.DependencyInjection;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// 更新チェックの実処理（DI 解決 → DB 初期化 → アラーム再同期 → CheckAll → 通知）を一元化する。
/// WorkManager 経路（<see cref="UpdateCheckWorker"/>）と前面サービス経路
/// （<see cref="UpdateCheckForegroundService"/>）の双方から呼び、ロジック重複を避ける。
/// </summary>
public static class UpdateCheckRunner
{
    public enum Outcome { Success, Retry, Failure }

    public static async Task<Outcome> RunAsync(Context appContext, CancellationToken ct = default)
    {
        // MainApplication 初期化完了前に起動しうる（FGS / Worker いずれも）。最大 ~3 秒だけ待つ。
        IServiceProvider? services = null;
        for (int i = 0; i < 30; i++)
        {
            services = IPlatformApplication.Current?.Services;
            if (services is not null) break;
            await Task.Delay(100).ConfigureAwait(false);
        }

        if (services is null)
        {
            // 呼び出し側のリトライ（WorkManager のバックオフ等）に委ねる。
            MessageService.Warn("IPlatformApplication.Current is null, retry later");
            return Outcome.Retry;
        }

        var dbService = services.GetService<DatabaseService>();
        var updateCheckService = services.GetService<UpdateCheckService>();
        var notifier = services.GetService<IUpdateNotificationService>();
        if (dbService is null || updateCheckService is null || notifier is null)
        {
            MessageService.Error("Failed to resolve services");
            return Outcome.Failure;
        }

        // DB 初期化（アプリ起動完了前に走る可能性があるため明示）。
        await dbService.EnsureInitializedAsync().ConfigureAwait(false);

        // 権威ある DB の間隔値でアラームを再武装し、Preferences ミラーの drift を是正する。
        var settingsRepo = services.GetService<AppSettingsRepository>();
        if (settingsRepo is not null)
        {
            try
            {
                var hours = await settingsRepo.GetIntValueAsync(
                    SettingsKeys.UPDATE_INTERVAL_HOURS,
                    SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS).ConfigureAwait(false);
                UpdateAlarmScheduler.ScheduleNext(appContext, hours);
            }
            catch (Exception ex)
            {
                MessageService.Warn($"Alarm re-sync from DB failed: {ex.Message}");
            }
        }

        // 通知表示はフォアグラウンド経路(App.xaml.cs)と共通の IUpdateNotificationService に集約。
        var updates = await updateCheckService.CheckAllAsync(ct).ConfigureAwait(false);
        await notifier.ShowUpdatesAsync(updates).ConfigureAwait(false);
        return Outcome.Success;
    }
}
