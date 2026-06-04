using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using NewReleaseChecker.Data.Api;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>
/// 巻詳細（SCR-006 / F-014）。追跡中シリーズ巻・ランキング・発売予定表の巻を兼用。
/// 非永続巻（Source）はお気に入り/購入済/カレンダーいずれかの操作時に SeriesId=NULL で永続化する。
/// </summary>
public partial class BookDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IBookRepository _book;
    private readonly ICalendarService _calendar;
    private readonly BookActionService _actions;

    public BookDetailViewModel(IBookRepository book, ICalendarService calendar, BookActionService actions)
    {
        _book = book;
        _calendar = calendar;
        _actions = actions;
    }

    private Book? _persisted;     // 永続巻（あれば）
    private RakutenBook? _source; // 非永続巻の元データ
    private string? _itemUrl;     // Kobo を開く（OpenKoboAsync）で使用
    private DateTime? _releaseDate; // カレンダー登録（AddToCalendarAsync）で使用

    // ApplyQueryAttributes は同期 void だが内部のバインドは非同期。各操作コマンドはこの Task の完了を待ち、
    // バインド未完了のまま（_itemNumber 等が null の状態で）トグル操作が空振りするのを防ぐ。
    private Task _bindTask = Task.CompletedTask;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _publisher = string.Empty;
    [ObservableProperty] private string _releaseDisplay = string.Empty;
    [ObservableProperty] private string _captionText = string.Empty;
    [ObservableProperty] private string _isbnText = string.Empty;
    [ObservableProperty] private string _imageUrl = string.Empty;

    [ObservableProperty] private bool _isPurchased;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isFuture;          // 未発売（カレンダーボタン表示条件）
    [ObservableProperty] private bool _showCalendarBadge; // 未発売かつ未登録

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // バインドを Task として保持し、各操作コマンドが await できるようにする（async void の未観測例外も回避）。
        _bindTask = BindAsync(query);
    }

    private async Task BindAsync(IDictionary<string, object> query)
    {
        try
        {
            if (query.TryGetValue("bookId", out var idObj) && int.TryParse(idObj?.ToString(), out var id))
            {
                var b = await _book.GetAsync(id);
                if (b is not null) BindPersisted(b);
            }
            else if (query.TryGetValue("source", out var src) && src is RakutenBook rb)
            {
                await BindSourceAsync(rb);
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    private void BindPersisted(Book b)
    {
        _persisted = b;
        _source = null;
        _itemUrl = b.ItemUrl;
        _releaseDate = ReleaseDateParser.Parse(b.ReleaseDate);

        Title = b.Title;
        Author = b.Author ?? string.Empty;
        Publisher = b.Publisher ?? string.Empty;
        ImageUrl = b.ImageUrl ?? string.Empty;
        ReleaseDisplay = DisplayFormat.Release(b.ReleaseDate);
        CaptionText = b.Caption ?? string.Empty;
        IsbnText = string.IsNullOrEmpty(b.Isbn) ? string.Empty : $"ISBN: {b.Isbn}";
        IsPurchased = b.IsPurchased == 1;
        IsFavorite = b.IsFavorite == 1;
        IsFuture = ReleaseDateParser.IsFuture(b.ReleaseDate, DateTime.Now);
        ShowCalendarBadge = IsFuture && b.IsCalendarRegistered == 0;
    }

    private async Task BindSourceAsync(RakutenBook rb)
    {
        // 既に永続化済み（ItemNumber 一致）なら既存行を採用
        var existing = await _book.GetByItemNumberAsync(rb.ItemNumber);
        if (existing is not null)
        {
            BindPersisted(existing);
            return;
        }

        _source = rb;
        _persisted = null;
        _itemUrl = rb.ItemUrl;
        var iso = ReleaseDateParser.ToIso(rb.SalesDate);
        _releaseDate = ReleaseDateParser.Parse(iso);

        Title = rb.Title;
        Author = rb.Author ?? string.Empty;
        Publisher = rb.Publisher ?? string.Empty;
        ImageUrl = rb.ImageUrl ?? string.Empty;
        ReleaseDisplay = DisplayFormat.Release(iso);
        CaptionText = rb.Caption ?? string.Empty;
        IsbnText = string.IsNullOrEmpty(rb.Isbn) ? string.Empty : $"ISBN: {rb.Isbn}";
        // 未保存巻のトグル初期状態は未購入・未お気に入り（F-014）
        IsPurchased = false;
        IsFavorite = false;
        IsFuture = ReleaseDateParser.IsFuture(iso, DateTime.Now);
        ShowCalendarBadge = false;
    }

    /// <summary>非永続巻なら DB に INSERT（SeriesId=NULL）。永続巻なら何もしない。</summary>
    /// <remarks>INSERT-on-demand は <see cref="BookActionService.EnsurePersistedAsync"/> に集約済み（一括処理と共有）。</remarks>
    private async Task EnsurePersistedAsync()
    {
        if (_persisted is not null) return;
        if (_source is null) return;
        _persisted = await _actions.EnsurePersistedAsync(_source);
    }

    [RelayCommand]
    private async Task TogglePurchasedAsync()
    {
        await _bindTask; // バインド完了を待ってから操作する
        await EnsurePersistedAsync();
        if (_persisted is null) return;
        _persisted.IsPurchased = _persisted.IsPurchased == 1 ? 0 : 1;
        await _book.UpdateFlagsAsync(_persisted);
        IsPurchased = _persisted.IsPurchased == 1;
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        await _bindTask; // バインド完了を待ってから操作する
        await EnsurePersistedAsync();
        if (_persisted is null) return;
        _persisted.IsFavorite = _persisted.IsFavorite == 1 ? 0 : 1;
        await _book.UpdateFlagsAsync(_persisted);
        IsFavorite = _persisted.IsFavorite == 1;
    }

    [RelayCommand]
    private async Task AddToCalendarAsync()
    {
        await _bindTask; // バインド完了を待ってから操作する
        if (_releaseDate is null) return;
        await EnsurePersistedAsync();
        if (_persisted is null) return;

        var ok = await _calendar.AddEventAsync($"【発売】{Title}", _releaseDate.Value, Publisher);
        if (ok)
        {
            _persisted.IsCalendarRegistered = 1;
            await _book.UpdateFlagsAsync(_persisted);
            ShowCalendarBadge = false;
        }
    }

    [RelayCommand]
    private async Task OpenKoboAsync()
    {
        await _bindTask; // バインド完了を待ってから _itemUrl を使う
        // アフィリエイトURL生成は当面未使用（将来は中継サーバー側で付与）。素の itemUrl を開く。
        var url = UrlBuilder.GetProductUrl(_itemUrl, null);
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.External);
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }
}
