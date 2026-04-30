using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class EpisodeListViewModel : ErrorAwareViewModel, IQueryAttributable
{
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly AppSettingsRepository _settingsRepo;
    private readonly PrefetchService _prefetch;

    private int _novelDbId;
    private List<Episode> _allEpisodes = new();
    private HashSet<int> _cachedIds = new();
    private List<Episode> _filteredCache = new();

    public EpisodeListViewModel(
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        EpisodeCacheRepository cacheRepo,
        AppSettingsRepository settingsRepo,
        PrefetchService prefetch)
    {
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _cacheRepo = cacheRepo;
        _settingsRepo = settingsRepo;
        _prefetch = prefetch;
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

    [ObservableProperty]
    private bool _isNovelFavorite;

    [ObservableProperty]
    private bool _showUnreadOnly;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    private int _episodesPerPage = 50;
    private Novel? _novel;
    private Task? _initTask;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("novelId", out var novelIdObj) && int.TryParse(novelIdObj?.ToString(), out var novelId))
        {
            _novelDbId = novelId;
            _initTask = InitializeAsync();
        }
    }

    public Task EnsureInitializedAsync() => _initTask ?? Task.CompletedTask;

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ClearError();
        try
        {
            _novel = await _novelRepo.GetByIdAsync(_novelDbId);
            if (_novel is null) return;

            _episodesPerPage = await _settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, 50);

            _allEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
            _cachedIds = await _cacheRepo.GetCachedEpisodeIdsAsync(_novelDbId);
            RebuildFilterCache();

            var hasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
            var lastRead = await _episodeRepo.GetLastReadEpisodeAsync(_novelDbId);

            NovelTitle = _novel.Title;
            HasChapters = hasChapters;
            HasLastRead = lastRead is not null;
            IsNovelFavorite = _novel.IsFavorite;

            RecalcPaging();
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(EpisodeListViewModel), $"InitializeAsync failed: {ex.Message}");
            SetError($"目次の読み込みに失敗しました: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildFilterCache()
    {
        IEnumerable<Episode> src = _allEpisodes;
        if (ShowUnreadOnly) src = src.Where(e => !e.IsRead);
        if (ShowFavoritesOnly) src = src.Where(e => e.IsFavorite);
        _filteredCache = src.ToList();
    }

    private void RecalcPaging()
    {
        var totalCount = _filteredCache.Count;
        MaxPage = Math.Max(1, (int)Math.Ceiling((double)totalCount / _episodesPerPage));
        if (CurrentPage > MaxPage) CurrentPage = MaxPage;
    }

    private Task LoadPageAsync()
    {
        var list = _filteredCache
            .Skip((CurrentPage - 1) * _episodesPerPage)
            .Take(_episodesPerPage)
            .Select(e => EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id)))
            .ToList();
        Episodes = new ObservableCollection<EpisodeViewModel>(list);
        return Task.CompletedTask;
    }

    public async Task RefreshReadStatusAsync()
    {
        if (_allEpisodes.Count == 0) return;

        var freshEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
        var readMap = freshEpisodes.ToDictionary(e => e.Id, e => e.IsRead);

        foreach (var ep in _allEpisodes)
        {
            if (readMap.TryGetValue(ep.Id, out var isRead))
                ep.IsRead = isRead;
        }

        if (ShowUnreadOnly)
        {
            // 未読フィルタ ON のときは既読化した話をリストから外す必要がある
            RebuildFilterCache();
            RecalcPaging();
            await LoadPageAsync();
        }
        else
        {
            // フィルタが OFF なら現在表示中のアイテムだけ in-place 更新
            foreach (var vm in Episodes)
            {
                if (readMap.TryGetValue(vm.Id, out var isRead))
                    vm.IsRead = isRead;
            }
        }
    }

    partial void OnShowUnreadOnlyChanged(bool value) => _ = ReloadListAsync();
    partial void OnShowFavoritesOnlyChanged(bool value) => _ = ReloadListAsync();

    private async Task ReloadListAsync()
    {
        RebuildFilterCache();
        CurrentPage = 1;
        RecalcPaging();
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task ReadContinueAsync()
    {
        var firstUnread = await _episodeRepo.GetFirstUnreadEpisodeAsync(_novelDbId);
        var lastRead = await _episodeRepo.GetLastReadEpisodeAsync(_novelDbId);

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

    [RelayCommand]
    private async Task ToggleEpisodeFavoriteAsync(EpisodeViewModel ep)
    {
        var newValue = !ep.IsFavorite;
        await _episodeRepo.SetFavoriteAsync(ep.Id, newValue);
        ep.IsFavorite = newValue;

        var source = _allEpisodes.FirstOrDefault(e => e.Id == ep.Id);
        if (source is not null) source.IsFavorite = newValue;

        if (ShowFavoritesOnly)
        {
            RebuildFilterCache();
            RecalcPaging();
            await LoadPageAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleNovelFavoriteAsync()
    {
        if (_novel is null) return;
        var newValue = !IsNovelFavorite;
        await _novelRepo.SetFavoriteAsync(_novel.Id, newValue);
        IsNovelFavorite = newValue;
        _novel.IsFavorite = newValue;
    }

    [RelayCommand]
    private async Task DownloadAllAsync()
    {
        if (_novel is null) return;
        var enqueued = await _prefetch.EnqueueNovelAsync(_novelDbId, highPriority: true);
        await Shell.Current.DisplayAlert("一括ダウンロード",
            enqueued > 0
                ? $"{enqueued}話をバックグラウンドで取得します（Wi-Fi接続時のみ）"
                : "新規取得する話はありません",
            "OK");
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
