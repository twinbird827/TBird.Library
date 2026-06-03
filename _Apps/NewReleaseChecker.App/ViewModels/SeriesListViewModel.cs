using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>登録シリーズ一覧（SCR-003 / F-002）。</summary>
public partial class SeriesListViewModel : ObservableObject
{
    private readonly ISeriesRepository _series;
    private readonly IBookRepository _book;
    private readonly NewReleaseCheckService _check;
    private readonly IUserNotifier _notifier;

    public SeriesListViewModel(
        ISeriesRepository series, IBookRepository book,
        NewReleaseCheckService check, IUserNotifier notifier)
    {
        _series = series;
        _book = book;
        _check = check;
        _notifier = notifier;
    }

    public ObservableCollection<SeriesListItem> Items { get; } = new();

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isBusy;

    /// <summary>並び替え: 0=最新巻発売日順, 1=登録順, 2=シリーズ名順。</summary>
    [ObservableProperty] private int _sortOption;

    /// <summary>絞り込み: 0=すべて, 1=ラノベのみ, 2=コミックのみ。</summary>
    [ObservableProperty] private int _filterOption;

    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    /// <summary>DB から構築した全シリーズ項目のキャッシュ（絞込/並替はこれをメモリ上で処理する）。</summary>
    private List<SeriesListItem> _allItems = new();

    // 並替/絞込は DB 再取得せずキャッシュ（_allItems）に対してメモリ上で行う（UI ホットパスでの全表スキャン回避）。
    partial void OnSortOptionChanged(int value) => ApplySortAndFilter();
    partial void OnFilterOptionChanged(int value) => ApplySortAndFilter();

    public async Task InitializeAsync() => await LoadAsync();

    /// <summary>DB から全シリーズ・全巻を取得して項目キャッシュを再構築し、現在の並替/絞込を適用する。</summary>
    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var allSeries = await _series.GetAllAsync();
            var allBooks = await _book.GetAllAsync();
            var bySeries = allBooks.Where(b => b.SeriesId.HasValue)
                                   .GroupBy(b => b.SeriesId!.Value)
                                   .ToDictionary(g => g.Key, g => g.ToList());

            var items = new List<SeriesListItem>();
            foreach (var sr in allSeries)
            {
                bySeries.TryGetValue(sr.Id, out var books);
                books ??= new List<Book>();
                var latest = books
                    .OrderByDescending(b => ReleaseDateParser.Parse(b.ReleaseDate) ?? DateTime.MinValue)
                    .FirstOrDefault();
                var unpurchased = books.Count(b => b.IsPurchased == 0);

                items.Add(new SeriesListItem
                {
                    Series = sr,
                    LatestBook = latest,
                    UnpurchasedCount = unpurchased,
                    AuthorDisplay = latest?.Author ?? DisplayFormat.Authors(sr.AuthorSet),
                    LatestReleaseInfo = latest is null ? string.Empty : $"最新刊: {DisplayFormat.Release(latest.ReleaseDate)}",
                });
            }

            _allItems = items;
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

    /// <summary>キャッシュ済み項目に現在の絞込（MediaType）と並替を適用して Items を差し替える。</summary>
    private void ApplySortAndFilter()
    {
        IEnumerable<SeriesListItem> filtered = FilterOption switch
        {
            1 => _allItems.Where(i => i.Series.MediaType == MediaType.Novel),
            2 => _allItems.Where(i => i.Series.MediaType == MediaType.Comic),
            _ => _allItems,
        };

        IEnumerable<SeriesListItem> sorted = SortOption switch
        {
            1 => filtered.OrderByDescending(i => i.Series.RegisteredAt),
            2 => filtered.OrderBy(i => i.SeriesKey, StringComparer.Ordinal),
            _ => filtered.OrderByDescending(i => ReleaseDateParser.Parse(i.LatestBook?.ReleaseDate) ?? DateTime.MinValue),
        };

        Items.Clear();
        foreach (var i in sorted) Items.Add(i);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task AddSeriesAsync()
        => await Shell.Current.GoToAsync(Routes.SeriesSearch);

    [RelayCommand]
    private async Task OpenSeriesAsync(SeriesListItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"{Routes.SeriesDetail}?seriesId={item.Id}");
    }

    [RelayCommand]
    private async Task DeleteSeriesAsync(SeriesListItem? item)
    {
        if (item is null) return;
        var ok = await Shell.Current.DisplayAlert("シリーズ削除",
            $"「{item.SeriesKey}」と紐づく全巻を削除します。よろしいですか？", "削除", "キャンセル");
        if (!ok) return;

        await _book.DeleteBySeriesAsync(item.Id);
        await _series.DeleteAsync(item.Id);
        await LoadAsync();
    }

    /// <summary>プルリフレッシュ → 手動チェック（F-003）。共通サービス・同一ローテーションを使う。</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var summary = await _check.CheckAsync(CheckTrigger.Manual);
            await LoadAsync();
            await _notifier.ShowToastAsync($"チェック完了: {summary.TargetCount}件中 新刊{summary.NewCount}件");
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            await _notifier.ShowToastAsync("更新に失敗しました");
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
