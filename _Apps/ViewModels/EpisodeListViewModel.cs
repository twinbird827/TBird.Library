using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class EpisodeListViewModel : ObservableObject, IQueryAttributable
{
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly AppSettingsRepository _settingsRepo;

    private int _novelDbId;

    public EpisodeListViewModel(
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        AppSettingsRepository settingsRepo)
    {
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _settingsRepo = settingsRepo;
    }

    [ObservableProperty]
    private string _novelTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<EpisodeViewModel> _episodes = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _maxPage;

    [ObservableProperty]
    private bool _hasChapters;

    [ObservableProperty]
    private bool _hasLastRead;

    [ObservableProperty]
    private bool _isLoading;

    private int _episodesPerPage = 50;
    private Novel? _novel;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("novelId", out var novelIdObj) && int.TryParse(novelIdObj?.ToString(), out var novelId))
        {
            _novelDbId = novelId;
            _ = InitializeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            _novel = await _novelRepo.GetByIdAsync(_novelDbId).ConfigureAwait(false);
            if (_novel is null) return;

            _episodesPerPage = await _settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, 50).ConfigureAwait(false);

            // Check if episodes have chapters
            var allEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId).ConfigureAwait(false);
            var hasChapters = allEpisodes.Any(e => e.ChapterName is not null);

            // Check last read
            var lastRead = await _episodeRepo.GetLastReadEpisodeAsync(_novelDbId).ConfigureAwait(false);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                NovelTitle = _novel.Title;
                HasChapters = hasChapters;
                HasLastRead = lastRead is not null;
            });

            if (hasChapters)
            {
                // Show all episodes grouped by chapter (no paging)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Episodes = new ObservableCollection<EpisodeViewModel>(
                        allEpisodes.Select(EpisodeViewModel.FromModel));
                    MaxPage = 1;
                });
            }
            else
            {
                var totalCount = allEpisodes.Count;
                var maxPage = Math.Max(1, (int)Math.Ceiling((double)totalCount / _episodesPerPage));

                MainThread.BeginInvokeOnMainThread(() => MaxPage = maxPage);
                await LoadPageAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(EpisodeListViewModel), $"InitializeAsync failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPageAsync()
    {
        var episodes = await _episodeRepo.GetPagedByNovelIdAsync(_novelDbId, CurrentPage, _episodesPerPage).ConfigureAwait(false);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Episodes = new ObservableCollection<EpisodeViewModel>(
                episodes.Select(EpisodeViewModel.FromModel));
        });
    }

    [RelayCommand]
    private async Task ReadContinueAsync()
    {
        var firstUnread = await _episodeRepo.GetFirstUnreadEpisodeAsync(_novelDbId).ConfigureAwait(false);
        var lastRead = await _episodeRepo.GetLastReadEpisodeAsync(_novelDbId).ConfigureAwait(false);

        var target = firstUnread ?? lastRead;
        if (target is not null && _novel is not null)
        {
            await Shell.Current.GoToAsync(
                $"reader?novelId={_novelDbId}&episodeId={target.Id}&siteType={_novel.SiteType}&siteNovelId={_novel.NovelId}");
        }
    }

    [RelayCommand]
    private async Task NavigateToEpisode(EpisodeViewModel episode)
    {
        if (_novel is null) return;
        await Shell.Current.GoToAsync(
            $"reader?novelId={_novelDbId}&episodeId={episode.Id}&siteType={_novel.SiteType}&siteNovelId={_novel.NovelId}");
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private async Task PrevPageAsync()
    {
        CurrentPage--;
        await LoadPageAsync();
    }

    private bool CanGoPrev() => CurrentPage > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPageAsync()
    {
        CurrentPage++;
        await LoadPageAsync();
    }

    private bool CanGoNext() => CurrentPage < MaxPage;
}
