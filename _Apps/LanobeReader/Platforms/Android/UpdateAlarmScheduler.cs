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

    /// <summary>
    /// 次回発火までの実効間隔(ミリ秒)。Release は設定間隔そのもの。DEBUG では短縮上書きを反映。
    /// アラームの発火時刻と冗長発火ゲートの窓を「同じ間隔」で算出するため一元化している。
    /// </summary>
    internal static long GetEffectiveIntervalMs(int intervalHours)
    {
        if (intervalHours <= 0) intervalHours = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS;
        var ms = (long)intervalHours * 3600_000L;
#if DEBUG
        // 実機テスト用: 間隔を分単位に短縮（Release では未参照）。
        if (DebugSchedulingConfig.AlarmOverrideMinutes > 0)
        {
            ms = (long)DebugSchedulingConfig.AlarmOverrideMinutes * 60_000L;
        }
#endif
        return ms;
    }

    public static void ScheduleNext(Context context, int intervalHours)
    {
        if (intervalHours <= 0) intervalHours = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS;

        var am = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (am is null) return;

        var pi = BuildPendingIntent(context);
        // 実効間隔(DEBUG 短縮上書きを含む)は GetEffectiveIntervalMs に一元化。ログもこの結果から
        // 導出し、上書きロジックを二重評価して表示値が実値と乖離するのを防ぐ。
        var effectiveMs = GetEffectiveIntervalMs(intervalHours);
        // ELAPSED_REALTIME(端末起動からの経過時間)を基準にする。RTC(壁時計)だと手動/NTP の
        // 時刻巻き戻しでアラームが差分ぶん先送りされ、次回起動の再武装まで背景チェックが停止しうる。
        // 経過時間基準は時刻変更の影響を受けない。再起動でアラームは消えるため BootReceiver が再武装する。
        var triggerAt = SystemClock.ElapsedRealtime() + effectiveMs;

#if DEBUG
        if (DebugSchedulingConfig.AlarmOverrideMinutes > 0)
        {
            MessageService.Info($"[DEBUG] Alarm override active: firing in ~{effectiveMs / 60_000L} min");
        }
#endif

        // exact アラームは前面サービスの背面起動を許可する条件（Android 12+）に該当する。
        // API 31+ は権限の有無を実機判定し、不可なら不正確アラームへフォールバック。
        var useExact = Build.VERSION.SdkInt < BuildVersionCodes.S || am.CanScheduleExactAlarms();
        if (useExact)
        {
            am.SetExactAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, triggerAt, pi);
            MessageService.Info($"Update alarm (exact) scheduled in {intervalHours}h");
        }
        else
        {
            // 不正確アラーム → Doze 中もメンテ窓で発火するが、前面起動は電池最適化除外に依存。
            am.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, triggerAt, pi);
            MessageService.Info($"Update alarm (inexact) scheduled in {intervalHours}h");
        }

        // 間隔ミラーの更新はアラーム武装が成功した後にのみ行う。SetExact* が SecurityException 等で
        // throw した場合、ミラーは旧値のまま残り GetCachedIntervalHours が実機構と乖離しない
        // (ArmBoth のドリフト不変条件を維持: WorkManager 側も未変更で両機構が旧間隔で揃う)。
        Preferences.Set(IntervalPrefKey, intervalHours);
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

    /// <summary>
    /// アラーム発火時、直近に(いずれかの経路で)チェックが完了済みなら冗長な FGS 起動を省くべきか返す。
    /// WorkManager 定期が健全に動いている間はアラームを真のバックストップに留め、毎周期の常駐通知・
    /// 端末ウェイク・電池消費を避ける狙い。閾値は「実効間隔の半分」とし、これより新しい完了が
    /// あれば冗長とみなす。Doze 等で WorkManager が滞ると最終完了時刻が古くなりゲートを通過する。
    /// 完了時刻の記録は全経路の合流点 UpdateCheckService.CheckAllAsync が一元的に行う
    /// (SettingsKeys.LAST_CHECK_COMPLETED_MS)。
    /// </summary>
    public static bool ShouldSkipRedundantCheck()
    {
        var last = Preferences.Get(SettingsKeys.LAST_CHECK_COMPLETED_MS, 0L);
        if (last <= 0L) return false; // 完了履歴が無ければ必ず実行する。
        var elapsed = Java.Lang.JavaSystem.CurrentTimeMillis() - last;
        return elapsed >= 0 && elapsed < GetEffectiveIntervalMs(GetCachedIntervalHours()) / 2;
    }

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
