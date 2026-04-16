using LanobeReader.Helpers;
using LanobeReader.Platforms.Android;

namespace LanobeReader.Services;

public class NotificationPermissionService
{
    private bool _requestedThisSession;

    public async Task EnsureRequestedAsync()
    {
        if (_requestedThisSession) return;
        _requestedThisSession = true;

        var status = await Permissions.CheckStatusAsync<PostNotificationsPermission>();
        if (status == PermissionStatus.Granted) return;

        if (Permissions.ShouldShowRationale<PostNotificationsPermission>())
        {
            var accepted = await Shell.Current.DisplayAlert(
                "通知の許可",
                "小説の更新をお知らせするために通知権限が必要です。許可しますか？",
                "許可する",
                "後で");
            if (!accepted)
            {
                LogHelper.Info(nameof(NotificationPermissionService), "User dismissed rationale dialog");
                return;
            }
        }

        var result = await Permissions.RequestAsync<PostNotificationsPermission>();
        LogHelper.Info(nameof(NotificationPermissionService), $"POST_NOTIFICATIONS request result: {result}");
    }
}
