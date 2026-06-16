using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Database;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// <see cref="IUpdateNotificationService"/> の Android 実装。
/// 新着のディープリンク先(最初の未読話)解決と、OEM ランチャー向け数字バッジ
/// (未確認更新を持つ小説数)の算出を行い、NotificationHelper で通知を表示する。
/// </summary>
public class UpdateNotificationService : IUpdateNotificationService
{
	private readonly EpisodeRepository _episodeRepo;
	private readonly NovelRepository _novelRepo;

	public UpdateNotificationService(EpisodeRepository episodeRepo, NovelRepository novelRepo)
	{
		_episodeRepo = episodeRepo;
		_novelRepo = novelRepo;
	}

	public async Task ShowUpdatesAsync(IReadOnlyList<(Novel novel, int newEpisodeCount)> updates)
	{
		if (updates.Count == 0) return;

		// アプリが前面にある間はシステム通知を出さない。アプリ内一覧が新着(NEW)を直接表示しており、
		// かつ MainActivity.OnResume の CancelAll が直後に消すため(= 機能同士の競合)。
		// この時点で DB は更新済みのため、ユーザは一覧画面で新着を確認できる。
		if (AppForegroundTracker.IsForeground) return;

		var context = global::Android.App.Application.Context;

		// OEM ランチャー(Samsung/Xiaomi 等)の数字バッジ用に「未確認更新を持つ小説数」を
		// COUNT クエリで算出(全件ロードを避ける)。
		var unconfirmedCount = await _novelRepo.CountUnconfirmedAsync().ConfigureAwait(false);

		for (int i = 0; i < updates.Count; i++)
		{
			var (novel, newCount) = updates[i];
			try
			{
				// ディープリンク先 = その小説の最初の未読話。未読が無ければ最後に読んだ話へ
				// フォールバックし、タップが無反応になる事態を避ける。
				var target = await _episodeRepo.GetFirstUnreadEpisodeAsync(novel.Id).ConfigureAwait(false)
					?? await _episodeRepo.GetLastReadEpisodeAsync(novel.Id).ConfigureAwait(false);
				var episodeId = target?.Id ?? 0;

				// 数字バッジは最後の 1 通にのみ総数を付ける。各通知に総数を付けると、通知ごとの
				// number を合算する OEM ランチャーで「通知数 × 総数」に膨らむため。
				// (合算式: 0+0+…+総数=総数 / 最大式: 総数 のどちらでも正しい値になる)
				var badge = (i == updates.Count - 1) ? unconfirmedCount : 0;

				NotificationHelper.ShowUpdateNotification(
					context,
					novel.Id, // notificationId = novel.Id（同一小説の通知は上書き）
					"ラノベリーダ",
					$"{novel.Title}: {newCount}話更新",
					novel.Id,
					episodeId,
					novel.SiteType,
					novel.NovelId,
					badge);
			}
			catch (Exception ex)
			{
				// 1 件の失敗で残りの通知が止まらないよう小説単位で握りつぶしてログのみ。
				MessageService.Warn($"ShowUpdateNotification failed for novel {novel.Id}: {ex.Message}");
			}
		}
	}
}
