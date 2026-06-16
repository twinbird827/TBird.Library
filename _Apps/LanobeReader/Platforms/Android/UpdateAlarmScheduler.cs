using Android.App;
using Android.Content;
using Android.OS;
using LanobeReader.Helpers;
using Microsoft.Maui.Storage;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// 更新チェック起床アラーム。WorkManager 定期実行が Doze 中（特にモバイル通信時）に
/// 延期される問題の緩和策。Doze を貫通して発火し、前面サービス経路で一回限りのチェックを
/// 起動して、毎回次回を再武装する。
///
/// 可能なら setExactAndAllowWhileIdle（exact）を使う。exact アラームは Android 12+ で
/// 「バックグラウンドからの前面サービス起動」が許可される条件に該当するため、電池最適化の
/// 除外可否に依存せず <see cref="UpdateCheckForegroundService"/> を確実に起動できる。
/// exact 権限が無い/取得不可なら setAndAllowWhileIdle（不正確）へフォールバックする。
/// </summary>
public static class UpdateAlarmScheduler
{
    private const int RequestCode = 1001;
    public const string ActionFire = "com.tbird.lanobereader.UPDATE_ALARM";
    // 受信側(Receiver/BootReceiver)が DB 非依存で間隔を取得するための Preferences ミラー。
    public const string IntervalPrefKey = "alarm_interval_hours";

    public static void ScheduleNext(Context context, int intervalHours)
    {
        if (intervalHours <= 0) intervalHours = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS;
        Preferences.Set(IntervalPrefKey, intervalHours);

        var am = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (am is null) return;

        var pi = BuildPendingIntent(context);
        var triggerAt = Java.Lang.JavaSystem.CurrentTimeMillis() + (long)intervalHours * 3600_000L;

#if DEBUG
        // 実機テスト用: 間隔を分単位に短縮（Release では未参照）。
        if (DebugSchedulingConfig.AlarmOverrideMinutes > 0)
        {
            triggerAt = Java.Lang.JavaSystem.CurrentTimeMillis()
                + (long)DebugSchedulingConfig.AlarmOverrideMinutes * 60_000L;
            MessageService.Info($"[DEBUG] Alarm override: firing in {DebugSchedulingConfig.AlarmOverrideMinutes} min");
        }
#endif

        // exact アラームは前面サービスの背面起動を許可する条件（Android 12+）に該当する。
        // API 31+ は権限の有無を実機判定し、不可なら不正確アラームへフォールバック。
        var useExact = Build.VERSION.SdkInt < BuildVersionCodes.S || am.CanScheduleExactAlarms();
        if (useExact)
        {
            am.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pi);
            MessageService.Info($"Update alarm (exact) scheduled in {intervalHours}h");
        }
        else
        {
            // 不正確アラーム → Doze 中もメンテ窓で発火するが、前面起動は電池最適化除外に依存。
            am.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pi);
            MessageService.Info($"Update alarm (inexact) scheduled in {intervalHours}h");
        }
    }

    public static void ScheduleFromCache(Context context)
    {
        ScheduleNext(context, GetCachedIntervalHours());
    }

    /// <summary>
    /// DB 非依存で間隔を取得する Preferences ミラーの値を返す(欠落時は既定値)。
    /// MainActivity のフォールバック分岐で WorkManager とアラームの間隔を一致させるために使用。
    /// </summary>
    public static int GetCachedIntervalHours()
        => Preferences.Get(IntervalPrefKey, SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS);

    public static void Cancel(Context context)
    {
        var am = context.GetSystemService(Context.AlarmService) as AlarmManager;
        am?.Cancel(BuildPendingIntent(context));
    }

    private static PendingIntent BuildPendingIntent(Context context)
    {
        var intent = new Intent(context, typeof(UpdateAlarmReceiver));
        intent.SetAction(ActionFire);
        return PendingIntent.GetBroadcast(
            context, RequestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}
