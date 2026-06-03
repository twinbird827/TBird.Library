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

    partial void OnSortOptionChanged(int value) => _ = LoadAsync();
    partial void OnFilterOptionChanged(int value) => _ = LoadAsync();

    public async Task InitializeAsync() => await LoadAsync();

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var favorites = await _book.GetFavoritesAsync();
            var series = (await _series.GetAllAsync()).ToDictionary(x => x.Id, x => x.SeriesKey);
            var now = DateTime.Now;

            IEnumerable<Book> filtered = FilterOption switch
            {
                1 => favorites.Where(b => b.IsPurchased == 0),
                2 => favorites.Where(b => ReleaseDateParser.IsFuture(b.ReleaseDate, now)),
                3 => favorites.Where(b => b.SeriesId is null),
                _ => favorites,
            };

            IEnumerable<Book> sorted = SortOption switch
            {
                1 => filtered.OrderBy(b => b.SeriesId.HasValue && series.ContainsKey(b.SeriesId.Value)
                        ? series[b.SeriesId.Value] : "￿", StringComparer.Ordinal),
                2 => filtered.OrderByDescending(b => b.DetectedAt ?? string.Empty),
                _ => filtered.OrderBy(b => ReleaseDateParser.Parse(b.ReleaseDate) ?? DateTime.MaxValue),
            };

            Items.Clear();
            foreach (var b in sorted)
            {
                var future = ReleaseDateParser.IsFuture(b.ReleaseDate, now);
                var seriesName = b.SeriesId.HasValue && series.TryGetValue(b.SeriesId.Value, out var k) ? k : "未追跡";
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

    [RelayCommand]
    private async Task OpenBookAsync(BookListItem? item)
    {
        if (item?.BookId is not { } id) return;
        await Shell.Current.GoToAsync($"{Routes.BookDetail}?bookId={id}");
    }
}
