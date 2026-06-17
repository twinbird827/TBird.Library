using System.Threading;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// アプリが前面(フォアグラウンド)にあるかを、可視(Started)Activity 数で追跡する。
/// 前面時はアプリ内 UI が新着(NEW)を直接表示するため、システム通知の投稿を抑止する。
/// これにより「前面で出した通知を直後の OnResume.CancelAll が消す」機能同士の競合を防ぐ。
/// カウンタの増減はアプリ全体の <see cref="ActivityForegroundCallbacks"/> が駆動する
/// (個々の Activity に依存しないため、Activity が複数になっても前面判定が崩れない)。
/// </summary>
public static class AppForegroundTracker
{
    private static int _startedActivityCount;

    public static bool IsForeground => Volatile.Read(ref _startedActivityCount) > 0;

    /// <summary>Activity が可視(Started)になった。前面 Activity 数を 1 増やす。</summary>
    public static void OnActivityStarted() => Interlocked.Increment(ref _startedActivityCount);

    /// <summary>Activity が不可視(Stopped)になった。前面 Activity 数を 1 減らす(下限 0)。</summary>
    public static void OnActivityStopped()
    {
        // 順序逆転・二重通知に対する防御。負値には陥らせない。
        if (Interlocked.Decrement(ref _startedActivityCount) < 0)
        {
            Interlocked.Exchange(ref _startedActivityCount, 0);
        }
    }
}
