using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using LanobeReader.Helpers;
using TBird.Core;
using LanobeReader.Platforms.Android;
using LanobeReader.Services.Database;

namespace LanobeReader;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        NotificationHelper.CreateNotificationChannels(this);

        // ApplicationContext を closure にキャプチャ。Activity 自体をキャプチャすると
        // 後続の Task.Run が最大 ~3 秒のリトライ + DB 初期化を含むため Activity ライフサイクルを
        // 跨いでリーク/例外の懸念がある。WorkManager は Application Context を要求する。
        var ctx = ApplicationContext
            ?? throw new InvalidOperationException("ApplicationContext is null in MainActivity.OnCreate");

        _ = Task.Run(async () =>
        {
            try
            {
                IServiceProvider? services = null;
                for (int i = 0; i < 30; i++)
                {
                    services = IPlatformApplication.Current?.Services;
                    if (services is not null) break;
                    await Task.Delay(100).ConfigureAwait(false);
                }

                if (services is null)
                {
                    MessageService.Warn("DI not ready in OnCreate; scheduling with cached interval");
                    // WorkManager とアラームを同一のキャッシュ間隔で武装し、両機構の間隔不一致を防ぐ。
                    UpdateSchedulingCoordinator.ArmBothFromCache(ctx);
                    return;
                }

                var dbService = services.GetService<DatabaseService>();
                var settingsRepo = services.GetService<AppSettingsRepository>();
                if (dbService is null || settingsRepo is null)
                {
                    UpdateSchedulingCoordinator.ArmBothFromCache(ctx);
                    return;
                }

                await dbService.EnsureInitializedAsync().ConfigureAwait(false);
                var hours = await settingsRepo.GetIntValueAsync(
                    SettingsKeys.UPDATE_INTERVAL_HOURS,
                    SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS).ConfigureAwait(false);

                // 差分判定: DB 値 != 前回 schedule 値 のときのみ再登録（毎起動でのリセット防止）。
                // last_scheduled_hours は update_interval_hours と同じ既定値 (=6) でシードされるため、
                // 既定設定のままのユーザは初回起動でも no-op となる。
                var lastScheduled = await settingsRepo.GetIntValueAsync(
                    SettingsKeys.LAST_SCHEDULED_HOURS,
                    SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS).ConfigureAwait(false);
                if (hours != lastScheduled)
                {
                    UpdateCheckScheduler.SchedulePeriodicCheck(ctx, hours);
                    await settingsRepo.SetValueAsync(
                        SettingsKeys.LAST_SCHEDULED_HOURS, hours.ToString()).ConfigureAwait(false);
                }

                // アラームは毎起動で再武装（冪等・自己修復）。同一 PendingIntent を上書きするだけ。
                UpdateAlarmScheduler.ScheduleNext(ctx, hours);
            }
            catch (Exception ex)
            {
                MessageService.Warn($"Schedule worker failed: {ex.Message}");
                // 最終フォールバックは耐障害性を優先し、片方の失敗でもう片方を巻き込まないよう
                // 各機構を独立 try で武装する（このため Coordinator.ArmBoth は経由しない）。
                var cached = UpdateAlarmScheduler.GetCachedIntervalHours();
                try { UpdateCheckScheduler.SchedulePeriodicCheck(ctx, cached); } catch { /* 諦める */ }
                try { UpdateAlarmScheduler.ScheduleNext(ctx, cached); } catch { /* 諦める */ }
            }
        });

        HandleIntent(Intent);
    }

    // 前面/背面の追跡は MainApplication が登録する ActivityForegroundCallbacks がアプリ全体の
    // Activity ライフサイクル(Started/Stopped)から一元的に行う。個々の Activity では行わない。

    protected override void OnResume()
    {
        base.OnResume();

        // 新着バッジ(ランチャーアイコンのドット)はアクティブな通知に紐づく Android 仕様のため、
        // アプリを前面化した時点で通知をクリアしてバッジを消す。
        // = 「新着があったらアプリを開くまではバッジを維持」を実現する。
        try { NotificationHelper.CancelAll(this); }
        catch (Exception ex) { MessageService.Warn($"CancelAll failed: {ex.Message}"); }

        // 電池最適化の除外を一度だけ依頼(OEM 実機でのバックグラウンド更新の信頼性向上)。
        BatteryOptimizationHelper.PromptOnceIfNeeded(this);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is not null)
        {
            HandleIntent(intent);
        }
    }

    private void HandleIntent(Intent? intent)
    {
        if (intent?.HasExtra("novelId") == true && intent.HasExtra("episodeId"))
        {
            var novelId = intent.GetIntExtra("novelId", -1);
            var episodeId = intent.GetIntExtra("episodeId", -1);
            var siteType = intent.GetIntExtra("siteType", 1);
            var siteNovelId = intent.GetStringExtra("siteNovelId") ?? "";

            if (novelId > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    // 未読話が解決できた場合はリーダーへ直行。解決できない(episodeId<=0)場合でも
                    // 無反応にせず、その小説の話一覧へ遷移してユーザの意図(=その小説を開く)を満たす。
                    var route = episodeId > 0
                        ? $"reader?novelId={novelId}&episodeId={episodeId}&siteType={siteType}&siteNovelId={siteNovelId}"
                        : $"episodes?novelId={novelId}";
                    try
                    {
                        // コールドスタートの通知タップでは Shell 構築前にここへ到達しうる。async void 内の
                        // NRE はプロセスを巻き込むため、null ガード + 握りつぶしでクラッシュを防ぐ。
                        if (Shell.Current is not null)
                        {
                            await Shell.Current.GoToAsync(route);
                        }
                        else
                        {
                            MessageService.Warn("Shell.Current is null on notification tap; navigation skipped");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageService.Warn($"Notification deep-link navigation failed: {ex.Message}");
                    }
                });
            }
        }
    }
}
