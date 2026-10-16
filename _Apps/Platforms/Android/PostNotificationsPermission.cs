namespace LanobeReader.Platforms.Android;

public class PostNotificationsPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(global::Android.Manifest.Permission.PostNotifications, true)];
}
