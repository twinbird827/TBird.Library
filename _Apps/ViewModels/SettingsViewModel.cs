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
    private int _cacheMonths = SettingsKeys.DEFAULT_CACHE_MONTHS;

    [ObservableProperty]
    private int _updateIntervalHours = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS;

    [ObservableProperty]
    private int _fontSizeSp = SettingsKeys.DEFAULT_FONT_SIZE_SP;

    [ObservableProperty]
    private int _backgroundTheme;

    [ObservableProperty]
    private int _lineSpacing = SettingsKeys.DEFAULT_LINE_SPACING;

    [ObservableProperty]
    private int _episodesPerPage = SettingsKeys.DEFAULT_EPISODES_PER_PAGE;

    [ObservableProperty]
    private string _previewText = "サンプルテキストです。フォントサイズと行間のプレビューを表示しています。";

    [ObservableProperty]
    private bool _verticalWriting;

    [ObservableProperty]
    private bool _prefetchEnabled = true;

    [ObservableProperty]
    private int _requestDelayMs = SettingsKeys.DEFAULT_REQUEST_DELAY_MS;

    public async Task InitializeAsync()
    {
        CacheMonths = await _settingsRepo.GetIntValueAsync(SettingsKeys.CACHE_MONTHS, SettingsKeys.DEFAULT_CACHE_MONTHS);
        UpdateIntervalHours = await _settingsRepo.GetIntValueAsync(SettingsKeys.UPDATE_INTERVAL_HOURS, SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS);
        FontSizeSp = await _settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, SettingsKeys.DEFAULT_FONT_SIZE_SP);
        BackgroundTheme = await _settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, SettingsKeys.DEFAULT_BACKGROUND_THEME);
        LineSpacing = await _settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, SettingsKeys.DEFAULT_LINE_SPACING);
        EpisodesPerPage = await _settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, SettingsKeys.DEFAULT_EPISODES_PER_PAGE);
        VerticalWriting = await _settingsRepo.GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, SettingsKeys.DEFAULT_VERTICAL_WRITING) == 1;
        PrefetchEnabled = await _settingsRepo.GetIntValueAsync(SettingsKeys.PREFETCH_ENABLED, SettingsKeys.DEFAULT_PREFETCH_ENABLED) == 1;
        RequestDelayMs = await _settingsRepo.GetIntValueAsync(SettingsKeys.REQUEST_DELAY_MS, SettingsKeys.DEFAULT_REQUEST_DELAY_MS);
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

    partial void OnVerticalWritingChanged(bool value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.VERTICAL_WRITING, value ? "1" : "0");

    partial void OnPrefetchEnabledChanged(bool value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.PREFETCH_ENABLED, value ? "1" : "0");

    partial void OnRequestDelayMsChanged(int value) =>
        _ = _settingsRepo.SetValueAsync(SettingsKeys.REQUEST_DELAY_MS, Math.Clamp(value, SettingsKeys.MIN_REQUEST_DELAY_MS, SettingsKeys.MAX_REQUEST_DELAY_MS).ToString());

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "確認", "すべてのキャッシュを削除しますか？", "OK", "キャンセル");

        if (confirm)
        {
            await _cacheRepo.DeleteAllAsync();
            // Snackbar-like notification
            await Shell.Current.DisplayAlert("完了", "クリアしました", "OK");
        }
    }
}
