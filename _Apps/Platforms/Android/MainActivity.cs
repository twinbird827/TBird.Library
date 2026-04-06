using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using LanobeReader.Platforms.Android;

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

        // Create notification channels
        NotificationHelper.CreateNotificationChannels(this);

        // Schedule periodic update check
        UpdateCheckScheduler.SchedulePeriodicCheck(this);

        // Handle notification deep link
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
