using LanobeReader.Services;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// <see cref="IUpdateScheduler"/> の Android 実装。WorkManager の定期ワークを再登録する。
/// </summary>
public class AndroidUpdateScheduler : IUpdateScheduler
{
	public void Schedule(int intervalHours)
	{
		// 二機構（WorkManager + アラーム）の同一間隔武装は UpdateSchedulingCoordinator に一元化。
		UpdateSchedulingCoordinator.ArmBoth(global::Android.App.Application.Context, intervalHours);
	}
}
