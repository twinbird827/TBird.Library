namespace LanobeReader.Platforms.Android;

/// <summary>
/// アプリが前面(フォアグラウンド)にあるかを追跡する。
/// 前面時はアプリ内 UI が新着(NEW)を直接表示するため、システム通知の投稿を抑止する。
/// これにより「前面で出した通知を直後の OnResume.CancelAll が消す」機能同士の競合を防ぐ。
/// </summary>
public static class AppForegroundTracker
{
    private static volatile bool _isForeground;

    public static bool IsForeground => _isForeground;

    public static void SetForeground(bool value) => _isForeground = value;
}
