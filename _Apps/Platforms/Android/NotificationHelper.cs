using Android.App;
using Android.Content;
using Android.OS;
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
		int novelId, int episodeId, int siteType, string siteNovelId)
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

		var notification = new NotificationCompat.Builder(context, UPDATE_CHANNEL_ID)!
			.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)!
			.SetContentTitle(title)!
			.SetContentText(body)!
			.SetAutoCancel(true)!
			.SetContentIntent(pendingIntent)!
			.SetPriority(NotificationCompat.PriorityDefault)!
			.Build();

		NotificationManagerCompat.From(context)?.Notify(notificationId, notification!);
	}
}