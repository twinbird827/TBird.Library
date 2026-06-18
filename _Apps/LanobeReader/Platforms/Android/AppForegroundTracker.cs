using System.Threading;
using LanobeReader.Services;

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

    /// <summary>
    /// システム通知を抑止すべきか。前面 かつ 新着を即時表示する一覧が可視のときのみ true。
    /// (前面でも一覧非表示なら抑止すると新着が全く可視化されないため通知を出す)。
    /// 通知投稿側(UpdateNotificationService)と原子的ゲート(NotificationHelper)が同じ条件を参照する。
    /// 可視一覧の追跡は中立な <see cref="UpdateListVisibilityTracker"/> へ委譲する。
    /// </summary>
    public static bool ShouldSuppressSystemNotification
        => IsForeground && UpdateListVisibilityTracker.HasVisibleUpdateList;

    /// <summary>Activity が可視(Started)になった。前面 Activity 数を 1 増やす。</summary>
    public static void OnActivityStarted() => Interlocked.Increment(ref _startedActivityCount);

    /// <summary>
    /// Activity が不可視(Stopped)になった。前面 Activity 数を 1 減らす(下限 0)。
    /// <paramref name="isChangingConfigurations"/> が true の場合は構成変更(画面回転/ダークモード/ロケール
    /// 変更等)による Activity 再生成であり、直後に新 Activity が Start する。この一時的な 0 遷移を真の
    /// 背面化と区別し、可視一覧トラッカの Reset(自己修復クリア)を抑止する。
    /// </summary>
    public static void OnActivityStopped(bool isChangingConfigurations = false)
    {
        // 順序逆転・二重通知に対する防御。負値には陥らせない。
        var remaining = Interlocked.Decrement(ref _startedActivityCount);
        if (remaining < 0)
        {
            Interlocked.Exchange(ref _startedActivityCount, 0);
            remaining = 0;
        }

        // 完全に背面化した(可視 Activity 0)時点で可視一覧カウンタを 0 へ自己修復する。OnDisappearing が
        // 万一発火せずカウンタが漏れても、前面復帰後に通知が恒久抑止される事故を防ぐ(背面では可視一覧なし)。
        // ただし構成変更による再生成は背面化ではない(ユーザは実質同じ一覧に留まる)。ここで Reset すると
        // 再生成の一瞬だけ可視一覧が消え、その窓に背面チェックがシステム通知を出してしまう(回転は日常操作で
        // 誤発火頻度が高い)。再生成時は Reset せず、新 Activity の OnAppearing 再購読に委ねる。
        if (remaining == 0 && !isChangingConfigurations)
        {
            UpdateListVisibilityTracker.Reset();
        }
    }
}
