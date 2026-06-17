using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using AndroidX.Core.App;

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

	/// <summary>
	/// 新着通知を投稿する。実際に投稿できたとき true を返す(権限なし・前面化により抑止したときは false)。
	/// 呼び出し側はこの戻り値で「バッジを載せた通知が出たか」を判定できる。
	/// </summary>
	public static bool ShowUpdateNotification(Context context, int notificationId, string title, string body,
		int novelId, int episodeId, int siteType, string siteNovelId, int badgeNumber = 0)
	{
		if (!HasNotificationPermission(context)) return false;

		var intent = new Intent(context, typeof(MainActivity));
		intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
		intent.PutExtra("novelId", novelId);
		intent.PutExtra("episodeId", episodeId);
		intent.PutExtra("siteType", siteType);
		intent.PutExtra("siteNovelId", siteNovelId);

		var pendingIntent = PendingIntent.GetActivity(
			context, notificationId, intent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

		var builder = new NotificationCompat.Builder(context, UPDATE_CHANNEL_ID)!
			.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)!
			.SetContentTitle(title)!
			.SetContentText(body)!
			// dismissible: ユーザがスワイプで消せる。タップで開いた場合も自動で消える。
			// アプリ前面化時(MainActivity.OnResume)にも CancelAll でクリアし、バッジ(通知ドット)を消す。
			// ※通知をスワイプで消すとバッジも消えるのは Android 仕様(バッジは通知に紐づく)。
			.SetAutoCancel(true)!
			.SetContentIntent(pendingIntent)!
			.SetPriority(NotificationCompat.PriorityDefault)!;

		// OEM ランチャー(Samsung/Xiaomi 等)向けに数字バッジを設定。0 のときは点(ドット)のみ。
		if (badgeNumber > 0)
		{
			builder.SetNumber(badgeNumber);
		}

		// 抑止判定(前面 かつ 一覧可視)と Notify をロック内で原子的に行う。抑止条件が真なら投稿しない
		// (アプリ内一覧が NEW を表示し、CancelAll が直後に消すため)。CancelAll と同一ロックのため
		// 「CancelAll 後に投稿して居座る」競合が生じない。前面化は OnActivityStarted で OnResume の
		// CancelAll より先に立つ。前面でも一覧非表示なら抑止せず投稿し、新着を確実に可視化する。
		lock (_notifyGate)
		{
			if (AppForegroundTracker.ShouldSuppressSystemNotification) return false;
			NotificationManagerCompat.From(context)?.Notify(notificationId, builder.Build()!);
			return true;
		}
	}

	/// <summary>
	/// 全ての新着通知をクリアする(=ランチャーアイコンのバッジも消える)。
	/// 「アプリを開くまでバッジを維持」を実現するため、アプリ前面化時に呼ぶ。
	/// </summary>
	public static void CancelAll(Context context)
	{
		// ShowUpdateNotification の「前面判定 + 投稿」と相互排他にし、前面化直後の投稿が消え残るのを防ぐ。
		lock (_notifyGate)
		{
			NotificationManagerCompat.From(context)?.CancelAll();
		}
	}
}