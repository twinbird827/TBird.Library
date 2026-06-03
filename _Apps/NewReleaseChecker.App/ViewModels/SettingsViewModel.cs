using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Platforms.Android;
using NewReleaseChecker.Core.Abstractions;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>設定（SCR-010 / F-008）。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] IntervalKeys = { "daily_once", "daily_twice", "every_6h", "every_12h" };

    public string[] IntervalLabels { get; } = { "1日1回", "1日2回", "6時間ごと", "12時間ごと" };

    private readonly IPreferencesService _prefs;
    private readonly IWorkScheduler _scheduler;
    private bool _loaded;

    public SettingsViewModel(IPreferencesService prefs, IWorkScheduler scheduler)
    {
        _prefs = prefs;
        _scheduler = scheduler;
        Load();
    }

    [ObservableProperty] private bool _notificationEnabled;
    [ObservableProperty] private bool _autoCheckEnabled;
    [ObservableProperty] private int _intervalIndex;
    [ObservableProperty] private string _appVersion = string.Empty;

    private void Load()
    {
        NotificationEnabled = _prefs.NotificationEnabled;
        AutoCheckEnabled = _prefs.AutoCheckEnabled;
        IntervalIndex = Math.Max(0, Array.IndexOf(IntervalKeys, _prefs.AutoCheckInterval));
        AppVersion = $"バージョン {AppInfo.Current.VersionString}";
        _loaded = true;
    }

    partial void OnNotificationEnabledChanged(bool value)
    {
        if (!_loaded) return;
        _prefs.NotificationEnabled = value;
        // トグル ON 時は OS 通知許可を確認・再要求する。起動時の許可要求は 1 セッション 1 回（NotificationPermissionService）
        // のため、インストール時に拒否したユーザーが後から ON にしても許可が無いまま通知が無言で抑止されるのを防ぐ。
        if (value) _ = EnsureNotificationPermissionAsync();
    }

    private static async Task EnsureNotificationPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<PostNotificationsPermission>();
            if (status == PermissionStatus.Granted) return;

            status = await Permissions.RequestAsync<PostNotificationsPermission>();
            if (status == PermissionStatus.Granted) return;

            // 恒久拒否（OS がダイアログを出さない）状態。OS 設定画面へ誘導する。
            var openSettings = await Shell.Current.DisplayAlert(
                "通知が許可されていません",
                "新刊をお知らせするには、OS の設定でこのアプリの通知を許可してください。",
                "設定を開く", "あとで");
            if (openSettings) AppInfo.Current.ShowSettingsUI();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    partial void OnAutoCheckEnabledChanged(bool value)
    {
        if (!_loaded) return;
        _prefs.AutoCheckEnabled = value;
        ApplySchedule();
    }

    partial void OnIntervalIndexChanged(int value)
    {
        if (!_loaded || value < 0) return;
        _prefs.AutoCheckInterval = IntervalKeys[value];
        ApplySchedule();
    }

    private void ApplySchedule()
    {
        if (AutoCheckEnabled) _scheduler.Schedule(_prefs.AutoCheckInterval);
        else _scheduler.Cancel();
    }

    [RelayCommand]
    private async Task OpenExcludeKeywordsAsync()
        => await Shell.Current.GoToAsync(Routes.ExcludeKeywords);
}
