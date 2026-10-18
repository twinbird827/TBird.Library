using Android.App;
using Android.Views;
using TBird.Core;

namespace ComicViewer.Services;

/// <summary>Activity の Window の明るさだけを変える（OS 設定は変えない）。単一 Activity のためアプリ全体に効く。</summary>
public class BrightnessService
{
    public void Apply() => Apply(Platform.CurrentActivity);

    public void SetBrightness(float value)
    {
        AppSettings.BrightnessFollowSystem = false;
        AppSettings.Brightness = value;
        Apply();
    }

    public static void Apply(Activity? activity)
    {
        var window = activity?.Window;
        if (window?.Attributes is not { } attributes)
        {
            MessageService.Warn("Activity is not available");
            return;
        }
        attributes.ScreenBrightness = AppSettings.BrightnessFollowSystem
            ? WindowManagerLayoutParams.BrightnessOverrideNone
            : AppSettings.Brightness;
        window.Attributes = attributes;
    }
}
