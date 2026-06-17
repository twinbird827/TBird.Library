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

		// await 中に前面化していても、投稿可否は NotificationHelper が「前面判定 + Notify」を原子的に
		// 行って決める(CancelAll と相互排他)。前面化後に投稿した通知が居座る TOCTOU はそこで閉じるため、
		// ここで個別に再判定する必要はない(従来の await 後/反復ごとの IsForeground チェックを撤去)。

		// 数字バッジ総数は「最初に投稿が成功した 1 通」にのみ付ける。各通知に総数を付けると number を
		// 合算する OEM ランチャーで膨らむため 1 通に限定しつつ、固定位置(従来は最後の 1 通)だとその通知の
		// 投稿失敗でバッジが 0 化するため、成功した最初の通知へ載せて取りこぼしを防ぐ。
		var badgePlaced = false;
		for (int i = 0; i < updates.Count; i++)
		{
			var (novel, newCount) = updates[i];
			try
			{
				// ディープリンク先 = その小説の最初の未読話(無ければ最後に読んだ話)。
				// 解決できない場合は 0 とし、タップ時は話一覧へ遷移する(MainActivity 側)。
				var episodeId = targets.TryGetValue(novel.Id, out var id) ? id : 0;

				var badge = badgePlaced ? 0 : badgeTotal;

				// 戻り値 = 実際に投稿できたか(権限なし/前面化による抑止時は false)。投稿できたときだけ
				// バッジ確定し、抑止・失敗時は badgePlaced を立てず次の通知へ総数を持ち越す。
				if (NotificationHelper.ShowUpdateNotification(
					context,
					novel.Id, // notificationId = novel.Id（同一小説の通知は上書き）
					"ラノベリーダ",
					$"{novel.Title}: {newCount}話更新",
					novel.Id,
					episodeId,
					novel.SiteType,
					novel.NovelId,
					badge))
				{
					badgePlaced = true;
				}
			}
			catch (Exception ex)
			{
				// 1 件の失敗で残りの通知が止まらないよう小説単位で握りつぶしてログのみ。
				MessageService.Warn($"ShowUpdateNotification failed for novel {novel.Id}: {ex.Message}");
			}
		}
	}
}
