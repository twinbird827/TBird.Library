using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using TBird.Core;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class EpisodeListViewModel(
    NovelRepository novelRepo,
    EpisodeRepository episodeRepo,
    EpisodeCacheRepository cacheRepo,
    AppSettingsRepository settingsRepo,
    PrefetchService prefetch) : AutoReloadViewModel, IQueryAttributable
{
    private int _novelDbId;
    public int NovelDbId => _novelDbId;
    private List<Episode> _allEpisodes = [];
    private HashSet<int> _cachedIds = [];
    private List<Episode> _filteredCache = [];

    [ObservableProperty]
    public partial string NovelTitle { get; set; } = string.Empty;

    // ItemsSource は ObservableCollection の同一インスタンスを保持し、ページ切替時は
    // Clear + Add で内容のみ差し替える。List 再代入だと CollectionView がアダプタを
    // 全リセットしてコンテナ再利用 (RecyclerView のプール) が効かず、ページ切替が体感重くなる。
    // 単発 Reset (notifyDataSetChanged 相当) は Android で逆に全アイテム invalidate が走り
    // 遅いことが多いため、incremental な Clear + Add の方が体感が速い。
    public ObservableCollection<EpisodeViewModel> Episodes { get; } = [];

    // 「ページが切り替わった」セマンティクスを View へ通知する。Episodes 更新と同一 UI tick で
    // 発火することで、View 側で ScrollTo を呼んでもユーザーには「先頭で表示されてからスクロール」
    // の途中過程が見えない (RecyclerView が同じレイアウトパスでアイテム配置とスクロール target を
    // 両方処理する)。RefreshReadStatusAsync 等のインプレース更新では発火させない。
    public event EventHandler<PageContentResetArgs>? PageContentReset;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    public partial int MaxPage { get; set; }

    [ObservableProperty]
    public partial bool HasChapters { get; set; }

    [ObservableProperty]
    public partial bool HasLastRead { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsNovelFavorite { get; set; }

    [ObservableProperty]
    public partial bool ShowUnreadOnly { get; set; }

    [ObservableProperty]
    public partial bool ShowFavoritesOnly { get; set; }

    // Shell のスライドアニメ (~250ms) と LoadPage の Clear+Add が UI スレッドで競合すると
    // 遷移が中途半端な位置で詰まって見える。Dispatcher.Dispatch (1 tick) では不足、200ms で
    // 詰まりが解消することを実機検証済。InitializeAsync / JumpToEpisodeAsync / RefreshReadStatusAsync
    // の 3 か所で同じ意味で使うため定数化。
    private const int ShellAnimationSettleMs = 200;

    private int _episodesPerPage = 50;
    private Novel? _novel;
    private Task? _initTask;

    // JumpToEpisodeAsync の in-flight 追跡。Reader→戻る の戻りジャンプを連打されたとき、前回が
    // 終わっていなければ次の起動をスキップする (二重 fetch / scroll target 競合 / _allEpisodes
    // mutate の並走を避ける)。fire-and-forget 起動なので Task を保持する必要がある。
    private Task? _jumpTask;

    // InitializeAsync / JumpToEpisodeAsync 完了直後の OnAppearing で RefreshReadStatusAsync を
    // スキップするためのフラグ。両者で IsRead は最新化済みのため、直後の Refresh は DB の二重
    // fetch を回避するためにスキップする。
    private bool _skipNextRefresh;

    // ReloadEpisodesAsync の再入防止フラグ。背面チェックの UpdatesDetectedMessage が近接連投された場合、
    // 受動的な背面再読込ではスピナー(IsLoading)を出したくないため、IsLoading とは別の同期フラグで
    // 二重実行だけを弾く。UI スレッド上でのみ check/set するため await を挟まず原子性が保たれる。
    private bool _isAutoReloading;

    // _allEpisodes / _filteredCache / Episodes / CurrentPage を変更する全経路(Initialize / ReloadEpisodes /
    // JumpToEpisode / RefreshReadStatus / ReloadList)を直列化するゲート。これらは UI スレッド上で動くが、
    // 各々が await(DB fetch / アニメ待ち)で yield するため、yield 中に別経路が割り込んで「fetch 前に読んだ
    // 状態」を前提に代入を進め、アンカー誤算出・誤スクロール・Episodes の Clear+Add 競合を起こしうる
    // (各経路の冒頭ガードは最初の await の前でしか評価されず、await 後の再チェックが無い)。本ゲートで
    // 変更シーケンス全体を排他にし、await 跨ぎの再入を断つ。通常の単一フローでは無競合で即時取得のため
    // UX タイミングは不変、実際に並走したときだけ直列化される。どの経路も他経路を呼ばないため再入なし。
    private readonly SemaphoreSlim _mutateGate = new(1, 1);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_initTask is not null)
        {
            // 既に init 起動済み: MAUI Shell が Reader→目次の戻り遷移で ApplyQueryAttributes を
            // 再発火させているケース。作品 Id を照合するのは、通知のディープリンクが目次の上に
            // 別作品の Reader を直接積みうるため (別作品なら OnAppearing の既読表示更新だけにする)。
            //
            // InitializeAsync が成功完了していない場合 (進行中 or 失敗) は JumpToEpisodeAsync を
            // 走らせない: _allEpisodes が未確定だと不正な位置計算をし、Init の IsLoading 等の
            // 状態とも競合するため。
            var (lastNovelId, lastEpisodeId) = ReaderViewModel.LastShownEpisode;
            if (lastNovelId == _novelDbId
                && _initTask.IsCompletedSuccessfully
                && _jumpTask is null or { IsCompleted: true })
            {
                // `JumpToEpisodeAsync` の中で同等の fetch を済ませるため、続く OnAppearing 経由の
                // RefreshReadStatusAsync は不要。fire-and-forget 前にフラグを立てて race を避ける
                // (JumpToEpisodeAsync の末尾で立てると、先に始まる RefreshReadStatusAsync のチェックに間に合わない)。
                _skipNextRefresh = true;
                _jumpTask = JumpToEpisodeAsync(lastEpisodeId);
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

    // 目次はシステム通知抑止に寄与させない。目次が反映するのは表示中作品(_novelDbId)の話一覧のみで、
    // 別作品の更新は OnUpdatesDetectedAsync が早期 return して反映しない。抑止に寄与させると、目次
    // 滞在中に別作品の新着があってもシステム通知が出ず、アプリ内にも現れず取りこぼしになるため。
    protected override bool ParticipatesInNotificationSuppression => false;

    /// <summary>
    /// 新着検出時、表示中作品に関係する更新のみ目次を再読込する。UpdatesDetectedMessage は新着のあった
    /// 作品 Id 一覧を持つため、本作品が対象でなければ全話再読込(GetByNovelIdAsync)を行わずに済ませる。
    /// </summary>
    protected override async Task OnUpdatesDetectedAsync(UpdatesDetectedMessage message)
    {
        // 別作品だけの更新ではこの目次に変化は無い → 高コストな全話ロードを避ける。
        if (_novelDbId <= 0 || !message.NovelIds.Contains(_novelDbId)) return;
        await ReloadEpisodesAsync();
    }

    /// <summary>
    /// 新着検出時に _allEpisodes を再取得して現在ページを再描画する。初期化中・戻りジャンプ中は
    /// _allEpisodes の mutate と scroll target が競合するためスキップする(次の OnAppearing が反映する)。
    /// 新着が無ければ何もしない。閲覧位置を保つため scroll(PageContentReset)はあえて発火しない
    /// (新着は末尾ページに出るため現在ページの表示位置は変わらない)。
    /// </summary>
    private async Task ReloadEpisodesAsync()
    {
        // IsLoading は設定せず _isAutoReloading だけを見る。旧実装は IsLoading を見るだけで立てて
        // いなかったため、近接連投された 2 つの通知が両方ガードを抜けて _allEpisodes / Episodes を
        // 並走 mutate し、コレクションやページングが壊れていた。check と set の間に await を挟まない
        // ことで UI スレッド上の原子性を保ち、二重実行を確実に弾く。
        if (IsLoading || _isAutoReloading) return;
        if (_initTask is null || !_initTask.IsCompletedSuccessfully) return;
        if (_jumpTask is { IsCompleted: false }) return;
        if (_novelDbId <= 0) return;

        _isAutoReloading = true;
        await _mutateGate.WaitAsync();
        try
        {
            var fresh = await episodeRepo.GetByNovelIdAsync(_novelDbId);
            var freshCachedIds = await cacheRepo.GetCachedEpisodeIdsAsync(_novelDbId);

            // 件数増(新着)に加え、site_episode_id 補完・タイトル訂正・誤話キャッシュ破棄(cached バッジ変化)
            // など件数不変の同内容差し替えも反映する。件数だけを契機にすると本PRの誤話是正(backfill が
            // episode_cache を破棄し安定 ID で取り直させる)が、表示中の目次に反映されないため。
            if (!HasMeaningfulEpisodeChange(fresh, freshCachedIds)) return; // 変化なし → 何もしない

            _allEpisodes = fresh;
            _cachedIds = freshCachedIds;
            RebuildFilterCache();
            RecalcPaging();
            LoadPage();
        }
        finally
        {
            _mutateGate.Release();
            _isAutoReloading = false;
        }
    }

    // 表示中の目次に反映すべき変化があるか。新着(件数差)に加え、Id 集合・タイトル・site_episode_id・
    // キャッシュ済み集合(cached バッジ)の変化を検出する。背面チェックは本作品が対象のときだけ本判定を
    // 走らせるため(OnUpdatesDetectedAsync の事前フィルタ)、ここでの追加クエリ・比較コストは限定的。
    private bool HasMeaningfulEpisodeChange(List<Episode> fresh, HashSet<int> freshCachedIds)
    {
        if (fresh.Count != _allEpisodes.Count) return true;
        if (!freshCachedIds.SetEquals(_cachedIds)) return true;
        var oldById = _allEpisodes.ToDictionary(e => e.Id);
        foreach (var e in fresh)
        {
            if (!oldById.TryGetValue(e.Id, out var old)) return true;
            if (old.Title != e.Title || old.SiteEpisodeId != e.SiteEpisodeId) return true;
        }
        return false;
    }

    public async Task InitializeAsync()
    {
        // 変更経路の直列化ゲートを取得(WaitAsync は throw しない経路のため、以降の finally で必ず解放される)。
        await _mutateGate.WaitAsync();
        IsLoading = true;
        ClearError();
        try
        {
            _novel = await novelRepo.GetByIdAsync(_novelDbId);
            if (_novel is null) return;

            _episodesPerPage = await settingsRepo.GetIntValueAsync(SettingsKeys.EPISODES_PER_PAGE, 50);

            _allEpisodes = await episodeRepo.GetByNovelIdAsync(_novelDbId);
            _cachedIds = await cacheRepo.GetCachedEpisodeIdsAsync(_novelDbId);
            RebuildFilterCache();

            // _allEpisodes は既にロード済みなので、HasChapters / HasLastRead は in-memory で導出。
            var hasChapters = _allEpisodes.Any(e => e.ChapterName is not null);
            var hasLastRead = _allEpisodes.Any(e => e.IsRead);

            NovelTitle = _novel.Title;
            HasChapters = hasChapters;
            HasLastRead = hasLastRead;
            IsNovelFavorite = _novel.IsFavorite;

            RecalcPaging();

            // ApplyQueryAttributes 側で再走を抑止しているため、ここに到達するのは VM ライフタイムで 1 回のみ。
            var (lastNovelId, lastEpisodeId) = ReaderViewModel.LastShownEpisode;
            var (anchorPage, anchorIdxInPage) = lastNovelId == _novelDbId
                ? ComputePosition(lastEpisodeId)
                : (null, null);
            if (anchorPage.HasValue) CurrentPage = anchorPage.Value;

            // Shell スライドアニメ完了を待ってからリスト構築する (アイテム追加との競合回避)。
            // 詳細は ShellAnimationSettleMs の定義コメント参照。
            await Task.Delay(ShellAnimationSettleMs);
            LoadPage();

            // LoadPage 完了と同じ UI tick でスクロール target を渡すことで、
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
            MessageService.Error($"InitializeAsync failed: {ex}");
            SetError($"目次の読み込みに失敗しました: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _mutateGate.Release();
        }
    }

    private (int? page, int? indexInPage) ComputePosition(int episodeId)
    {
        var idxInFiltered = _filteredCache.FindIndex(e => e.Id == episodeId);
        if (idxInFiltered < 0) return (null, null);

        var page = (idxInFiltered / _episodesPerPage) + 1;
        var indexInPage = idxInFiltered % _episodesPerPage;
        return (page, indexInPage);
    }

    // MAUI Shell が ApplyQueryAttributes を再発火させるタイミングで呼ばれる想定。
    // `_skipNextRefresh = true` は呼び出し元 (ApplyQueryAttributes) で先に立てる
    // (race を避けるため。詳細は ApplyQueryAttributes 側コメント参照)。
    //
    // 2 段階処理 (Stage 1 はページ内 / ページ跨ぎで分岐):
    //   Stage 1A (ページ内): stale な一覧で即時 scroll (アニメと並行)。ジャンプ先の話は既知で、
    //                       Stage 2 でずれるのはフィルタ再構築による位置だけなので、
    //                       stale でもユーザーには遅延なく「ほぼ正しい位置」が見える。
    //   Stage 1B (ページ跨ぎ): pre-Reader のページ内容を即時 hide (Episodes.Clear + IsLoading=true)。
    //                       Stage 2 相当の Clear+Add (LoadPage) をアニメ中に走らせると詰まるため
    //                       Stage 2 に回すが、「pre-Reader 位置を見せて急に飛ぶ」よりは「ローディング
    //                       → アンカー位置で表示」の方が違和感が小さい (ユーザー要望)。
    //   Stage 2: アニメ完了 (Task.Delay ShellAnimationSettleMs) を待ってから DB fetch + IsRead 反映 +
    //            必要なら LoadPage / 再 scroll。
    private async Task JumpToEpisodeAsync(int episodeId)
    {
        if (_allEpisodes.Count == 0) return;

        // 変更経路の直列化ゲート。空チェックの後に取得し、finally で必ず解放する。
        await _mutateGate.WaitAsync();
        bool clearedInStage1B = false;
        try
        {
            // --- Stage 1: stale な位置を計算してページ内 / 跨ぎで分岐 ---
            var (staleTargetPage, staleTargetIdx) = ComputePosition(episodeId);
            bool sameStartingPage = staleTargetPage.HasValue && staleTargetPage.Value == CurrentPage;
            if (sameStartingPage && staleTargetIdx.HasValue)
            {
                // Stage 1A: 同じページ内 → ScrollTo 単発はアニメと並行可、即時発火。
                PageContentReset?.Invoke(this, new PageContentResetArgs(
                    ScrollIndex: staleTargetIdx.Value,
                    ToCenter: true));
            }
            else if (staleTargetPage.HasValue)
            {
                // Stage 1B: ページ跨ぎ → pre-Reader のページ内容を即時 hide。
                // Episodes.Clear は Reset 通知だが空リスト化なので Clear+Add より軽い (ViewHolder 解放のみ)。
                Episodes.Clear();
                IsLoading = true;
                clearedInStage1B = true;
            }

            // --- Stage 2: アニメ完了後に fresh fetch + 反映 ---
            await Task.Delay(ShellAnimationSettleMs);

            // Reader 閲覧中に取り込まれた新話へ「次へ」で進んだ戻りでは、ジャンプ先が一覧に無いので取り直す。
            bool refetched = !_allEpisodes.Exists(e => e.Id == episodeId);
            if (refetched)
            {
                _allEpisodes = await episodeRepo.GetByNovelIdAsync(_novelDbId);
                _cachedIds = await cacheRepo.GetCachedEpisodeIdsAsync(_novelDbId);
            }

            // Reader が話を既読化している可能性があるので既読状態だけを取り直して反映する。
            var readIds = await episodeRepo.GetReadEpisodeIdsAsync(_novelDbId);
            foreach (var ep in _allEpisodes)
            {
                ep.IsRead = readIds.Contains(ep.Id);
            }
            RebuildFilterCache();
            RecalcPaging();

            var (freshTargetPage, freshTargetIdx) = ComputePosition(episodeId);
            bool pageChanged = freshTargetPage.HasValue && freshTargetPage.Value != CurrentPage;

            if (pageChanged)
            {
                // ページ跨ぎ → LoadPage 必須
                CurrentPage = freshTargetPage!.Value;
                LoadPage();
            }
            else if (refetched)
            {
                // 下の in-place 更新では新話が Episodes に入らず、無い行の位置へ scroll してしまう
                LoadPage();
            }
            else if (ShowUnreadOnly)
            {
                // 既読化された話がフィルタで除外されるため rebuild 必要
                LoadPage();
            }
            else if (Episodes.Count == 0)
            {
                // Stage 1B で Clear した場合、ページ跨ぎでなくても再描画必要 (rare: stale 位置は別ページだが
                // fresh 位置は CurrentPage に戻ってきた、またはフィルタで除外されたケース)
                LoadPage();
            }
            else
            {
                // 同じページ・フィルタ内で IsRead だけ変わるケース: in-place 更新で済ませる。
                // Clear+Add しないので Stage 1A の scroll 位置がそのまま温存される (フラッシュ無し)。
                foreach (var vm in Episodes)
                {
                    vm.IsRead = readIds.Contains(vm.Id);
                }
            }

            // フィルタで除外された話は先頭へ戻さず、見ていた位置を保つ。
            if (freshTargetIdx is int targetIdx
                && (!sameStartingPage || pageChanged || freshTargetIdx != staleTargetIdx))
            {
                PageContentReset?.Invoke(this, new PageContentResetArgs(
                    ScrollIndex: targetIdx,
                    ToCenter: true));
            }
        }
        catch (Exception ex)
        {
            // fire-and-forget で呼ばれるため、例外を握り潰さないと unobserved になる。
            // InitializeAsync と同等に SetError でユーザーに通知する。
            MessageService.Error($"JumpToEpisodeAsync failed: {ex}");
            SetError($"目次の再読込に失敗しました: {ex.Message}");
        }
        finally
        {
            // Stage 1B で立てた IsLoading のみ巻き戻す。Stage 1A 経路は IsLoading を触っていないので
            // ここで無条件に false にすると、稀に並走している InitializeAsync の IsLoading=true を
            // 横取りしてしまう (gating の見落とし) ため、フラグで限定する。
            if (clearedInStage1B) IsLoading = false;
            _mutateGate.Release();
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

    private void LoadPage()
    {
        Episodes.Clear();
        foreach (var e in _filteredCache.Skip((CurrentPage - 1) * _episodesPerPage).Take(_episodesPerPage))
        {
            Episodes.Add(EpisodeViewModel.FromModel(e, _cachedIds.Contains(e.Id)));
        }
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

        // 変更経路の直列化ゲート。早期スキップ判定の後に取得し、finally で必ず解放する。
        await _mutateGate.WaitAsync();
        try
        {
            // Reader→目次の戻り遷移ではここで Shell スライドアニメが進行中。DB クエリ + IsRead 更新が
            // 重なるとアニメが詰まって見えるため、アニメ完了を待ってから fetch & update する。
            // 詳細は ShellAnimationSettleMs の定義コメント参照。
            await Task.Delay(ShellAnimationSettleMs);

            var readIds = await episodeRepo.GetReadEpisodeIdsAsync(_novelDbId);

            foreach (var ep in _allEpisodes)
            {
                ep.IsRead = readIds.Contains(ep.Id);
            }

            if (ShowUnreadOnly)
            {
                // 未読フィルタ ON のときは既読化した話をリストから外す必要がある
                RebuildFilterCache();
                RecalcPaging();
                LoadPage();
            }
            else
            {
                // フィルタが OFF なら現在表示中のアイテムだけ in-place 更新
                foreach (var vm in Episodes)
                {
                    vm.IsRead = readIds.Contains(vm.Id);
                }
            }
        }
        finally
        {
            _mutateGate.Release();
        }
    }

    partial void OnShowUnreadOnlyChanged(bool value) => _ = ReloadListAsync();
    partial void OnShowFavoritesOnlyChanged(bool value) => _ = ReloadListAsync();

    private async Task ReloadListAsync()
    {
        // 初期化未完了/戻りジャンプ中は Init/Jump 側がページを構築するため触らない。
        // (Init の Task.Delay の隙間でフィルタ再構築と二重 LoadPage が競合し、ページング/アンカーが
        //  壊れるのを防ぐ。フィルタ切替は UI 操作=ページ描画後にしか起きないため、ここでのスキップは安全。)
        if (_initTask is null || !_initTask.IsCompletedSuccessfully) return;
        if (_jumpTask is { IsCompleted: false }) return;

        // 変更経路の直列化ゲート。早期スキップ判定の後に取得し、finally で必ず解放する。
        await _mutateGate.WaitAsync();
        try
        {
            RebuildFilterCache();
            CurrentPage = 1;
            RecalcPaging();
            LoadPage();
            PageContentReset?.Invoke(this, new PageContentResetArgs(ScrollIndex: 0, ToCenter: false));
        }
        finally
        {
            _mutateGate.Release();
        }
    }

    [RelayCommand]
    private async Task ReadContinueAsync()
    {
        await CommandGuard.RunAsync(async () =>
        {
            // 遷移先の規則(最初の未読話、無ければ最後の既読話)は通知のディープリンクと共用する。
            var targets = await episodeRepo.GetDeepLinkTargetEpisodeIdsAsync([_novelDbId]);
            if (targets.TryGetValue(_novelDbId, out var targetId) && _novel is not null)
            {
                await Shell.Current.GoToAsync(
                    ReaderRoute.Build(_novelDbId, targetId, _novel.SiteType, _novel.NovelId));
            }
        }, SetError, "続きを開けませんでした");
    }

    [RelayCommand]
    private async Task NavigateToEpisode(EpisodeViewModel episode)
    {
        if (_novel is null) return;
        await Shell.Current.GoToAsync(
            ReaderRoute.Build(_novelDbId, episode.Id, _novel.SiteType, _novel.NovelId));
    }

    [RelayCommand]
    private async Task ToggleEpisodeFavoriteAsync(EpisodeViewModel ep)
    {
        await CommandGuard.RunAsync(async () =>
        {
            var newValue = !ep.IsFavorite;
            await episodeRepo.SetFavoriteAsync(ep.Id, newValue);
            ep.IsFavorite = newValue;

            var source = _allEpisodes.FirstOrDefault(e => e.Id == ep.Id);
            if (source is not null) source.IsFavorite = newValue;

            if (ShowFavoritesOnly)
            {
                RebuildFilterCache();
                RecalcPaging();
                LoadPage();
            }
        }, SetError, "お気に入りの更新に失敗しました");
    }

    [RelayCommand]
    private async Task ToggleNovelFavoriteAsync()
    {
        await CommandGuard.RunAsync(async () =>
        {
            if (_novel is null) return;
            var newValue = !IsNovelFavorite;
            await novelRepo.SetFavoriteAsync(_novel.Id, newValue);
            IsNovelFavorite = newValue;
            _novel.IsFavorite = newValue;
        }, SetError, "お気に入りの更新に失敗しました");
    }

    [RelayCommand]
    private async Task DownloadAllAsync()
    {
        await CommandGuard.RunAsync(async () =>
        {
            if (_novel is null) return;
            var enqueued = await prefetch.EnqueueNovelAsync(_novelDbId, highPriority: true);
            await Shell.Current.DisplayAlertAsync("一括ダウンロード",
                enqueued > 0
                    ? $"{enqueued}話をバックグラウンドで取得します（Wi-Fi接続時のみ）"
                    : "新規取得する話はありません",
                "OK");
        }, SetError, "一括ダウンロードの登録に失敗しました");
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void PrevPage()
    {
        CurrentPage--;
        LoadPage();
        PageContentReset?.Invoke(this, new PageContentResetArgs(ScrollIndex: 0, ToCenter: false));
    }

    private bool CanGoPrev() => CurrentPage > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage()
    {
        CurrentPage++;
        LoadPage();
        PageContentReset?.Invoke(this, new PageContentResetArgs(ScrollIndex: 0, ToCenter: false));
    }

    private bool CanGoNext() => CurrentPage < MaxPage;
}

/// <summary>
/// PageContentReset イベントのペイロード。スクロール先のインデックスと位置 (先頭 / 中央) を渡す。
/// ToCenter=true は初回アンカースクロール (前回読んだ話を中央表示)、false はページ切替時の先頭リセット。
/// </summary>
public sealed record PageContentResetArgs(int ScrollIndex, bool ToCenter);
