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
public abstract partial class ApiBrowseViewModel : ObservableObject
{
    protected readonly IRakutenApiClient Api;

    protected ApiBrowseViewModel(IRakutenApiClient api) => Api = api;

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
        try
        {
            var rootId = KoboGenres.ForMedia(MediaTab);
            var root = await Api.GetGenreAsync(rootId, ct);
            if (ct.IsCancellationRequested) return;

            Genres.Clear();
            Genres.Add(new RakutenGenreNode { KoboGenreId = rootId, GenreName = "すべて" });
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

    [RelayCommand]
    private async Task OpenBookAsync(BookListItem? item)
    {
        if (item?.Source is not { } src) return;
        await Shell.Current.GoToAsync(Routes.BookDetail, new Dictionary<string, object> { ["source"] = src });
    }
}
