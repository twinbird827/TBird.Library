using Android.Content;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// 更新チェックの二機構（WorkManager 定期ワーク + アラーム）を同一間隔でペア武装する共通ヘルパ。
/// 「間隔を一括指定する」経路 ―― 設定変更（<see cref="AndroidUpdateScheduler"/>）と DI/DB 利用不能時の
/// フォールバック ―― の重複だけを畳む。
///
/// 注意: 本クラスは drift を防ぐ単一の関門ではない。両機構を非対称に扱う正常経路(MainActivity の
/// 通常分岐・Worker/FGS の DB 再同期)は意図的に本クラスを経由せず各スケジューラを直接呼ぶ
/// (定期ワークを毎回再登録すると周期がリセットされ続けるため)。drift 是正の実体は ScheduleNext の
/// 冪等再武装が担う。
/// </summary>
public static class UpdateSchedulingCoordinator
{
    /// <summary>
    /// WorkManager 定期ワークとアラームを同一間隔で武装する。
    /// 間隔を一括指定する経路（設定変更時・DI/DB が利用不能なフォールバック時）で利用する。
    /// </summary>
    public static void ArmBoth(Context context, int intervalHours)
    {
        // 失敗しうる側(exact アラーム: 一部 OEM で権限失効時に SecurityException)を先に武装する。
        // ここで例外が出れば WorkManager 側は未変更で、両機構が旧間隔のまま揃う(=ドリフトしない。
        // 次回起動の MainActivity 冪等再武装で新間隔へ自己修復)。逆順だと WorkManager だけ新間隔で
        // アラームが旧間隔という不一致が残る。
        UpdateAlarmScheduler.ScheduleNext(context, intervalHours);          // Doze 貫通(失敗しうる)
        UpdateCheckScheduler.SchedulePeriodicCheck(context, intervalHours); // WiFi 等で堅実なベースライン
    }

    /// <summary>
    /// キャッシュ（Preferences ミラー）の間隔で両機構を武装する。
    /// DI/DB 未準備のフォールバック分岐用。
    /// </summary>
    public static void ArmBothFromCache(Context context)
        => ArmBoth(context, UpdateAlarmScheduler.GetCachedIntervalHours());
}
