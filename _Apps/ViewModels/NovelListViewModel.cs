using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Services;
using LanobeReader.Services.Database;

namespace LanobeReader.ViewModels;

public partial class NovelListViewModel : ObservableObject
{
    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly UpdateCheckService _updateCheckService;

    public NovelListViewModel(
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        EpisodeCacheRepository cacheRepo,
        UpdateCheckService updateCheckService)
    {
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _cacheRepo = cacheRepo;
        _updateCheckService = updateCheckService;
    }

    [ObservableProperty]
    private ObservableCollection<NovelCardViewModel> _novels = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasCheckError;

    public async Task InitializeAsync()
    {
        await LoadNovelsAsync();
    }

    private async Task LoadNovelsAsync()
    {
        try
        {
            var novels = await _novelRepo.GetAllAsync();
            var cards = new List<NovelCardViewModel>();

            foreach (var novel in novels)
            {
                var unread = await _episodeRepo.CountUnreadByNovelIdAsync(novel.Id);
                cards.Add(NovelCardViewModel.FromModel(novel, unread));
            }

            Novels = new ObservableCollection<NovelCardViewModel>(cards);
            HasCheckError = novels.Any(n => n.HasCheckError == 1);
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(NovelListViewModel), $"LoadNovelsAsync failed: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await _updateCheckService.CheckAllAsync();
            await LoadNovelsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogHelper.Warn(nameof(NovelListViewModel), $"Refresh failed: {ex.Message}");
            HasCheckError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRefresh() => !IsLoading;

    [RelayCommand]
    private async Task NavigateToDetail(NovelCardViewModel card)
    {
        // Confirm unconfirmed update
        var novel = await _novelRepo.GetByIdAsync(card.Id);
        if (novel is not null && novel.HasUnconfirmedUpdate == 1)
        {
            novel.HasUnconfirmedUpdate = 0;
            await _novelRepo.UpdateAsync(novel);
            card.HasUnconfirmedUpdate = false;
        }

        await Shell.Current.GoToAsync($"episodes?novelId={card.Id}");
    }

    [RelayCommand]
    private async Task DeleteCache(NovelCardViewModel card)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "確認", "キャッシュを削除しますか？", "OK", "キャンセル");

        if (confirm)
        {
            await _cacheRepo.DeleteByNovelIdAsync(card.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteNovel(NovelCardViewModel card)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "確認", "タイトルを削除しますか？この操作は元に戻せません。", "OK", "キャンセル");

        if (confirm)
        {
            await _novelRepo.DeleteAsync(card.Id);
            Novels.Remove(card);
        }
    }
}
