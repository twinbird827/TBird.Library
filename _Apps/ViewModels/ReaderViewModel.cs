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
    private int _backgroundThemeIndex;

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
    private double _lineHeight = 1.7;

    [ObservableProperty]
    private Color _backgroundColor = Color.FromArgb("#FFFFFF");

    [ObservableProperty]
    private Color _textColor = Color.FromArgb("#212121");

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

    private Episode? _episode;

    partial void OnIsVerticalWritingChanged(bool value)
    {
        IsHorizontal = !value;
        if (value && !string.IsNullOrEmpty(EpisodeContent))
        {
            RefreshHtml();
        }
    }

    partial void OnFontSizeChanged(double value) => UpdateCssStateIfReady();
    partial void OnLineHeightChanged(double value) => UpdateCssStateIfReady();
    partial void OnBackgroundColorChanged(Color value) => UpdateCssStateIfReady();
    partial void OnTextColorChanged(Color value) => UpdateCssStateIfReady();

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
        var fontSizeSp = await _settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, 16);
        _backgroundThemeIndex = await _settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, 0);
        var lineSpacing = await _settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, 1);
        var vertical = await _settingsRepo.GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, 0);

        var (bg, text) = ThemeHelper.GetThemeColors(_backgroundThemeIndex);
        var lh = ThemeHelper.GetLineHeight(lineSpacing);

        FontSize = fontSizeSp;
        BackgroundColor = bg;
        TextColor = text;
        LineHeight = lh;
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
        LineHeight: LineHeight,
        BackgroundHex: ColorToHex(BackgroundColor),
        ForegroundHex: ColorToHex(TextColor));

    private static string ColorToHex(Color c) =>
        $"#{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}";

    private async Task LoadEpisodeAsync(int episodeId)
    {
        IsLoading = true;
        try
        {
            _episode = await _episodeRepo.GetByIdAsync(episodeId);
            if (_episode is null) return;

            var prev = await _episodeRepo.GetByNovelAndEpisodeNoAsync(_novelDbId, _episode.EpisodeNo - 1);
            var next = await _episodeRepo.GetByNovelAndEpisodeNoAsync(_novelDbId, _episode.EpisodeNo + 1);

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
            IsCurrentEpisodeFavorite = _episode.IsFavorite == 1;
            HasPrevEpisode = prev is not null;
            HasNextEpisode = next is not null;
            IsHeaderVisible = true;
            IsFooterVisible = true;

            if (IsVerticalWriting) RefreshHtml();
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
        var prev = await _episodeRepo.GetByNovelAndEpisodeNoAsync(_novelDbId, _episode.EpisodeNo - 1);
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
        var next = await _episodeRepo.GetByNovelAndEpisodeNoAsync(_novelDbId, _episode.EpisodeNo + 1);
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
        _episode.IsFavorite = newValue ? 1 : 0;
        IsCurrentEpisodeFavorite = newValue;
    }

    [RelayCommand]
    private async Task MarkAsReadAsync()
    {
        if (_episode is null || _episode.IsRead == 1) return;

        await _episodeRepo.MarkAsReadAsync(_episode.Id);
        _episode.IsRead = 1;

        var allRead = await _episodeRepo.AreAllReadAsync(_novelDbId);
        if (allRead)
        {
            var novel = await _novelRepo.GetByIdAsync(_novelDbId);
            if (novel is not null && novel.HasUnconfirmedUpdate == 1)
            {
                novel.HasUnconfirmedUpdate = 0;
                await _novelRepo.UpdateAsync(novel);
            }
        }
    }
}
