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

    private static async Task<(List<SearchResult> hits, string? error)> RunSiteSearchAsync(
        Func<Task<List<SearchResult>>> search, string siteName)
    {
        try
        {
            return (await search(), null);
        }
        catch (TaskCanceledException)
        {
            return ([], $"{siteName}の検索がタイムアウトしました");
        }
        catch (HttpRequestException ex)
        {
            return ([], $"{siteName}の通信エラー: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ([], $"{siteName}のエラー: {ex.Message}");
        }
    }

    private async Task ExecuteSiteQueryAsync(
        string operationName,
        Func<CancellationToken, Task<List<SearchResult>>>? narouFetch,
        Func<CancellationToken, Task<List<SearchResult>>>? kakuyomuFetch)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = cts.Token;

            var narouTask = narouFetch is not null
                ? RunSiteSearchAsync(() => narouFetch(ct), "なろう")
                : Task.FromResult<(List<SearchResult> hits, string? error)>(([], null));
            var kakuyomuTask = kakuyomuFetch is not null
                ? RunSiteSearchAsync(() => kakuyomuFetch(ct), "カクヨム")
                : Task.FromResult<(List<SearchResult> hits, string? error)>(([], null));

            var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

            var allHits = siteResults.SelectMany(r => r.hits).ToList();
            var errors = siteResults.Select(r => r.error).Where(e => e is not null).ToList();
            if (errors.Count > 0)
            {
                HasError = true;
                ErrorMessage = string.Join("\n", errors);
            }

            await ShowResultsAsync(allHits);
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(SearchViewModel), $"{operationName} failed: {ex.Message}");
            HasError = true;
            ErrorMessage = "通信エラーが発生しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private Task SearchAsync()
    {
        var searchTarget = "Both";
        return ExecuteSiteQueryAsync(
            "Search",
            SearchNarou ? ct => _narou.SearchAsync(SearchKeyword, searchTarget, ct) : null,
            SearchKakuyomu ? ct => _kakuyomu.SearchAsync(SearchKeyword, searchTarget, ct) : null);
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchKeyword) && !IsLoading;

    [RelayCommand]
    private Task FetchRankingAsync()
    {
        var period = (RankingPeriod)Math.Clamp(RankingPeriodIndex, 0, 3);
        return ExecuteSiteQueryAsync(
            "Ranking fetch",
            SearchNarou
                ? ct =>
                {
                    int? bg = null;
                    if (SelectedNarouBigGenre is not null
                        && int.TryParse(SelectedNarouBigGenre.Id, out var bgv)) bg = bgv;
                    return _narou.FetchRankingAsync(period, bg, 30, ct);
                }
                : null,
            SearchKakuyomu
                ? ct =>
                {
                    var periodSlug = period switch
                    {
                        RankingPeriod.Daily => "daily",
                        RankingPeriod.Weekly => "weekly",
                        RankingPeriod.Monthly => "monthly",
                        _ => "weekly",
                    };
                    return _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre?.Id ?? "all", periodSlug, ct);
                }
                : null);
    }

    [RelayCommand]
    private Task FetchGenreAsync()
    {
        int? narouBg = (SearchNarou
            && SelectedNarouBigGenre is not null
            && int.TryParse(SelectedNarouBigGenre.Id, out var bg)) ? bg : null;

        return ExecuteSiteQueryAsync(
            "Genre fetch",
            narouBg is int bgv ? ct => _narou.FetchByGenreAsync(bgv, "weeklypoint", 30, ct) : null,
            (SearchKakuyomu && SelectedKakuyomuGenre is not null)
                ? ct => _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre.Id, "weekly", ct)
                : null);
    }

    private async Task ShowResultsAsync(List<SearchResult> results)
    {
        var existingIds = await _novelRepo.GetExistingSiteNovelIdsAsync();
        var viewModels = results.Select(r =>
            SearchResultViewModel.FromModel(r, existingIds.Contains(((int)r.SiteType, r.NovelId)))
        ).ToList();
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var novel = new Novel
            {
                SiteType = (int)result.SiteType,
                NovelId = result.NovelId,
                Title = result.Title,
                Author = result.Author,
                TotalEpisodes = result.TotalEpisodes,
                IsCompleted = result.IsCompleted,
                RegisteredAt = DateTime.UtcNow.ToString("o"),
                LastUpdatedAt = DateTime.UtcNow.ToString("o"),
            };

            await _novelRepo.InsertAsync(novel);

            var service = _serviceFactory.GetService(result.SiteType);
            var episodes = await service.FetchEpisodeListAsync(result.NovelId, cts.Token);

            var dbNovel = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
            if (dbNovel is not null)
            {
                foreach (var ep in episodes)
                {
                    ep.NovelId = dbNovel.Id;
                }
                await _episodeRepo.InsertAllAsync(episodes);

                dbNovel.TotalEpisodes = episodes.Count;

                // 作者名が空の場合、FetchNovelInfoAsync で補完
                if (string.IsNullOrEmpty(dbNovel.Author))
                {
                    try
                    {
                        var (_, _, _, fetchedAuthor) = await service.FetchNovelInfoAsync(result.NovelId, cts.Token);
                        if (!string.IsNullOrEmpty(fetchedAuthor))
                        {
                            dbNovel.Author = fetchedAuthor;
                        }
                    }
                    catch { /* 作者名取得失敗は無視 */ }
                }

                await _novelRepo.UpdateAsync(dbNovel);

                // Auto-enqueue prefetch for newly registered novel (Wi-Fi only)
                _ = _prefetch.EnqueueNovelAsync(dbNovel.Id);
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
