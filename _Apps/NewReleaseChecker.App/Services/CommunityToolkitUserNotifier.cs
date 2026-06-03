using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using NewReleaseChecker.Core.Abstractions;

namespace NewReleaseChecker.App.Services;

/// <summary>CommunityToolkit.Maui の Toast による IUserNotifier 実装。</summary>
public sealed class CommunityToolkitUserNotifier : IUserNotifier
{
    public Task ShowToastAsync(string message)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var toast = Toast.Make(message, ToastDuration.Short);
            await toast.Show();
        });
}
