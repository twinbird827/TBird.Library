using AndroidX.Core.View;

namespace ComicViewer.Services;

public class SystemBarsService
{
    public void Hide()
    {
        if (GetController() is not { } controller) return;
        controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        controller.Hide(WindowInsetsCompat.Type.SystemBars());
    }

    public void Show() => GetController()?.Show(WindowInsetsCompat.Type.SystemBars());

    /// <summary>ナビゲーションバーの高さ（dp）。オーバーレイ下部パネルを重ねないために使う。</summary>
    public double GetNavigationBarHeight()
    {
        var activity = Platform.CurrentActivity;
        var decor = activity?.Window?.DecorView;
        if (decor is null) return 0;
        var insets = ViewCompat.GetRootWindowInsets(decor)?.GetInsetsIgnoringVisibility(WindowInsetsCompat.Type.NavigationBars());
        return insets is null ? 0 : insets.Bottom / (activity!.Resources?.DisplayMetrics?.Density ?? 1);
    }

    private static WindowInsetsControllerCompat? GetController()
    {
        var window = Platform.CurrentActivity?.Window;
        return window?.DecorView is { } decor ? WindowCompat.GetInsetsController(window, decor) : null;
    }
}
