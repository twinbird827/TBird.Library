using TBird.Core;

namespace TBird.Maui;

/// <summary>
/// 通知許可（POST_NOTIFICATIONS 等）をユーザーに依頼するヘルパー。
/// 1 セッション内で 1 度のみ実行され、Granted ならそのまま返す。
/// Rationale が必要な場合は Shell.Current.DisplayAlert で同意を取ってから RequestAsync を呼ぶ。
/// </summary>
/// <typeparam name="TPermission">
/// Android 等のプラットフォーム固有 Permission クラス。
/// アプリ側で BasePlatformPermission を継承した型を渡す（例: PostNotificationsPermission）。
/// </typeparam>
public class NotificationPermissionService<TPermission>
    where TPermission : Permissions.BasePlatformPermission, new()
{
    private readonly string _title;
    private readonly string _message;
    private readonly string _acceptLabel;
    private readonly string _declineLabel;
    private bool _requestedThisSession;

    public NotificationPermissionService(string title, string message, string acceptLabel, string declineLabel)
    {
        _title = title;
        _message = message;
        _acceptLabel = acceptLabel;
        _declineLabel = declineLabel;
    }

    public async Task EnsureRequestedAsync()
    {
        if (_requestedThisSession) return;
        _requestedThisSession = true;

        var status = await Permissions.CheckStatusAsync<TPermission>();
        if (status == PermissionStatus.Granted) return;

        if (Permissions.ShouldShowRationale<TPermission>())
        {
            var accepted = await Shell.Current.DisplayAlert(_title, _message, _acceptLabel, _declineLabel);
            if (!accepted)
            {
                MessageService.Info("User dismissed rationale dialog");
                return;
            }
        }

        var result = await Permissions.RequestAsync<TPermission>();
        MessageService.Info($"Notification permission request result: {result}");
    }
}
