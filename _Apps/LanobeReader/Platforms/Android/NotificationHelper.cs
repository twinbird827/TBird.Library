using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using AndroidX.Core.App;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

public static class NotificationHelper
{
	public const string UPDATE_CHANNEL_ID = "update_notification";

	// 通知の「前面判定 + 投稿」と「CancelAll」を相互排他にするゲート。
	// 背面ワーカが通知を投稿する処理と、UI スレッドの MainActivity.OnResume が呼ぶ CancelAll が
	// 直列化されないと、CancelAll 後に投稿された通知が消えず残る TOCTOU が起きる。両者を同一ロックで
	// 包むことで、(投稿側がロック獲得→前面なら投稿せず) / (CancelAll 獲得→全消去) のいずれかに収束し、
	// 「前面化後に投稿された通知が居座る」窓を閉じる。前面判定は AppForegroundTracker(可視 Activity 数)。
	private static readonly object _notifyGate = new object();

	public static void CreateNotificationChannels(Activity activity)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
		{
			var updateChannel = new NotificationChannel(
				UPDATE_CHANNEL_ID, "更新通知", NotificationImportance.Default)
			{
				Description = "小説の更新通知"
			};

			var manager = activity.GetSystemService(Context.NotificationService) as NotificationManager;
			// ランチャーアイコンの通知ドット(新着バッジ)を明示的に有効化。
			// 既定でも true だが意図を明示。チャンネルは作成後イミュータブルのため、
			// 既存インストールではアプリ再インストール/データ削除まで反映されない (Android 仕様)。
			updateChannel.SetShowBadge(true);
			manager?.CreateNotificationChannel(updateChannel);
		}
	}

	public static bool HasNotificationPermission(Context context)
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return true;
		return AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
			context, global::Android.Manifest.Permission.PostNotifications)
			== global::Android.Content.PM.Permission.Granted;
	}

	/// <summary>一括投稿する新着通知 1 通分のデータ。</summary>
	public readonly record struct UpdateNotificationItem(
		int NotificationId, string Title, string Body,
		int NovelId, int EpisodeId, int SiteType, string SiteNovelId, int BadgeNumber);

	private static Notification BuildUpdateNotification(Context context, UpdateNotificationItem item)
	{
		var intent = new Intent(context, typeof(MainActivity));
		intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
		intent.PutExtra("novelId", item.NovelId);
		intent.PutExtra("episodeId", item.EpisodeId);
		intent.PutExtra("siteType", item.SiteType);
		intent.PutExtra("siteNovelId", item.SiteNovelId);

		var pendingIntent = PendingIntent.GetActivity(
			context, item.NotificationId, intent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

		var builder = new NotificationCompat.Builder(context, UPDATE_CHANNEL_ID)!
			.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)!
			.SetContentTitle(item.Title)!
			.SetContentText(item.Body)!
			// dismissible: ユーザがスワイプで消せる。タップで開いた場合も自動で消える。
			// アプリ前面化時(MainActivity.OnResume)にも CancelAll でクリアし、バッジ(通知ドット)を消す。
			// ※通知をスワイプで消すとバッジも消えるのは Android 仕様(バッジは通知に紐づく)。
			.SetAutoCancel(true)!
			.SetContentIntent(pendingIntent)!
			.SetPriority(NotificationCompat.PriorityDefault)!;

		// OEM ランチャー(Samsung/Xiaomi 等)向けに数字バッジを設定。0 のときは点(ドット)のみ。
		if (item.BadgeNumber > 0)
		{
			builder.SetNumber(item.BadgeNumber);
		}

		return builder.Build()!;
	}

	/// <summary>
	/// 複数の新着通知を「単一の抑止判定」で一括投稿する。抑止可否はバッチ開始時に 1 回だけ確定し、投稿は
	/// 全件まとめて <see cref="_notifyGate"/> 内で行う(CancelAll と原子的)。実際に投稿できたとき true を返す
	/// (権限なし・前面化により抑止したときは false)。
	///
	/// 通知ごとに抑止を再判定すると、投函途中の前面化で一部だけ投稿され、直後の MainActivity.OnResume の
	/// CancelAll がそれを消してバッジ総数まで失う「半抑止バッチ」になりうる。バッチ全体で抑止を 1 回確定し
	/// all-or-nothing にすることでこれを防ぐ。抑止判定(前面 かつ 一覧可視)と Notify を同一ロックで原子的に
	/// 行うため、CancelAll と「投稿後に消える/消した後に投稿される」競合も生じない。前面化は OnActivityStarted
	/// で OnResume の CancelAll より先に立つ。前面でも一覧非表示なら抑止せず投稿し新着を確実に可視化する。
	/// </summary>
	public static bool ShowUpdateNotifications(Context context, IReadOnlyList<UpdateNotificationItem> items)
	{
		if (items.Count == 0) return false;
		if (!HasNotificationPermission(context)) return false;

		// Notify 直前の原子区間(ロック保持時間)を短く保つため、通知ビルドはロック外で済ませる。
		// build は item 単位で隔離する。1 件のビルド失敗(OEM 由来/不正データ等)で他の新着通知まで
		// 巻き込んで全損させないため、失敗は warn ログのみで残りを継続する。
		var built = new List<(int id, Notification notification)>(items.Count);
		foreach (var item in items)
		{
			try { built.Add((item.NotificationId, BuildUpdateNotification(context, item))); }
			catch (Exception ex) { MessageService.Warn($"build notification failed for {item.NovelId}: {ex.Message}"); }
		}
		if (built.Count == 0) return false;

		lock (_notifyGate)
		{
			// バッチ全体で 1 回だけ抑止判定し、全件まとめて投稿する(CancelAll と相互排他)。
			if (AppForegroundTracker.ShouldSuppressSystemNotification) return false;
			var manager = NotificationManagerCompat.From(context);
			// manager==null なら Notify は no-op で 1 件も投稿できない。ループ前に 1 回だけ判定して早期 return し、
			// null no-op のまま anyPosted=true になる経路自体を消す(全損が無音化しないよう warn を 1 行残す)。
			if (manager is null)
			{
				MessageService.Warn($"NotificationManagerCompat.From returned null; {built.Count} notifications not posted");
				return false;
			}
			var anyPosted = false;
			foreach (var (id, notification) in built)
			{
				// Notify も item 単位で隔離する。1 件の投稿失敗(RemoteServiceException/too-many-pending-
				// intents 等)で残り全件を破棄しないため、失敗は warn のみで継続する。
				try { manager.Notify(id, notification); anyPosted = true; }
				catch (Exception ex) { MessageService.Warn($"Notify failed for {id}: {ex.Message}"); }
			}
			// 全件失敗は anyPosted=false となり「抑止(前面)」と戻り値が区別できず、例外も投げないため外側
			// catch にも乗らない。全損が無音化しないよう、ここで全損サマリを 1 行だけ残す(観測性維持)。
			if (!anyPosted) MessageService.Warn($"all {built.Count} update notifications failed to post");
			return anyPosted;
		}
	}

	/// <summary>
	/// 新着「更新通知」(<see cref="UPDATE_CHANNEL_ID"/>)のみをクリアする(=ランチャーアイコンのバッジも消える)。
	/// 「アプリを開くまでバッジを維持」を実現するため、アプリ前面化時に呼ぶ。
	/// 実行中の前面サービス(<c>update_check_progress</c> チャンネルの常駐通知)は対象外。CancelAll で
	/// 巻き込むと進行中チェックの前面通知が消え、サービスの前面状態に干渉するため、チャンネルで限定する。
	/// </summary>
	public static void CancelAll(Context context)
	{
		// ShowUpdateNotifications の「前面判定 + 投稿」と相互排他にし、前面化直後の投稿が消え残るのを防ぐ。
		lock (_notifyGate)
		{
			if (Build.VERSION.SdkInt >= BuildVersionCodes.M
				&& context.GetSystemService(Context.NotificationService) is NotificationManager manager)
			{
				foreach (var sbn in manager.GetActiveNotifications() ?? Array.Empty<StatusBarNotification>())
				{
					if (sbn.Notification?.ChannelId == UPDATE_CHANNEL_ID)
					{
						manager.Cancel(sbn.Tag, sbn.Id);
					}
				}
				return;
			}

			// 旧 OS フォールバック(minSdk 上は到達しない)。
			NotificationManagerCompat.From(context)?.CancelAll();
		}
	}
}