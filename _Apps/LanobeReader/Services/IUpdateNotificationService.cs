using LanobeReader.Models;

namespace LanobeReader.Services;

/// <summary>
/// 新着更新をユーザに通知する抽象。
/// フォアグラウンド(アプリ起動時の CheckAllAsync)・バックグラウンド(WorkManager)の
/// どちらの検出経路からも同じ通知を出すために導入。
/// 従来はバックグラウンド Worker のみが通知を出していたため、アプリ起動時の検出が
/// 新着を DB に取り込んで「黙って消費」し、通知が出ない問題があった。
/// Android 実装が NotificationHelper を呼ぶ。
/// </summary>
public interface IUpdateNotificationService
{
	/// <summary>
	/// 検出された更新の一覧について新着通知を表示する。空なら何もしない。
	/// </summary>
	Task ShowUpdatesAsync(IReadOnlyList<(Novel novel, int newEpisodeCount)> updates);
}
