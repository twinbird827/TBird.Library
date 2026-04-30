using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using LanobeReader.Helpers;
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
                    LogHelper.Warn(nameof(MainActivity),
                        "DI not ready in OnCreate; scheduling with default interval");
                    UpdateCheckScheduler.SchedulePeriodicCheck(ctx);
                    return;
                }

                var dbService = services.GetService<DatabaseService>();
                var settingsRepo = services.GetService<AppSettingsRepository>();
                if (dbService is null || settingsRepo is null)
                {
                    UpdateCheckScheduler.SchedulePeriodicCheck(ctx);
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
            }
            catch (Exception ex)
            {
                LogHelper.Warn(nameof(MainActivity), $"Schedule worker failed: {ex.Message}");
                try { UpdateCheckScheduler.SchedulePeriodicCheck(ctx); } catch { /* 諦める */ }
            }
        });

        HandleIntent(Intent);
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

            if (novelId > 0 && episodeId > 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.GoToAsync(
                        $"reader?novelId={novelId}&episodeId={episodeId}&siteType={siteType}&siteNovelId={siteNovelId}");
                });
            }
        }
    }
}
