using Android.App;
using Android.Content;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

// Exported=false: 明示 PendingIntent 経由のみ（暗黙 intent-filter 不要）。
[BroadcastReceiver(Enabled = true, Exported = false)]
public class UpdateAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;

        // 1) まず次回を再武装（例外で連鎖が切れないよう最優先）。
        try { UpdateAlarmScheduler.ScheduleFromCache(context); }
        catch (Exception ex) { MessageService.Warn($"Re-arm alarm failed: {ex.Message}"); }

        // 2) 前面サービスで Doze を貫通して即チェック。前面起動が拒否された場合は
        //    サービス側が WorkManager 経路へフォールバックする（UpdateCheckForegroundService）。
        try { UpdateCheckForegroundService.Start(context); }
        catch (Exception ex)
        {
            MessageService.Warn($"Start FGS failed; fallback to WorkManager: {ex.Message}");
            try { UpdateCheckScheduler.EnqueueOneTimeCheck(context); }
            catch (Exception ex2) { MessageService.Error($"Enqueue one-time check failed: {ex2.Message}"); }
        }
    }
}
