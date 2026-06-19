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

		// 前面 かつ 新着を即時表示する一覧(本棚/目次)が可視の間はシステム通知を出さない。アプリ内一覧が
		// 新着(NEW)を直接表示し、かつ MainActivity.OnResume の CancelAll が直後に消すため(= 機能同士の競合)。
		// 前面でも一覧非表示の画面(リーダー/設定)滞在中は抑止すると新着が全く可視化されないため通知する。
		if (AppForegroundTracker.ShouldSuppressSystemNotification) return;

		var context = global::Android.App.Application.Context;

		// バッジ数とディープリンク先を解決する。どちらも通知本体とは別物のため、ここでのクエリ失敗で
		// 通知全体を止めない: バッジは表示中の通知数、遷移先は話一覧(episodeId=0)へフォールバックし、
		// 通知だけは確実に投稿する。
		int badgeTotal;
		Dictionary<int, int> targets;
		try
		{
			// OEM ランチャー(Samsung/Xiaomi 等)の数字バッジ用に「未確認更新を持つ小説数」を
			// COUNT クエリで算出(全件ロードを避ける)。
			var unconfirmedCount = await _novelRepo.CountUnconfirmedAsync().ConfigureAwait(false);
			// 別クエリの COUNT は、挿入とこの COUNT の間に確認操作/CancelAll が走ると今回投稿する
			// 通知数を下回りうる(0 になることも)。表示中の通知数を下回らないよう下限を担保する。
			badgeTotal = Math.Max(unconfirmedCount, updates.Count);

			// ディープリンク先(各小説の最初の未読話、無ければ最後に読んだ話)を 1 度の集約クエリで解決。
			// 作品ごとに 2 クエリを逐次発行する従来方式(最大 2×N 往復)を避ける。
			var novelIds = updates.Select(u => u.novel.Id).ToList();
			targets = await _episodeRepo.GetDeepLinkTargetEpisodeIdsAsync(novelIds).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			MessageService.Warn($"Notification metadata resolution failed; using fallback: {ex.Message}");
			badgeTotal = updates.Count;
			targets = new Dictionary<int, int>();
		}

		// await 中に前面化していても、投稿可否は NotificationHelper が「前面判定 + 全件 Notify」を原子的に
		// 行って決める(CancelAll と相互排他)。前面化後に投稿した通知が居座る TOCTOU はそこで閉じるため、
		// ここで個別に再判定する必要はない(従来の await 後/反復ごとの IsForeground チェックを撤去)。

		// 数字バッジ総数は先頭 1 通にのみ付ける。各通知に総数を付けると number を合算する OEM ランチャーで
		// 膨らむため 1 通に限定する。投稿は all-or-nothing(NotificationHelper.ShowUpdateNotifications が
		// バッチ全体で抑止を 1 回確定)なので、従来の「成功した最初の通知へ載せる」動的判定は不要で、
		// 先頭固定でバッジ取りこぼしは起きない。
		var items = new List<NotificationHelper.UpdateNotificationItem>(updates.Count);
		for (int i = 0; i < updates.Count; i++)
		{
			var (novel, newCount) = updates[i];
			// ディープリンク先 = その小説の最初の未読話(無ければ最後に読んだ話)。
			// 解決できない場合は 0 とし、タップ時は話一覧へ遷移する(MainActivity 側)。
			var episodeId = targets.TryGetValue(novel.Id, out var id) ? id : 0;
			items.Add(new NotificationHelper.UpdateNotificationItem(
				NotificationId: novel.Id, // novel.Id（同一小説の通知は上書き）
				Title: "ラノベリーダ",
				Body: $"{novel.Title}: {newCount}話更新",
				NovelId: novel.Id,
				EpisodeId: episodeId,
				SiteType: novel.SiteType,
				SiteNovelId: novel.NovelId,
				BadgeNumber: i == 0 ? badgeTotal : 0));
		}

		try
		{
			NotificationHelper.ShowUpdateNotifications(context, items);
		}
		catch (Exception ex)
		{
			MessageService.Warn($"ShowUpdateNotifications failed: {ex.Message}");
		}
	}
}
