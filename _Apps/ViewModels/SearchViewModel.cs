using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using LanobeReader.Services.Kakuyomu;
using LanobeReader.Services.Narou;

namespace LanobeReader.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly INovelServiceFactory _serviceFactory;
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly NarouApiService _narou;
    private readonly KakuyomuApiService _kakuyomu;
    private readonly PrefetchService _prefetch;

    public SearchViewModel(
        INovelServiceFactory serviceFactory,
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        NarouApiService narou,
        KakuyomuApiService kakuyomu,
        PrefetchService prefetch)
    {
        _serviceFactory = serviceFactory;
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _narou = narou;
        _kakuyomu = kakuyomu;
        _prefetch = prefetch;

        NarouBigGenres = new ObservableCollection<GenreInfo>(NarouGenres.BigGenres);
        KakuyomuGenreList = new ObservableCollection<GenreInfo>(KakuyomuGenres.Genres);
        KakuyomuPeriodList = new ObservableCollection<GenreInfo>(KakuyomuGenres.Periods);

        SelectedNarouBigGenre = NarouBigGenres.First();
        SelectedKakuyomuGenre = KakuyomuGenreList.First();
        SelectedKakuyomuPeriod = KakuyomuPeriodList.First();
    }

    // Mode: 0=Keyword, 1=Ranking, 2=Genre browse
    [ObservableProperty]
    private int _mode;

    public bool IsKeywordMode => Mode == 0;
    public bool IsRankingMode => Mode == 1;
    public bool IsGenreMode => Mode == 2;

    partial void OnModeChanged(int value)
    {
        OnPropertyChanged(nameof(IsKeywordMode));
        OnPropertyChanged(nameof(IsRankingMode));
        OnPropertyChanged(nameof(IsGenreMode));
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

    // Ranking/Genre browse
    public ObservableCollection<GenreInfo> NarouBigGenres { get; }
    public ObservableCollection<GenreInfo> KakuyomuGenreList { get; }
    public ObservableCollection<GenreInfo> KakuyomuPeriodList { get; }

    [ObservableProperty]
    private GenreInfo? _selectedNarouBigGenre;

    [ObservableProperty]
    private GenreInfo? _selectedKakuyomuGenre;

    [ObservableProperty]
    private GenreInfo? _selectedKakuyomuPeriod;

    [ObservableProperty]
    private int _rankingPeriodIndex; // 0=Daily 1=Weekly 2=Monthly 3=Quarterly

    [RelayCommand]
    private void SetModeKeyword() => Mode = 0;

    [RelayCommand]
    private void SetModeRanking() => Mode = 1;

    [RelayCommand]
    private void SetModeGenre() => Mode = 2;

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
                    var narouResults = await _narou.SearchAsync(SearchKeyword, searchTarget);
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
                    var kakuyomuResults = await _kakuyomu.SearchAsync(SearchKeyword, searchTarget);
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

            await ShowResultsAsync(results);
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
    private async Task FetchRankingAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            var results = new List<SearchResult>();
            var period = (RankingPeriod)Math.Clamp(RankingPeriodIndex, 0, 3);

            if (SearchNarou)
            {
                try
                {
                    int? bg = null;
                    if (SelectedNarouBigGenre is not null && int.TryParse(SelectedNarouBigGenre.Id, out var bgv)) bg = bgv;
                    var narouList = await _narou.FetchRankingAsync(period, bg, 30);
                    results.AddRange(narouList);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn(nameof(SearchViewModel), $"Narou ranking failed: {ex.Message}");
                }
            }

            if (SearchKakuyomu)
            {
                try
                {
                    var periodSlug = period switch
                    {
                        RankingPeriod.Daily => "daily",
                        RankingPeriod.Weekly => "weekly",
                        RankingPeriod.Monthly => "monthly",
                        _ => "weekly",
                    };
                    var kakuyomuList = await _kakuyomu.FetchRankingAsync(
                        SelectedKakuyomuGenre?.Id ?? "all", periodSlug);
                    results.AddRange(kakuyomuList);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn(nameof(SearchViewModel), $"Kakuyomu ranking failed: {ex.Message}");
                }
            }

            await ShowResultsAsync(results);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task FetchGenreAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            var results = new List<SearchResult>();

            if (SearchNarou && SelectedNarouBigGenre is not null && int.TryParse(SelectedNarouBigGenre.Id, out var bg))
            {
                try
                {
                    var narouList = await _narou.FetchByGenreAsync(bg, "weeklypoint", 30);
                    results.AddRange(narouList);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn(nameof(SearchViewModel), $"Narou genre failed: {ex.Message}");
                }
            }

            if (SearchKakuyomu && SelectedKakuyomuGenre is not null)
            {
                try
                {
                    var kakuyomuList = await _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre.Id, "weekly");
                    results.AddRange(kakuyomuList);
                }
                catch (Exception ex)
                {
                    LogHelper.Warn(nameof(SearchViewModel), $"Kakuyomu genre failed: {ex.Message}");
                }
            }

            await ShowResultsAsync(results);
        }
        finally { IsLoading = false; }
    }

    private async Task ShowResultsAsync(List<SearchResult> results)
    {
        var viewModels = new List<SearchResultViewModel>();
        foreach (var result in results)
        {
            var existing = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
            viewModels.Add(SearchResultViewModel.FromModel(result, existing is not null));
        }
        SearchResults = new ObservableCollection<SearchResultViewModel>(viewModels);
        HasSearched = true;
    }

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

            await _novelRepo.InsertAsync(novel);

            var service = _serviceFactory.GetService(result.SiteType);
            var episodes = await service.FetchEpisodeListAsync(result.NovelId);

            var dbNovel = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
            if (dbNovel is not null)
            {
                foreach (var ep in episodes)
                {
                    ep.NovelId = dbNovel.Id;
                }
                await _episodeRepo.InsertAllAsync(episodes);

                dbNovel.TotalEpisodes = episodes.Count;
                await _novelRepo.UpdateAsync(dbNovel);

                // Auto-enqueue prefetch for newly registered novel (Wi-Fi only)
                _ = Task.Run(() => _prefetch.EnqueueNovelAsync(dbNovel.Id));
            }

            result.IsRegistered = true;
            result.TotalEpisodes = episodes.Count;
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
