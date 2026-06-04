using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>シリーズ詳細（SCR-005 / F-006）。情報＋所属全巻を表示。一括選択（F-015）対応。</summary>
[QueryProperty(nameof(SeriesId), "seriesId")]
public partial class SeriesDetailViewModel : SelectableBookListViewModel
{
    private readonly ISeriesRepository _series;

    public SeriesDetailViewModel(ISeriesRepository series, IBookRepository book) : base(book)
    {
        _series = series;
    }

    public ObservableCollection<BookListItem> Books { get; } = new();

    [ObservableProperty] private int _seriesId;
    [ObservableProperty] private string _seriesKey = string.Empty;
    [ObservableProperty] private string _authorDisplay = string.Empty;
    [ObservableProperty] private string _publisher = string.Empty;
    [ObservableProperty] private string _headerImageUrl = string.Empty;
    [ObservableProperty] private string _volumeCountText = string.Empty;
    [ObservableProperty] private string _lastCheckedText = string.Empty;

    private Series? _model;
    private bool _isLoading;

    // 読込トリガは OnAppearing（SeriesDetailPage）に一本化する。QueryProperty セッタからは起動しない。
    // OnSeriesIdChanged で fire-and-forget すると OnAppearing と二重起動し Books.Clear()/Add() が交錯するため。

    public async Task LoadAsync()
    {
        if (SeriesId <= 0) return;
        if (_isLoading) return; // 再入ガード（万一の二重起動でもコレクション操作を交錯させない）
        _isLoading = true;
        try
        {
            _model = await _series.GetAsync(SeriesId);
            if (_model is null) return;

            var books = await BookRepo.GetBySeriesAsync(SeriesId);
            var now = DateTime.Now;

            SeriesKey = _model.SeriesKey;
            // 発売日が確定（非 NULL）した最新巻をヘッダに採用する。GetBySeriesAsync は NULL 日付を末尾へ
            // 並べるため、LastOrDefault() のままだと未確定日の巻をヘッダに拾ってしまう。
            var latest = books.LastOrDefault(b => b.ReleaseDate != null) ?? books.LastOrDefault();
            HeaderImageUrl = latest?.ImageUrl ?? string.Empty;
            AuthorDisplay = latest?.Author ?? DisplayFormat.Authors(_model.AuthorSet);
            Publisher = latest?.Publisher ?? string.Empty;
            VolumeCountText = $"既刊 {books.Count} 冊";
            LastCheckedText = _model.LastCheckedAt is null
                ? "最終チェック: 未実施"
                : $"最終チェック: {DisplayFormat.Release(_model.LastCheckedAt)}";

            Books.Clear();
            foreach (var b in books)
            {
                var future = ReleaseDateParser.IsFuture(b.ReleaseDate, now);
                Books.Add(new BookListItem
                {
                    BookId = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Publisher = b.Publisher,
                    ImageUrl = b.ImageUrl,
                    ReleaseDisplay = DisplayFormat.Release(b.ReleaseDate),
                    SeriesName = _model.SeriesKey,
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
            _isLoading = false;
        }
    }

    // ----- 一括選択（F-015）フック -----
    protected override ObservableCollection<BookListItem> SelectionItems => Books;

    protected override async Task<Book?> ResolveAsync(BookListItem item)
        => item.BookId is { } id ? await BookRepo.GetAsync(id) : null;

    protected override Task ReloadAsync() => LoadAsync();

    protected override async Task OpenBookAsync(BookListItem item)
    {
        if (item.BookId is not { } id) return;
        await Shell.Current.GoToAsync($"{Routes.BookDetail}?bookId={id}");
    }

    [RelayCommand] private Task BulkPurchase() => ApplyToSelectedAsync(b => b.IsPurchased = 1);
    [RelayCommand] private Task BulkUnpurchase() => ApplyToSelectedAsync(b => b.IsPurchased = 0);

    [RelayCommand]
    private async Task EditSeriesKeyAsync()
    {
        if (_model is null) return;
        var result = await Shell.Current.DisplayPromptAsync(
            "追跡キー編集",
            "シリーズ名（追跡キー）を編集します。\n半角スペースで区切ると、すべての語を含む巻だけを対象にします（AND検索）。",
            initialValue: _model.SeriesKey);
        if (string.IsNullOrWhiteSpace(result)) return;

        _model.SeriesKey = result.Trim();
        await _series.UpdateAsync(_model);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteSeriesAsync()
    {
        if (_model is null) return;
        var ok = await Shell.Current.DisplayAlert("シリーズ削除",
            $"「{_model.SeriesKey}」と紐づく全巻を削除します。よろしいですか？", "削除", "キャンセル");
        if (!ok) return;

        await BookRepo.DeleteBySeriesAsync(SeriesId);
        await _series.DeleteAsync(SeriesId);
        await Shell.Current.GoToAsync("..");
    }
}
