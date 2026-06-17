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
        var services = await PlatformServiceReadiness.WaitForServicesAsync().ConfigureAwait(false);

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

        // 通知表示はフォアグラウンド経路(App.xaml.cs)と共通の IUpdateNotificationService に集約。
        // 別経路が実チェック中で本呼び出しがスキップされた場合は、この経路では何も確認できていない。
        // Retry を返して呼び出し側(FGS は WorkManager フォールバック、Worker は WorkManager バックオフ)に
        // 近接再試行を委ね、進行中経路が失敗/打ち切られても取りこぼさないようにする(同時実行ガードにより
        // 二重実行はせず、進行中経路が完了済みなら再試行は新着なしで通知も出ない=安全)。
        var skippedDueToContention = false;
        var updates = await updateCheckService
            .CheckAllAsync(ct, () => skippedDueToContention = true).ConfigureAwait(false);
        if (skippedDueToContention)
        {
            return Outcome.Retry;
        }

        await notifier.ShowUpdatesAsync(updates).ConfigureAwait(false);

        // 権威ある DB の間隔値でアラームを再武装し、Preferences ミラーの drift を是正する。
        // チェック「後」に行う: 受信側(UpdateAlarmReceiver)が発火直後にキャッシュ間隔で次回を再武装済みのため、
        // ここでの再同期前にプロセスが kill されても次回アラームは確保されている。再同期をチェック前に置くと、
        // kill 時に「次回が丸ごと1間隔先」へ押し出されるだけで近接リトライの機会を失う。
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

        // 完了時刻の記録は UpdateCheckService.CheckAllAsync(全経路の合流点)へ一元化済み。
        // ここで個別にマークすると経路ごとの記録漏れリスクが戻るため行わない。
        return Outcome.Success;
    }
}
