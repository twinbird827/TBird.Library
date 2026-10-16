namespace LanobeReader.Services;

/// <summary>
/// 定期更新チェック(WorkManager)のスケジュール抽象。
/// 設定画面で更新間隔を変更した際に即時に再スケジュールするために導入。
/// 従来は次回アプリ起動時(MainActivity の差分チェック)まで反映されなかった。
/// </summary>
public interface IUpdateScheduler
{
	/// <summary>
	/// 指定した間隔(時間)で定期更新チェックを再スケジュールする。
	/// </summary>
	void Schedule(int intervalHours);
}
