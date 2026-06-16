using Android.Content;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// 更新チェックの二機構（WorkManager 定期ワーク + setAndAllowWhileIdle アラーム）を
/// 「常に同一間隔でペア武装する」唯一の地点。
/// MainActivity のフォールバック各分岐・設定変更（<see cref="AndroidUpdateScheduler"/>）は
/// 必ずここを経由し、両機構の間隔が静かに乖離（drift）する事態を構造的に防ぐ。
///
/// ※ 通常起動の正常経路（MainActivity）と Worker の DB 再同期は、意図的にここを経由しない:
///   - 正常経路: 定期ワークは「DB 値 != 前回 schedule 値」のときのみ再登録し、毎起動での
///     周期リセットを避ける。アラームのみ毎起動で冪等に再武装する。
///   - Worker 再同期: 定期ワークは Preferences ミラーに依存せず drift しないため、
///     毎実行で再登録すると周期がリセットされ続ける。アラーム（Preferences ミラー依存）のみ是正する。
/// </summary>
public static class UpdateSchedulingCoordinator
{
    /// <summary>
    /// WorkManager 定期ワークとアラームを同一間隔で武装する。
    /// 設定変更時・DI/DB が利用不能なフォールバック時の唯一の経路。
    /// </summary>
    public static void ArmBoth(Context context, int intervalHours)
    {
        UpdateCheckScheduler.SchedulePeriodicCheck(context, intervalHours); // WiFi 等で堅実なベースライン
        UpdateAlarmScheduler.ScheduleNext(context, intervalHours);          // Doze 貫通
    }

    /// <summary>
    /// キャッシュ（Preferences ミラー）の間隔で両機構を武装する。
    /// DI/DB 未準備のフォールバック分岐用。
    /// </summary>
    public static void ArmBothFromCache(Context context)
        => ArmBoth(context, UpdateAlarmScheduler.GetCachedIntervalHours());
}
