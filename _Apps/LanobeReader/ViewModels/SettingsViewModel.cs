using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using TBird.Core;
using TBird.Maui.ViewModels;
using LanobeReader.Services;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class SettingsViewModel : ErrorAwareViewModel
{
    private readonly AppSettingsRepository _settingsRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly IUpdateScheduler _scheduler;
    private bool _isInitializing;

    public SettingsViewModel(AppSettingsRepository settingsRepo, EpisodeCacheRepository cacheRepo, IUpdateScheduler scheduler)
    {
        _settingsRepo = settingsRepo;
        _cacheRepo = cacheRepo;
        _scheduler = scheduler;
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
    private readonly object _debounceLock = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// キーごとに最新の操作のみを <see cref="DebounceDelay"/> 後に実行する共通デバウンス。
    /// 各 CancellationTokenSource は「待機中トークンと競合する Cancel 直後の Dispose」を避けるため、
    /// 自身の継続(finally)でのみ破棄する。辞書アクセスは _debounceLock で直列化(スレッド安全)。
    /// </summary>
    /// <param name="userErrorMessage">非 null のとき、失敗時に UI へエラー表示する。</param>
    private void Debounce(string key, Func<Task> action, string? userErrorMessage = null)
    {
        if (_isInitializing) return;

        CancellationTokenSource cts;
        lock (_debounceLock)
        {
            if (_debounceCts.TryGetValue(key, out var oldCts))
            {
                oldCts.Cancel();
            }
            cts = new CancellationTokenSource();
            _debounceCts[key] = cts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, cts.Token).ConfigureAwait(false);
                await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* 新しい変更が来た */ }
            catch (Exception ex)
            {
                MessageService.Warn($"Debounce[{key}] failed: {ex.Message}");
                if (userErrorMessage is not null)
                {
                    // Task.Run 経由のためバインディング更新は UI スレッドへ戻す
                    MainThread.BeginInvokeOnMainThread(() =>
                        SetError($"{userErrorMessage}: {ex.Message}"));
                }
            }
            finally
            {
                lock (_debounceLock)
                {
                    // 自分がまだ最新エントリなら辞書から外す(以後この破棄済み CTS への Cancel を防ぐ)。
                    // 既に置き換え済みなら最新を残す。どちらでも自分自身の破棄は安全。
                    if (_debounceCts.TryGetValue(key, out var cur) && ReferenceEquals(cur, cts))
                    {
                        _debounceCts.Remove(key);
                    }
                }
                cts.Dispose();
            }
        });
    }

    private void DebounceSave(string key, string value)
        => Debounce(key, () => _settingsRepo.SetValueAsync(key, value), "設定の保存に失敗しました");

    partial void OnCacheMonthsChanged(int value)       => DebounceSave(SettingsKeys.CACHE_MONTHS, value.ToString());

    partial void OnUpdateIntervalHoursChanged(int value)
    {
        DebounceSave(SettingsKeys.UPDATE_INTERVAL_HOURS, value.ToString());
        // 間隔変更を即時に WorkManager / アラームへ反映する。従来は次回アプリ起動時の差分チェック
        // (MainActivity) まで反映されず「設定を変えても効かない」状態だった。
        Debounce("__reschedule_interval", async () =>
        {
            _scheduler.Schedule(value);
            // MainActivity の差分チェックと整合させ、次回起動時の二重スケジュールを防ぐ。
            await _settingsRepo.SetValueAsync(SettingsKeys.LAST_SCHEDULED_HOURS, value.ToString()).ConfigureAwait(false);
            MessageService.Info($"Rescheduled update check to {value}h from settings");
        }, "更新間隔の反映に失敗しました");
    }

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
            MessageService.Error($"ClearCacheAsync failed: {ex.Message}");
            SetError($"キャッシュの削除に失敗しました: {ex.Message}");
        }
    }
}
