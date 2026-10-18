using Android.App;
using Android.Content.PM;
using Android.OS;
using ComicViewer.Services;

namespace ComicViewer;

// ScreenOrientation は指定しない（端末の自動回転に従う）。回転で Activity を作り直さないよう ConfigurationChanges は既定を残す。
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        BrightnessService.Apply(this);
    }
}
