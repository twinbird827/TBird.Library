using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using TBird.Core;
using TBird.Maui.ViewModels;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>シリーズ検索・登録（SCR-004 / F-001）。検索失敗は画面にエラー表示（ErrorAwareViewModel）。</summary>
public partial class SeriesSearchViewModel : ErrorAwareViewModel
{
    private readonly IRakutenApiClient _api;
    private readonly NewReleaseCheckService _check;

    public SeriesSearchViewModel(IRakutenApiClient api, NewReleaseCheckService check)
    {
        _api = api;
        _check = check;
    }

    public ObservableCollection<RakutenBook> Results { get; } = new();

    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword)) return;
        try
        {
            IsBusy = true;
            ClearError();
            var items = await _api.SearchAsync(new RakutenSearchQuery { Keyword = SearchKeyword, Hits = 30 });
            Results.Clear();
            foreach (var it in items) Results.Add(it);
            if (Results.Count == 0) SetError("該当する作品が見つかりませんでした。");
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            SetError("検索に失敗しました。通信状況を確認してください。");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>タップした巻から登録ダイアログの初期値を組み立てる（MediaTab: 0=ラノベ, 1=コミック）。</summary>
    public (string SeriesKey, List<AuthorSelection> Authors, int MediaTab) BuildDefault(RakutenBook book)
    {
        var seriesKey = SeriesKeyExtractor.Extract(book.Title);
        var authors = AuthorNormalizer.ToSet(book.Author)
            .Select(a => new AuthorSelection(a))
            .ToList();
        return (seriesKey, authors, 0); // 既定はラノベ。ダイアログで変更可。
    }

    /// <summary>登録確認ダイアログで「登録」された後の処理。</summary>
    public async Task<bool> RegisterConfirmedAsync(SeriesRegistration reg)
    {
        try
        {
            IsBusy = true;
            await _check.RegisterSeriesAsync(reg);
            await Shell.Current.GoToAsync(".."); // SCR-003 へ戻る
            return true;
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            SetError("登録に失敗しました。");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
