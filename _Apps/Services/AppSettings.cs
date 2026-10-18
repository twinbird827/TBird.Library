namespace ComicViewer.Services;

public static class AppSettings
{
    public const int MinCacheLimitMb = 256;
    public const int MaxCacheLimitMb = 20480;

    public static int CacheLimitMb
    {
        get => Preferences.Get(nameof(CacheLimitMb), 1024);
        set => Preferences.Set(nameof(CacheLimitMb), value);
    }

    public static bool BrightnessFollowSystem
    {
        get => Preferences.Get(nameof(BrightnessFollowSystem), true);
        set => Preferences.Set(nameof(BrightnessFollowSystem), value);
    }

    public static float Brightness
    {
        get => Preferences.Get(nameof(Brightness), 0.5f);
        set => Preferences.Set(nameof(Brightness), Math.Clamp(value, 0.01f, 1f));
    }
}
