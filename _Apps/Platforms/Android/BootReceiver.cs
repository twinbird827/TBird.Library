using Android.App;
using Android.Content;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

// 再起動でアラームは消えるため再武装する。WorkManager 定期は自前の boot 受信で復帰する。
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        if (intent?.Action != Intent.ActionBootCompleted) return;
        try { UpdateAlarmScheduler.ScheduleFromCache(context); }
        catch (Exception ex) { MessageService.Warn($"Boot re-arm failed: {ex.Message}"); }
    }
}
