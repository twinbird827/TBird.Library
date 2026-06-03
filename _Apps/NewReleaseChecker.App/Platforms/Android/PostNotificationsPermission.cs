namespace NewReleaseChecker.App.Platforms.Android;

/// <summary>POST_NOTIFICATIONS（Android 13+）のランタイム権限定義。</summary>
public sealed class PostNotificationsPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new (string, bool)[]
        {
            (global::Android.Manifest.Permission.PostNotifications, true),
        };
}
