using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>
/// 発売予定表（SCR-008）/ ランキング（SCR-009）の共通基底。
/// タブ（ラノベ/コミック）＋ジャンル絞り込みで API を都度フェッチ（DB 未保存）。
/// </summary>
public abstract partial class ApiBrowseViewModel : SelectableBookListViewModel
{
    protected readonly IRakutenApiClient Api;
    private readonly BookActionService _actions;

    protected ApiBrowseViewModel(IRakutenApiClient api, IBookRepository book, BookActionService actions, IUserNotifier notifier)
        : base(book, notifier)
    {
        Api = api;
        _actions = actions;
    }

    public ObservableCollection<BookListItem> Items { get; } = new();
    public ObservableCollection<RakutenGenreNode> Genres { get; } = new();

    [ObservableProperty] private bool _isBusy;

    /// <summary>0=ラノベ, 1=コミック。</summary>
    [ObservableProperty] private int _mediaTab;

    [ObservableProperty] private RakutenGenreNode? _selectedGenre;

    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    // タブ/ジャンル連打で複数の読込が走ると Items/Genres 操作が交錯し「後に完了した方」が表示に残る。
    // 直近要求のみ反映するため CTS で旧読込をキャンセルし、最新の読込だけがコレクションと IsBusy を更新する。
    private CancellationTokenSource? _genreCts;
    private CancellationTokenSource? _loadCts;

    partial void OnMediaTabChanged(int value) => _ = LoadGenresAsync();
    partial void OnSelectedGenreChanged(RakutenGenreNode? value) => _ = LoadAsync();

    public async Task InitializeAsync()
    {
        if (Genres.Count == 0) await LoadGenresAsync();
    }

    private async Task LoadGenresAsync()
    {
        _genreCts?.Cancel();
        var cts = new CancellationTokenSource();
        _genreCts = cts;
        var ct = cts.Token;

        var rootId = KoboGenres.ForMedia(MediaTab);
        // 「すべて」は API 失敗時も必ず先に提示する。ここを空のままにすると finally の SelectedGenre 代入が
        // null→null となって変更通知が出ず、LoadAsync が起動せず画面が空＆無反応のまま復帰できなくなる。
        Genres.Clear();
        Genres.Add(new RakutenGenreNode { KoboGenreId = rootId, GenreName = "すべて" });
        try
        {
            var root = await Api.GetGenreAsync(rootId, ct);
            if (ct.IsCancellationRequested) return;

            foreach (var c in root.Children) Genres.Add(c);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            MessageService.Warn($"ジャンル取得失敗: {ex.Message}");
        }
        finally
        {
            // 最新のジャンル読込のみ SelectedGenre を更新し、その変更が LoadAsync を駆動する。
            if (_genreCts == cts) SelectedGenre = Genres.FirstOrDefault();
        }
    }

    public async Task LoadAsync()
    {
        // タブ/ジャンル変更で一覧を作り直すため、選択モードが残っていれば解除する（件数/ラベルの陳腐化防止）。
        if (IsSelectionMode) ResetSelection();

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var ct = cts.Token;
        try
        {
            IsBusy = true;
            var genreId = SelectedGenre?.KoboGenreId ?? KoboGenres.ForMedia(MediaTab);
            var results = await Api.SearchAsync(BuildQuery(genreId), ct);
            if (ct.IsCancellationRequested) return;

            Items.Clear();
            int rank = 1;
            foreach (var rb in results)
            {
                Items.Add(ToItem(rb, rank));
                rank++;
            }
            // 非永続巻のため、表示後に DB のお気に入り状態を照合して★を反映する（最新読込のみ）。
            if (_loadCts == cts) await RefreshFavoriteFlagsAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
        finally
        {
            // 最新の読込のみ後始末する（旧読込は IsBusy を奪わない）。
            if (_loadCts == cts)
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
                NotifyListChanged(); // 空一覧での「選択」無効化を再評価（F-015）。
            }
        }
    }

    protected abstract RakutenSearchQuery BuildQuery(string genreId);

    protected abstract BookListItem ToItem(RakutenBook rb, int rank);

    protected static BookListItem ToItemBase(RakutenBook rb, int? rank) => new()
    {
        Source = rb,
        Title = rb.Title,
        Author = rb.Author,
        Publisher = rb.Publisher,
        ImageUrl = rb.ImageUrl,
        ReleaseDisplay = DisplayFormat.Release(ReleaseDateParser.ToIso(rb.SalesDate)),
        SeriesName = string.Empty,
        Rank = rank,
    };

    // ----- 一括選択（F-015）フック -----
    protected override ObservableCollection<BookListItem> SelectionItems => Items;

    protected override async Task<Book?> ResolveAsync(BookListItem item, bool createIfMissing)
    {
        if (item.Source is { } src)
            // 付与系は必要なら永続化、解除系は既存巻のみ対象（未永続巻に無駄な孤児行を作らない）。
            return createIfMissing
                ? await _actions.EnsurePersistedAsync(src)
                : await BookRepo.GetByItemNumberAsync(src.ItemNumber);
        return item.BookId is { } id ? await BookRepo.GetAsync(id) : null;
    }

    // API 一覧は再フェッチしないが、一括お気に入り操作後は★表示だけ DB 照合で更新する。
    protected override Task ReloadAsync() => RefreshFavoriteFlagsAsync();

    /// <summary>現在表示中の各巻について、DB のお気に入り状態を ItemNumber 照合で反映する（★表示の更新）。</summary>
    private async Task RefreshFavoriteFlagsAsync()
    {
        var favorites = (await BookRepo.GetFavoritesAsync()).Select(b => b.ItemNumber).ToHashSet();
        foreach (var it in Items)
            if (it.Source is { } src) it.IsFavorite = favorites.Contains(src.ItemNumber);
    }

    protected override async Task OpenBookAsync(BookListItem item)
    {
        if (item.Source is not { } src) return;
        await Shell.Current.GoToAsync(Routes.BookDetail, new Dictionary<string, object> { ["source"] = src });
    }

    // 付与系（★追加）は INSERT してでも対象化。解除系（★解除）は既存お気に入り巻のみ。
    [RelayCommand] private Task BulkFavorite() => ApplyToSelectedAsync(b => b.IsFavorite = 1);
    [RelayCommand] private Task BulkUnfavorite() => ApplyToSelectedAsync(b => b.IsFavorite = 0, createIfMissing: false);
}
