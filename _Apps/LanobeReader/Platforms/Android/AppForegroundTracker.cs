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
    private static int _visibleUpdateListCount;

    public static bool IsForeground => Volatile.Read(ref _startedActivityCount) > 0;

    /// <summary>
    /// 新着を即時表示する一覧画面(本棚/目次)が現在可視で購読中か。前面でも一覧が出ていない画面
    /// (リーダー/設定)に滞在中はこれが false になり、システム通知を抑止すべきでないと判定できる。
    /// </summary>
    public static bool HasVisibleUpdateList => Volatile.Read(ref _visibleUpdateListCount) > 0;

    /// <summary>
    /// システム通知を抑止すべきか。前面 かつ 新着を即時表示する一覧が可視のときのみ true。
    /// (前面でも一覧非表示なら抑止すると新着が全く可視化されないため通知を出す)。
    /// 通知投稿側(UpdateNotificationService)と原子的ゲート(NotificationHelper)が同じ条件を参照する。
    /// </summary>
    public static bool ShouldSuppressSystemNotification => IsForeground && HasVisibleUpdateList;

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

    /// <summary>一覧画面が新着メッセージの購読を開始した(可視になった)。</summary>
    public static void OnUpdateListSubscribed() => Interlocked.Increment(ref _visibleUpdateListCount);

    /// <summary>一覧画面が購読を解除した(非表示になった)。前面一覧数を 1 減らす(下限 0)。</summary>
    public static void OnUpdateListUnsubscribed()
    {
        if (Interlocked.Decrement(ref _visibleUpdateListCount) < 0)
        {
            Interlocked.Exchange(ref _visibleUpdateListCount, 0);
        }
    }
}
