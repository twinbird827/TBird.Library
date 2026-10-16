using Android.App;
using Android.Runtime;
using LanobeReader.Platforms.Android;

namespace LanobeReader;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        // 前面/背面の判定をアプリ全体の可視(Started)Activity 数で一元管理する。個々の Activity に
        // SetForeground を書かせる方式は単一 Activity を暗黙の前提とし、将来 Activity が増えると
        // 前面状態を取りこぼすため、アプリ全体のライフサイクルコールバックへ集約する。
        RegisterActivityLifecycleCallbacks(new ActivityForegroundCallbacks());
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
