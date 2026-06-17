using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using AndroidX.Core.App;

namespace LanobeReader.Platforms.Android;

public static class NotificationHelper
{
	public const string UPDATE_CHANNEL_ID = "update_notification";

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

	public static void ShowUpdateNotification(Context context, int notificationId, string title, string body,
		int novelId, int episodeId, int siteType, string siteNovelId, int badgeNumber = 0)
	{
		if (!HasNotificationPermission(context)) return;

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

		NotificationManagerCompat.From(context)?.Notify(notificationId, builder.Build()!);
	}

	/// <summary>
	/// 新着「更新通知」(<see cref="UPDATE_CHANNEL_ID"/>)のみをクリアする(=ランチャーアイコンのバッジも消える)。
	/// 「アプリを開くまでバッジを維持」を実現するため、アプリ前面化時に呼ぶ。
	/// 実行中の前面サービス(<c>update_check_progress</c> チャンネルの常駐通知)は対象外。CancelAll で
	/// 巻き込むと進行中チェックの前面通知が消え、サービスの前面状態に干渉するため、チャンネルで限定する。
	/// </summary>
	public static void CancelAll(Context context)
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