using Android.App;
using Android.OS;
using AObject = Java.Lang.Object;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// アプリ全体の Activity ライフサイクルを監視し、可視(Started)Activity 数で前面/背面を判定する。
/// <see cref="AppForegroundTracker"/> のカウンタを駆動する唯一の経路。
///
/// Started/Stopped 境界で数える理由: 権限・電池最適化のシステムダイアログや、アプリ自身の
/// DisplayAlert は直下の Activity を Pause させるが Stop はさせない。Resume/Pause で数えると
/// 「ユーザは見ているのに背面扱い」となる隙が生じ、その間に背面経路の通知が出てしまう。
/// 真に不可視になる Stop までは前面とみなす。OnActivityStarted は OnActivityResumed より先に
/// 走るため、MainActivity.OnResume の CancelAll より前に前面フラグが立つ。
/// </summary>
public class ActivityForegroundCallbacks : AObject, global::Android.App.Application.IActivityLifecycleCallbacks
{
    public void OnActivityStarted(Activity? activity) => AppForegroundTracker.OnActivityStarted();
    // 構成変更(回転/ダークモード/ロケール変更等)による再生成 Stop は真の背面化と区別する
    // (IsChangingConfigurations=true)。これを渡し、トラッカの一時クリアによる通知漏れを防ぐ。
    public void OnActivityStopped(Activity? activity)
        => AppForegroundTracker.OnActivityStopped(activity?.IsChangingConfigurations ?? false);

    public void OnActivityCreated(Activity? activity, Bundle? savedInstanceState) { }
    public void OnActivityResumed(Activity? activity) { }
    public void OnActivityPaused(Activity? activity) { }
    public void OnActivitySaveInstanceState(Activity? activity, Bundle? outState) { }
    public void OnActivityDestroyed(Activity? activity) { }
}
