using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsRepository _settingsRepo;
    private readonly EpisodeCacheRepository _cacheRepo;

    public SettingsViewModel(AppSettingsRepository settingsRepo, EpisodeCacheRepository cacheRepo)
    {
        _settingsRepo = settingsRepo;
        _cacheRepo = cacheRepo;
    }

    [ObservableProperty]
    private int _cacheMonths = 3;

    [ObservableProperty]
    private int _updateIntervalHours = 6;

    [ObservableProperty]
    private int _fontSizeSp = 16;

    [ObservableProperty]
    private int _backgroundTheme;

    [ObservableProperty]
    private int _lineSpacing = 1;

    [ObservableProperty]
    private int _episodesPerPage = 50;

    [ObservableProperty]
    private string _previewText = "サンプルテキストです。フォントサイズと行間のプレビューを表示しています。";

    public async Task InitializeAsync()
    {
        CacheMonths = await _settingsRepo.GetIntValueAsync(SettingsKeys.CACHE_MONTHS, 3).ConfigureAwait(false);
        UpdateIntervalHours = await _settingsRepo.GetIntValueAsync(SettingsKeys.UPDATE_INTERVAL_HOURS, 6).ConfigureAwait(false);
        FontSizeSp = await _settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, 16).ConfigureAwait(false);
        BackgroundTheme = await _settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, 0).ConfigureAwait(false);
        LineSpacing = await _settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, 1).ConfigureAwait(false);
        EpisodesPerPage = await _settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, 50).ConfigureAwait(false);
    }

    partial void OnCacheMonthsChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.CACHE_MONTHS, value.ToString());

    partial void OnUpdateIntervalHoursChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.UPDATE_INTERVAL_HOURS, value.ToString());

    partial void OnFontSizeSpChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.FONT_SIZE_SP, value.ToString());

    partial void OnBackgroundThemeChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.BACKGROUND_THEME, value.ToString());

    partial void OnLineSpacingChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.LINE_SPACING, value.ToString());

    partial void OnEpisodesPerPageChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.EPISODES_PER_PAGE, value.ToString());

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "確認", "すべてのキャッシュを削除しますか？", "OK", "キャンセル");

        if (confirm)
        {
            await _cacheRepo.DeleteAllAsync().ConfigureAwait(false);
            // Snackbar-like notification
            await Shell.Current.DisplayAlert("完了", "クリアしました", "OK");
        }
    }
}
