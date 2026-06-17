using System.Threading;

namespace LanobeReader.Services;

/// <summary>
/// 「新着(NEW)を即時表示する一覧画面(本棚)が現在可視で購読中か」を追跡するプラットフォーム非依存の
/// カウンタ。前面判定(Activity ライフサイクル=<c>AppForegroundTracker.IsForeground</c>)と組み合わせ、
/// アプリ内一覧が新着を直接表示している間だけシステム通知を抑止するために使う。
///
/// ViewModel 層(<see cref="ViewModels.AutoReloadViewModel"/>)から増減されるため、Platforms.Android の
/// 静的クラスではなく中立な Services 層に置く(VM がプラットフォーム実装へ逆依存しないようにする)。
/// </summary>
public static class UpdateListVisibilityTracker
{
    private static int _visibleCount;

    /// <summary>新着を即時表示する一覧が 1 つ以上可視で購読中か。</summary>
    public static bool HasVisibleUpdateList => Volatile.Read(ref _visibleCount) > 0;

    /// <summary>一覧画面が可視(購読開始)になった。</summary>
    public static void OnSubscribed() => Interlocked.Increment(ref _visibleCount);

    /// <summary>一覧画面が非可視(購読解除)になった。下限 0。</summary>
    public static void OnUnsubscribed()
    {
        // Decrement→負値→Exchange(0) の間に他スレッドの Increment を取りこぼさないよう、CAS で
        // 「正のときのみ 1 減らす」を実現する(0 以下なら何もしない)。
        int current;
        do
        {
            current = Volatile.Read(ref _visibleCount);
            if (current <= 0) return;
        }
        while (Interlocked.CompareExchange(ref _visibleCount, current - 1, current) != current);
    }

    /// <summary>
    /// カウントを 0 へ戻す。アプリが背面化(可視 Activity 数 0)した時点で呼び、OnDisappearing が
    /// 万一発火しなかった場合のカウンタ漏れ(=前面復帰後に通知が恒久抑止される事故)を自己修復する。
    /// 背面では可視一覧は存在し得ないため 0 化は常に安全。
    /// </summary>
    public static void Reset() => Interlocked.Exchange(ref _visibleCount, 0);
}
