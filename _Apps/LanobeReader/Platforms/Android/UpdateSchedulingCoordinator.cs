using Android.Content;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// 更新チェックの二機構（WorkManager 定期ワーク + setAndAllowWhileIdle アラーム）を
/// 同一間隔でペア武装する共通ヘルパ。間隔が「一括で指定される」経路
/// ―― 設定変更（<see cref="AndroidUpdateScheduler"/>）と、DI/DB 利用不能時の
/// フォールバック（MainActivity の OnCreate 早期 return 分岐）―― の重複を畳む。
///
/// ※ 重要: 本クラスは drift を強制的に防ぐ「単一の関門」ではない。両機構を別々に扱うべき
///   正常経路は、意図的に本クラスを経由せず各スケジューラを直接呼ぶ:
///   - MainActivity 正常経路: 定期ワークは「DB 値 != 前回 schedule 値」のときのみ再登録し
///     毎起動の周期リセットを避ける一方、アラームは毎起動で冪等に再武装する（非対称なため）。
///   - MainActivity catch フォールバック: 片方の失敗でもう片方を巻き込まないよう各機構を独立 try で武装する。
///   - Worker / FGS の DB 再同期: 定期ワークは Preferences ミラーに依存せず drift しないため、
///     毎実行で再登録すると周期がリセットされ続ける。アラーム（Preferences ミラー依存）のみ是正する。
///   これらの経路を本クラスへ無理に集約すると上記の周期リセット等を招くため、共通化はあくまで
///   「間隔を一括指定する経路」に限定している。drift 是正の実体は ScheduleNext の冪等再武装が担う。
/// </summary>
public static class UpdateSchedulingCoordinator
{
    /// <summary>
    /// WorkManager 定期ワークとアラームを同一間隔で武装する。
    /// 間隔を一括指定する経路（設定変更時・DI/DB が利用不能なフォールバック時）で利用する。
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
