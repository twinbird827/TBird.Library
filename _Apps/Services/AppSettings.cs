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

    /// <summary>S03 で表示中の漫画。S03 を閉じると空に戻し、残っていれば起動時に開き直す。</summary>
    public static string ViewingSourceKey
    {
        get => Preferences.Get(nameof(ViewingSourceKey), "");
        set => Preferences.Set(nameof(ViewingSourceKey), value);
    }

    public static string ViewingPath
    {
        get => Preferences.Get(nameof(ViewingPath), "");
        set => Preferences.Set(nameof(ViewingPath), value);
    }
}
