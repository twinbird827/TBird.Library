using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class SettingsViewModel : ErrorAwareViewModel
{
    private readonly AppSettingsRepository _settingsRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private bool _isInitializing;

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
    private bool _autoMarkReadEnabled = true;

    [ObservableProperty]
    private int _requestDelayMs = SettingsKeys.DEFAULT_REQUEST_DELAY_MS;

    public async Task InitializeAsync()
    {
        _isInitializing = true;
        try
        {
            CacheMonths = await _settingsRepo.GetIntValueAsync(SettingsKeys.CACHE_MONTHS, SettingsKeys.DEFAULT_CACHE_MONTHS);
            UpdateIntervalHours = await _settingsRepo.GetIntValueAsync(SettingsKeys.UPDATE_INTERVAL_HOURS, SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS);
            FontSizeSp = await _settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, SettingsKeys.DEFAULT_FONT_SIZE_SP);
            BackgroundTheme = await _settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, SettingsKeys.DEFAULT_BACKGROUND_THEME);
            LineSpacing = await _settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, SettingsKeys.DEFAULT_LINE_SPACING);
            EpisodesPerPage = await _settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, SettingsKeys.DEFAULT_EPISODES_PER_PAGE);
            VerticalWriting = await _settingsRepo.GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, SettingsKeys.DEFAULT_VERTICAL_WRITING) == 1;
            PrefetchEnabled = await _settingsRepo.GetIntValueAsync(SettingsKeys.PREFETCH_ENABLED, SettingsKeys.DEFAULT_PREFETCH_ENABLED) == 1;
            AutoMarkReadEnabled = await _settingsRepo.GetIntValueAsync(SettingsKeys.AUTO_MARK_READ_ENABLED, SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED) == 1;
            RequestDelayMs = await _settingsRepo.GetIntValueAsync(SettingsKeys.REQUEST_DELAY_MS, SettingsKeys.DEFAULT_REQUEST_DELAY_MS);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private readonly Dictionary<string, CancellationTokenSource> _debounceCts = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(400);

    private void DebounceSave(string key, string value)
    {
        if (_isInitializing) return;
        if (_debounceCts.TryGetValue(key, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }
        var cts = new CancellationTokenSource();
        _debounceCts[key] = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, cts.Token).ConfigureAwait(false);
                await _settingsRepo.SetValueAsync(key, value).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { /* 新しい変更が来た */ }
            catch (Exception ex)
            {
                LogHelper.Warn(nameof(SettingsViewModel), $"DebounceSave failed: {ex.Message}");
                // Task.Run 経由のためバインディング更新は UI スレッドへ戻す
                MainThread.BeginInvokeOnMainThread(() =>
                    SetError($"設定の保存に失敗しました: {ex.Message}"));
            }
        });
    }

    partial void OnCacheMonthsChanged(int value)       => DebounceSave(SettingsKeys.CACHE_MONTHS, value.ToString());
    partial void OnUpdateIntervalHoursChanged(int value) => DebounceSave(SettingsKeys.UPDATE_INTERVAL_HOURS, value.ToString());
    partial void OnFontSizeSpChanged(int value)        => DebounceSave(SettingsKeys.FONT_SIZE_SP, value.ToString());
    partial void OnBackgroundThemeChanged(int value)   => DebounceSave(SettingsKeys.BACKGROUND_THEME, value.ToString());
    partial void OnLineSpacingChanged(int value)       => DebounceSave(SettingsKeys.LINE_SPACING, value.ToString());
    partial void OnEpisodesPerPageChanged(int value)   => DebounceSave(SettingsKeys.EPISODES_PER_PAGE, value.ToString());

    partial void OnRequestDelayMsChanged(int value) =>
        DebounceSave(SettingsKeys.REQUEST_DELAY_MS,
            Math.Clamp(value, SettingsKeys.MIN_REQUEST_DELAY_MS, SettingsKeys.MAX_REQUEST_DELAY_MS).ToString());

    partial void OnVerticalWritingChanged(bool value)  => DebounceSave(SettingsKeys.VERTICAL_WRITING, value ? "1" : "0");
    partial void OnPrefetchEnabledChanged(bool value)  => DebounceSave(SettingsKeys.PREFETCH_ENABLED, value ? "1" : "0");
    partial void OnAutoMarkReadEnabledChanged(bool value) => DebounceSave(SettingsKeys.AUTO_MARK_READ_ENABLED, value ? "1" : "0");

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "確認", "すべてのキャッシュを削除しますか？", "OK", "キャンセル");

        if (!confirm) return;

        ClearError();
        try
        {
            await _cacheRepo.DeleteAllAsync();
            // Snackbar-like notification
            await Shell.Current.DisplayAlert("完了", "クリアしました", "OK");
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(SettingsViewModel), $"ClearCacheAsync failed: {ex.Message}");
            SetError($"キャッシュの削除に失敗しました: {ex.Message}");
        }
    }
}
