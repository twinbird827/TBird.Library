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

    // ItemsSource は ObservableCollection の同一インスタンスを保持し、ページ切替時は
    // Clear + Add で内容のみ差し替える。List 再代入だと CollectionView がアダプタを
    // 全リセットしてコンテナ再利用 (RecyclerView のプール) が効かず、ページ切替が体感重くなる。
    // 単発 Reset (notifyDataSetChanged 相当) は Android で逆に全アイテム invalidate が走り
    // 遅いことが多いため、incremental な Clear + Add の方が体感が速い。
    public ObservableCollection<EpisodeViewModel> Episodes { get; } = new();

    // 「ページが切り替わった」セマンティクスを View へ通知する。Episodes 更新と同一 UI tick で
    // 発火することで、View 側で ScrollTo を呼んでもユーザーには「先頭で表示されてからスクロール」
    // の途中過程が見えない (RecyclerView が同じレイアウトパスでアイテム配置とスクロール target を
    // 両方処理する)。RefreshReadStatusAsync 等のインプレース更新では発火させない。
    public event EventHandler<PageContentResetArgs>? PageContentReset;

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

    // InitializeAsync / JumpToAnchorAsync 完了直後の OnAppearing で RefreshReadStatusAsync を
    // スキップするためのフラグ。両者で IsRead は最新化済みのため、直後の Refresh は DB の二重
    // fetch を回避するためにスキップする。
    private bool _skipNextRefresh;

    // Reader への遷移経路が「続きから読む」だった場合、戻り遷移時にアンカー位置（直前まで読んだ話）
    // へ再ジャンプさせるマーカー。`NavigateToEpisode`（詳細タップ）では立てない → タップ経路は
    // ApplyQueryAttributes 再発火時に何もせず、CollectionView の scroll 位置を維持する。
    private bool _pendingScrollToAnchor;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_initTask is not null)
        {
            // 既に init 済み: MAUI Shell が Reader→目次の戻り遷移で ApplyQueryAttributes を
            // 再発火させているケース。`_pendingScrollToAnchor` の有無で出し分け:
            // - true (続きから読む経由)     → アンカー位置へ再ジャンプ
            // - false (詳細タップ経由)      → 何もしない (Episodes / CurrentPage 不変 = scroll 維持)
            if (_pendingScrollToAnchor)
            {
                _pendingScrollToAnchor = false;
                _ = JumpToAnchorAsync();
            }
            return;
        }

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

            // _allEpisodes は既にロード済みなので、HasChapters / HasLastRead は in-memory で導出。
            // 旧実装は GetLastReadEpisodeAsync を別 DB クエリで呼んでいたが、IsRead フラグが
            // 1 つでもあれば「続きから読む」ボタンを出すのに十分なので DB 経由は不要。
            var hasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
            var hasLastRead = _allEpisodes.Any(e => e.IsRead);

            NovelTitle = _novel.Title;
            HasChapters = hasChapters;
            HasLastRead = hasLastRead;
            IsNovelFavorite = _novel.IsFavorite;

            RecalcPaging();

            // 一覧 → 目次（初回）: アンカー位置（直前まで読んだ話）へジャンプする。
            // ApplyQueryAttributes 側で再走を抑止しているため、ここに到達するのは VM ライフタイムで 1 回のみ。
            var (anchorPage, anchorIdxInPage) = ComputeAnchor();
            if (anchorPage.HasValue) CurrentPage = anchorPage.Value;

            // Shell のスライドアニメ (~250ms) と 50 アイテム追加が UI スレッドで競合すると
            // 遷移が中途半端な位置で詰まって見えるため、アニメ完了を待ってからリスト構築する。
            // Dispatcher.Dispatch (1 tick) では不足、Task.Delay(200) が必要 (実機検証済)。
            await Task.Delay(200);
            await LoadPageAsync();

            // LoadPageAsync 完了と同じ UI tick でスクロール target を渡すことで、
            // ユーザーには「先頭→アンカー」の中間スクロールが見えないようにする。
            // anchor が無い場合は先頭リセット (ScrollIndex=0, ToCenter=false)。
            PageContentReset?.Invoke(this, new PageContentResetArgs(
                ScrollIndex: anchorIdxInPage ?? 0,
                ToCenter: anchorIdxInPage.HasValue));

            // _allEpisodes は最新なので次の OnAppearing.Refresh は不要 (Reader 復帰時のみ実 fetch)。
            _skipNextRefresh = true;
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

    // アンカー（直前まで読んだ話 = firstUnread の 1 つ前、無ければ最新既読話）の
    // フィルタ後ページ番号と当該ページ内インデックスを返す。
    // 個別話 IsRead を flip する経路が無い前提で firstUnread の 1 つ前 == MAX(EpisodeNo WHERE IsRead) と一致する。
    // 該当話がフィルタ（未読のみ等）で除外されている／そもそも既読話が無い場合は (null, null)。
    private (int? page, int? indexInPage) ComputeAnchor()
    {
        Episode? anchor = null;
        var firstUnread = _allEpisodes.FirstOrDefault(e => !e.IsRead);
        if (firstUnread is not null)
        {
            var idx = _allEpisodes.IndexOf(firstUnread);
            if (idx > 0) anchor = _allEpisodes[idx - 1];
        }
        else
        {
            anchor = _allEpisodes.LastOrDefault(e => e.IsRead);
        }

        if (anchor is null) return (null, null);

        var idxInFiltered = _filteredCache.FindIndex(e => e.Id == anchor.Id);
        if (idxInFiltered < 0) return (null, null);

        var page = (idxInFiltered / _episodesPerPage) + 1;
        var indexInPage = idxInFiltered % _episodesPerPage;
        return (page, indexInPage);
    }

    // 続きから読む → Reader → 目次 戻り遷移用: IsRead を最新化してからアンカー位置にジャンプする。
    // MAUI Shell が ApplyQueryAttributes を再発火させるタイミングで呼ばれる想定。
    private async Task JumpToAnchorAsync()
    {
        if (_allEpisodes.Count == 0) return;

        // Reader が話を既読化している可能性があるので IsRead を再 fetch して反映する。
        var freshEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
        var readMap = freshEpisodes.ToDictionary(e => e.Id, e => e.IsRead);
        foreach (var ep in _allEpisodes)
        {
            if (readMap.TryGetValue(ep.Id, out var isRead))
                ep.IsRead = isRead;
        }
        RebuildFilterCache();
        RecalcPaging();

        var (anchorPage, anchorIdxInPage) = ComputeAnchor();
        if (anchorPage.HasValue) CurrentPage = anchorPage.Value;

        await LoadPageAsync();

        PageContentReset?.Invoke(this, new PageContentResetArgs(
            ScrollIndex: anchorIdxInPage ?? 0,
            ToCenter: anchorIdxInPage.HasValue));

        // OnAppearing 経由の RefreshReadStatusAsync は同等処理を済ませたのでスキップ。
        _skipNextRefresh = true;
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
        Episodes.Clear();
        foreach (var e in _filteredCache.Skip((CurrentPage - 1) * _episodesPerPage).Take(_episodesPerPage))
        {
            Episodes.Add(EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id)));
        }
        return Task.CompletedTask;
    }

    public async Task RefreshReadStatusAsync()
    {
        if (_allEpisodes.Count == 0) return;

        // Init 直後の最初の OnAppearing では _allEpisodes が最新なのでスキップ。
        // フラグを倒して、次回 (Reader 復帰時等) の OnAppearing からは実 fetch する。
        if (_skipNextRefresh)
        {
            _skipNextRefresh = false;
            return;
        }

        // Reader→目次の戻り遷移ではここで Shell のスライドアニメが進行中。DB クエリ + IsRead 更新が
        // 重なるとアニメが詰まって見えるため、アニメ完了を待ってから fetch & update する。
        // Dispatcher.Dispatch (1 tick) では不足、Task.Delay(200) が必要 (実機検証済)。
        await Task.Delay(200);

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
        PageContentReset?.Invoke(this, new PageContentResetArgs(ScrollIndex: 0, ToCenter: false));
    }

    [RelayCommand]
    private async Task ReadContinueAsync()
    {
        var firstUnread = await _episodeRepo.GetFirstUnreadEpisodeAsync(_novelDbId);
        var lastRead = await _episodeRepo.GetLastReadEpisodeAsync(_novelDbId);

        var target = firstUnread ?? lastRead;
        if (target is not null && _novel is not null)
        {
            // Reader 復帰時にアンカー位置（直前まで読んだ話）へ再ジャンプさせるマーカー。
            // NavigateToEpisode（詳細タップ経由）ではセットしないため、タップ経路は scroll 位置維持。
            _pendingScrollToAnchor = true;
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
        PageContentReset?.Invoke(this, new PageContentResetArgs(ScrollIndex: 0, ToCenter: false));
    }

    private bool CanGoPrev() => CurrentPage > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPageAsync()
    {
        CurrentPage++;
        await LoadPageAsync();
        PageContentReset?.Invoke(this, new PageContentResetArgs(ScrollIndex: 0, ToCenter: false));
    }

    private bool CanGoNext() => CurrentPage < MaxPage;
}

/// <summary>
/// PageContentReset イベントのペイロード。スクロール先のインデックスと位置 (先頭 / 中央) を渡す。
/// ToCenter=true は初回アンカースクロール (前回読んだ話を中央表示)、false はページ切替時の先頭リセット。
/// </summary>
public sealed record PageContentResetArgs(int ScrollIndex, bool ToCenter);
