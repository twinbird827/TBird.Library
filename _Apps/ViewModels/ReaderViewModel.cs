using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class ReaderViewModel : ObservableObject, IQueryAttributable
{
    private readonly EpisodeRepository _episodeRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly NovelRepository _novelRepo;
    private readonly INovelServiceFactory _serviceFactory;
    private readonly AppSettingsRepository _settingsRepo;

    private int _novelDbId;
    private int _currentEpisodeId;
    private int _siteType;
    private string _siteNovelId = string.Empty;
    public ReaderViewModel(
        EpisodeRepository episodeRepo,
        EpisodeCacheRepository cacheRepo,
        NovelRepository novelRepo,
        INovelServiceFactory serviceFactory,
        AppSettingsRepository settingsRepo)
    {
        _episodeRepo = episodeRepo;
        _cacheRepo = cacheRepo;
        _novelRepo = novelRepo;
        _serviceFactory = serviceFactory;
        _settingsRepo = settingsRepo;
    }

    [ObservableProperty]
    private string _episodeTitle = string.Empty;

    [ObservableProperty]
    private string _episodeContent = string.Empty;

    [ObservableProperty]
    private string _episodeHtml = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isHeaderVisible = true;

    [ObservableProperty]
    private bool _isFooterVisible = true;

    [ObservableProperty]
    private double _fontSize = 16;

    [ObservableProperty]
    private int _backgroundThemeIndex;

    [ObservableProperty]
    private int _lineSpacingIndex = SettingsKeys.DEFAULT_LINE_SPACING;

    [ObservableProperty]
    private bool _isVerticalWriting;

    [ObservableProperty]
    private bool _isHorizontal = true;

    [ObservableProperty]
    private ReaderCssState? _readerCss;

    [ObservableProperty]
    private bool _isCurrentEpisodeFavorite;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevEpisodeCommand))]
    private bool _hasPrevEpisode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextEpisodeCommand))]
    private bool _hasNextEpisode;

    [ObservableProperty]
    private bool _autoMarkReadEnabled = true;

    public bool IsManualReadButtonOverlayVisible
        => !AutoMarkReadEnabled && !IsFooterVisible;

    partial void OnAutoMarkReadEnabledChanged(bool value)
        => OnPropertyChanged(nameof(IsManualReadButtonOverlayVisible));

    partial void OnIsFooterVisibleChanged(bool value)
        => OnPropertyChanged(nameof(IsManualReadButtonOverlayVisible));

    private Episode? _episode;

    public Action? ScrollToTop { get; set; }

    partial void OnIsVerticalWritingChanged(bool value)
    {
        IsHorizontal = !value;
        if (value && !string.IsNullOrEmpty(EpisodeContent))
        {
            RefreshHtml();
        }
    }

    partial void OnFontSizeChanged(double value) => UpdateCssStateIfReady();
    partial void OnBackgroundThemeIndexChanged(int value) => UpdateCssStateIfReady();
    partial void OnLineSpacingIndexChanged(int value) => UpdateCssStateIfReady();

    private void UpdateCssStateIfReady()
    {
        if (ReaderCss is not null)
        {
            ReaderCss = BuildCssState();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("novelId", out var nid)) int.TryParse(nid?.ToString(), out _novelDbId);
        if (query.TryGetValue("episodeId", out var eid)) int.TryParse(eid?.ToString(), out _currentEpisodeId);
        if (query.TryGetValue("siteType", out var st)) int.TryParse(st?.ToString(), out _siteType);
        if (query.TryGetValue("siteNovelId", out var snid)) _siteNovelId = snid?.ToString() ?? "";

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadEpisodeAsync(_currentEpisodeId);
    }

    private async Task LoadSettingsAsync()
    {
        FontSize = await _settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, SettingsKeys.DEFAULT_FONT_SIZE_SP);
        BackgroundThemeIndex = await _settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, SettingsKeys.DEFAULT_BACKGROUND_THEME);
        LineSpacingIndex = await _settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, SettingsKeys.DEFAULT_LINE_SPACING);
        var vertical = await _settingsRepo.GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, SettingsKeys.DEFAULT_VERTICAL_WRITING);
        AutoMarkReadEnabled = await _settingsRepo.GetIntValueAsync(
            SettingsKeys.AUTO_MARK_READ_ENABLED,
            SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED) == 1;

        IsVerticalWriting = vertical == 1;
        IsHorizontal = !IsVerticalWriting;
        ReaderCss = BuildCssState();
    }

    public Task ReloadSettingsAsync() => LoadSettingsAsync();

    private void RefreshHtml()
    {
        var state = BuildCssState();
        // EpisodeHtml 先・ReaderCss 後: 古い document への無駄な JS 適用を防ぐ
        EpisodeHtml = ReaderHtmlBuilder.Build(EpisodeContent, state);
        ReaderCss = state;
    }

    private ReaderCssState BuildCssState() => new(
        FontSizePx: FontSize,
        LineSpacingIndex: LineSpacingIndex,
        BackgroundThemeIndex: BackgroundThemeIndex);

    private async Task LoadEpisodeAsync(int episodeId)
    {
        IsLoading = true;
        try
        {
            _episode = await _episodeRepo.GetByIdAsync(episodeId);
            if (_episode is null) return;

            var prev = await _episodeRepo.GetPreviousEpisodeAsync(_novelDbId, _episode.EpisodeNo);
            var next = await _episodeRepo.GetNextEpisodeAsync(_novelDbId, _episode.EpisodeNo);

            string content;
            var cache = await _cacheRepo.GetByEpisodeIdAsync(episodeId);
            if (cache is not null)
            {
                content = cache.Content;
            }
            else
            {
                var connectivity = Connectivity.Current.NetworkAccess;
                if (connectivity != NetworkAccess.Internet)
                {
                    await Shell.Current.DisplayAlert("エラー", "オフラインのため表示できません。キャッシュがありません", "OK");
                    return;
                }

                var service = _serviceFactory.GetService((SiteType)_siteType);
                content = await service.FetchEpisodeContentAsync(_siteNovelId, _episode.EpisodeNo);

                await _cacheRepo.InsertAsync(new EpisodeCache
                {
                    EpisodeId = episodeId,
                    Content = content,
                    CachedAt = DateTime.UtcNow.ToString("o"),
                });
            }

            EpisodeTitle = _episode.Title;
            EpisodeContent = content;
            IsCurrentEpisodeFavorite = _episode.IsFavorite;
            HasPrevEpisode = prev is not null;
            HasNextEpisode = next is not null;
            IsHeaderVisible = true;
            IsFooterVisible = true;

            if (IsVerticalWriting) RefreshHtml();

            ScrollToTop?.Invoke();
        }
        catch (TaskCanceledException)
        {
            await Shell.Current.DisplayAlert("エラー", "タイムアウトしました", "OK");
        }
        catch (HttpRequestException ex)
        {
            await Shell.Current.DisplayAlert("エラー", $"本文の取得に失敗しました（HTTPエラー: {ex.Message}）", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("エラー", $"本文の取得に失敗しました（{ex.Message}）", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private async Task PrevEpisodeAsync()
    {
        if (_episode is null) return;
        var prev = await _episodeRepo.GetPreviousEpisodeAsync(_novelDbId, _episode.EpisodeNo);
        if (prev is not null)
        {
            _currentEpisodeId = prev.Id;
            await LoadEpisodeAsync(prev.Id);
        }
    }

    private bool CanGoPrev() => HasPrevEpisode;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextEpisodeAsync()
    {
        if (_episode is null) return;
        var next = await _episodeRepo.GetNextEpisodeAsync(_novelDbId, _episode.EpisodeNo);
        if (next is not null)
        {
            _currentEpisodeId = next.Id;
            await LoadEpisodeAsync(next.Id);
        }
    }

    private bool CanGoNext() => HasNextEpisode;

    [RelayCommand]
    private async Task NavigateToTocAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void ToggleHeaderFooter()
    {
        IsHeaderVisible = !IsHeaderVisible;
        IsFooterVisible = !IsFooterVisible;
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (_episode is null) return;
        var newValue = !IsCurrentEpisodeFavorite;
        await _episodeRepo.SetFavoriteAsync(_episode.Id, newValue);
        _episode.IsFavorite = newValue;
        IsCurrentEpisodeFavorite = newValue;
    }

    [RelayCommand]
    private Task MarkAsReadAsync() => ApplyMarkAsReadAsync();

    [RelayCommand]
    private Task MarkAsReadFromAutoAsync()
    {
        // 自動経路 (OnScrolled / WebView read-end)。設定 OFF なら no-op。
        if (!AutoMarkReadEnabled) return Task.CompletedTask;
        return ApplyMarkAsReadAsync();
    }

    private async Task ApplyMarkAsReadAsync()
    {
        if (_episode is null) return;
        // N-2 仕様: 既読でも N+1 以降の未読化を走らせるため IsRead チェックは外す。
        await _episodeRepo.SetReadStateUpToAsync(_novelDbId, _episode.EpisodeNo);
        _episode.IsRead = true;

        var allRead = await _episodeRepo.AreAllReadAsync(_novelDbId);
        if (allRead)
        {
            var novel = await _novelRepo.GetByIdAsync(_novelDbId);
            if (novel is not null && novel.HasUnconfirmedUpdate)
            {
                novel.HasUnconfirmedUpdate = false;
                await _novelRepo.UpdateAsync(novel);
            }
        }
    }
}
