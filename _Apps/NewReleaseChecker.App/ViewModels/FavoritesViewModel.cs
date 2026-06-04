using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>お気に入り一覧（SCR-007 / F-010）。SeriesId=NULL の単発巻は「未追跡」と表示。一括選択（F-015）対応。</summary>
public partial class FavoritesViewModel : SelectableBookListViewModel
{
    private readonly ISeriesRepository _series;

    public FavoritesViewModel(IBookRepository book, ISeriesRepository series) : base(book)
    {
        _series = series;
    }

    public ObservableCollection<BookListItem> Items { get; } = new();

    [ObservableProperty] private bool _isBusy;

    /// <summary>0=発売日順, 1=シリーズ名順, 2=お気に入り登録順。</summary>
    [ObservableProperty] private int _sortOption;

    /// <summary>0=すべて, 1=未購入のみ, 2=未発売のみ, 3=未追跡のみ。</summary>
    [ObservableProperty] private int _filterOption;

    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    /// <summary>DB から取得したお気に入り巻とシリーズ名のキャッシュ（絞込/並替はこれをメモリ上で処理する）。</summary>
    private List<Book> _allFavorites = new();
    private Dictionary<int, string> _seriesNames = new();

    // 並替/絞込は DB 再取得せずキャッシュに対してメモリ上で行う（ドロップダウン変更ごとの二重 DB クエリ＋全件再パースを回避）。
    partial void OnSortOptionChanged(int value) => ApplySortAndFilter();
    partial void OnFilterOptionChanged(int value) => ApplySortAndFilter();

    public async Task InitializeAsync() => await LoadAsync();

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            _allFavorites = (await BookRepo.GetFavoritesAsync()).ToList();
            _seriesNames = (await _series.GetAllAsync()).ToDictionary(x => x.Id, x => x.SeriesKey);
            ApplySortAndFilter();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>キャッシュ済みお気に入りに現在の絞込/並替を適用して Items を差し替える。</summary>
    private void ApplySortAndFilter()
    {
        // Items を作り直すため、選択モードが残っていれば解除する（件数/ラベルの陳腐化防止）。
        if (IsSelectionMode) ResetSelection();

        var now = DateTime.Now;

        IEnumerable<Book> filtered = FilterOption switch
        {
            1 => _allFavorites.Where(b => b.IsPurchased == 0),
            2 => _allFavorites.Where(b => ReleaseDateParser.IsFuture(b.ReleaseDate, now)),
            3 => _allFavorites.Where(b => b.SeriesId is null),
            _ => _allFavorites,
        };

        IEnumerable<Book> sorted = SortOption switch
        {
            1 => filtered.OrderBy(b => b.SeriesId.HasValue && _seriesNames.ContainsKey(b.SeriesId.Value)
                    ? _seriesNames[b.SeriesId.Value] : "￿", StringComparer.Ordinal),
            2 => filtered.OrderByDescending(b => b.DetectedAt ?? string.Empty),
            _ => filtered.OrderBy(b => ReleaseDateParser.Parse(b.ReleaseDate) ?? DateTime.MaxValue),
        };

        Items.Clear();
        foreach (var b in sorted)
        {
            var future = ReleaseDateParser.IsFuture(b.ReleaseDate, now);
            var seriesName = b.SeriesId.HasValue && _seriesNames.TryGetValue(b.SeriesId.Value, out var k) ? k : "未追跡";
            Items.Add(new BookListItem
            {
                BookId = b.Id,
                Title = b.Title,
                Author = b.Author,
                Publisher = b.Publisher,
                ImageUrl = b.ImageUrl,
                ReleaseDisplay = DisplayFormat.Release(b.ReleaseDate),
                SeriesName = seriesName,
                IsPurchased = b.IsPurchased == 1,
                ShowCalendarBadge = future && b.IsCalendarRegistered == 0,
            });
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    // ----- 一括選択（F-015）フック -----
    protected override ObservableCollection<BookListItem> SelectionItems => Items;

    // お気に入り一覧の巻は常に永続（BookId あり）のため createIfMissing は不問。
    protected override async Task<Book?> ResolveAsync(BookListItem item, bool createIfMissing)
        => item.BookId is { } id ? await BookRepo.GetAsync(id) : null;

    protected override Task ReloadAsync() => LoadAsync();

    protected override async Task OpenBookAsync(BookListItem item)
    {
        if (item.BookId is not { } id) return;
        await Shell.Current.GoToAsync($"{Routes.BookDetail}?bookId={id}");
    }

    [RelayCommand] private Task BulkPurchase() => ApplyToSelectedAsync(b => b.IsPurchased = 1);
    [RelayCommand] private Task BulkUnpurchase() => ApplyToSelectedAsync(b => b.IsPurchased = 0, createIfMissing: false);
    [RelayCommand] private Task BulkUnfavorite() => ApplyToSelectedAsync(b => b.IsFavorite = 0, createIfMissing: false);
}
