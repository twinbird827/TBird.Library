using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly INovelServiceFactory _serviceFactory;
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;

    public SearchViewModel(
        INovelServiceFactory serviceFactory,
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo)
    {
        _serviceFactory = serviceFactory;
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _searchNarou = true;

    [ObservableProperty]
    private bool _searchKakuyomu = true;

    [ObservableProperty]
    private ObservableCollection<SearchResultViewModel> _searchResults = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var results = new List<SearchResult>();
            var searchTarget = "Both";

            if (SearchNarou)
            {
                try
                {
                    var narouService = _serviceFactory.GetService(SiteType.Narou);
                    var narouResults = await narouService.SearchAsync(SearchKeyword, searchTarget).ConfigureAwait(false);
                    results.AddRange(narouResults);
                }
                catch (TaskCanceledException)
                {
                    HasError = true;
                    ErrorMessage = "なろうの検索がタイムアウトしました";
                }
                catch (HttpRequestException)
                {
                    HasError = true;
                    ErrorMessage = "通信エラーが発生しました";
                }
            }

            if (SearchKakuyomu)
            {
                try
                {
                    var kakuyomuService = _serviceFactory.GetService(SiteType.Kakuyomu);
                    var kakuyomuResults = await kakuyomuService.SearchAsync(SearchKeyword, searchTarget).ConfigureAwait(false);
                    results.AddRange(kakuyomuResults);
                }
                catch (TaskCanceledException)
                {
                    if (!HasError) { HasError = true; ErrorMessage = "カクヨムの検索がタイムアウトしました"; }
                }
                catch (HttpRequestException)
                {
                    if (!HasError) { HasError = true; ErrorMessage = "通信エラーが発生しました"; }
                }
            }

            // Check registration status
            var viewModels = new List<SearchResultViewModel>();
            foreach (var result in results)
            {
                var existing = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId).ConfigureAwait(false);
                viewModels.Add(SearchResultViewModel.FromModel(result, existing is not null));
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchResults = new ObservableCollection<SearchResultViewModel>(viewModels);
                HasSearched = true;
            });
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(SearchViewModel), $"Search failed: {ex.Message}");
            HasError = true;
            ErrorMessage = "通信エラーが発生しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchKeyword) && !IsLoading;

    [RelayCommand]
    private async Task RegisterAsync(SearchResultViewModel result)
    {
        if (result.IsRegistered || result.IsRegistering) return;

        result.IsRegistering = true;
        try
        {
            var novel = new Novel
            {
                SiteType = (int)result.SiteType,
                NovelId = result.NovelId,
                Title = result.Title,
                Author = result.Author,
                TotalEpisodes = result.TotalEpisodes,
                IsCompleted = result.IsCompleted ? 1 : 0,
                RegisteredAt = DateTime.UtcNow.ToString("o"),
                LastUpdatedAt = DateTime.UtcNow.ToString("o"),
            };

            await _novelRepo.InsertAsync(novel).ConfigureAwait(false);

            // Fetch episode list
            var service = _serviceFactory.GetService(result.SiteType);
            var episodes = await service.FetchEpisodeListAsync(result.NovelId).ConfigureAwait(false);

            // Set novel_id for each episode
            var dbNovel = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId).ConfigureAwait(false);
            if (dbNovel is not null)
            {
                foreach (var ep in episodes)
                {
                    ep.NovelId = dbNovel.Id;
                }
                await _episodeRepo.InsertAllAsync(episodes).ConfigureAwait(false);

                // Update total episodes
                dbNovel.TotalEpisodes = episodes.Count;
                await _novelRepo.UpdateAsync(dbNovel).ConfigureAwait(false);
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                result.IsRegistered = true;
                result.TotalEpisodes = episodes.Count;
            });
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(SearchViewModel), $"Register failed: {ex.Message}");
            await Shell.Current.DisplayAlert("エラー", $"登録に失敗しました: {ex.Message}", "OK");
        }
        finally
        {
            result.IsRegistering = false;
        }
    }
}
