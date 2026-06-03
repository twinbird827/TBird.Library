using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>お気に入り一覧（SCR-007 / F-010）。SeriesId=NULL の単発巻は「未追跡」と表示。</summary>
public partial class FavoritesViewModel : ObservableObject
{
    private readonly IBookRepository _book;
    private readonly ISeriesRepository _series;

    public FavoritesViewModel(IBookRepository book, ISeriesRepository series)
    {
        _book = book;
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
            _allFavorites = (await _book.GetFavoritesAsync()).ToList();
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

    [RelayCommand]
    private async Task OpenBookAsync(BookListItem? item)
    {
        if (item?.BookId is not { } id) return;
        await Shell.Current.GoToAsync($"{Routes.BookDetail}?bookId={id}");
    }
}
