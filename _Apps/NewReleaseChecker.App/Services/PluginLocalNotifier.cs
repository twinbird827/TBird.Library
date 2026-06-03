using NewReleaseChecker.Core.Abstractions;
using Plugin.LocalNotification;

namespace NewReleaseChecker.App.Services;

/// <summary>Plugin.LocalNotification による ILocalNotifier 実装。</summary>
public sealed class PluginLocalNotifier : ILocalNotifier
{
    // 集約通知は単一 ID で上書きする（鳴り分けない）
    private const int NewReleaseNotificationId = 1000;

    public async Task ShowAsync(string title, string message, int? tapSeriesId = null)
    {
        var request = new NotificationRequest
        {
            NotificationId = NewReleaseNotificationId,
            Title = title,
            Description = message,
            // タップ時にシリーズ詳細へ遷移するための識別子（MauiProgram でハンドル）
            ReturningData = tapSeriesId?.ToString() ?? string.Empty,
        };

        await LocalNotificationCenter.Current.Show(request);
    }
}
