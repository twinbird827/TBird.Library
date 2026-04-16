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
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly AppSettingsRepository _settingsRepo;
    private readonly UpdateCheckService _updateCheckService;
    private readonly NotificationPermissionService _notificationPermission;

    public NovelListViewModel(
        NovelRepository novelRepo,
        EpisodeCacheRepository cacheRepo,
        AppSettingsRepository settingsRepo,
        UpdateCheckService updateCheckService,
        NotificationPermissionService notificationPermission)
    {
        _novelRepo = novelRepo;
        _cacheRepo = cacheRepo;
        _settingsRepo = settingsRepo;
        _updateCheckService = updateCheckService;
        _notificationPermission = notificationPermission;
    }

    [ObservableProperty]
    private ObservableCollection<NovelCardViewModel> _novels = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasCheckError;

    [ObservableProperty]
    private string _sortKey = "updated_desc";

    private bool _sortKeyLoaded;
    private bool _needsReload = true;
    private bool _isInitializing;

    public async Task InitializeAsync()
    {
        if (!_sortKeyLoaded)
        {
            _isInitializing = true;
            try
            {
                SortKey = await _settingsRepo.GetValueAsync(SettingsKeys.NOVEL_SORT_KEY, "updated_desc");
            }
            finally
            {
                _isInitializing = false;
            }
            _sortKeyLoaded = true;
            await _notificationPermission.EnsureRequestedAsync();
        }
        if (!_needsReload)
        {
            // 登録・削除でDB件数が変わった場合はリロード
            var dbCount = await _novelRepo.CountAsync();
            if (dbCount == Novels.Count) return;
        }
        await LoadNovelsAsync();
        _needsReload = false;
    }


    private async Task LoadNovelsAsync()
    {
        try
        {
            var rows = await _novelRepo.GetAllWithUnreadCountAsync(SortKey);
            Novels = new ObservableCollection<NovelCardViewModel>(
                rows.Select(r => NovelCardViewModel.FromModel(r.Novel, r.UnreadCount)));
            HasCheckError = rows.Any(r => r.Novel.HasCheckError == 1);
        }
        catch (Exception ex)
        {
            LogHelper.Error(nameof(NovelListViewModel), $"LoadNovelsAsync failed: {ex.Message}");
        }
    }

    partial void OnSortKeyChanged(string value)
    {
        if (_isInitializing) return;
        _ = _settingsRepo.SetValueAsync(SettingsKeys.NOVEL_SORT_KEY, value);
        _ = LoadNovelsAsync();
    }

    [RelayCommand]
    private async Task ChangeSortAsync()
    {
        var options = new[]
        {
            "更新日時（新しい順）",
            "更新日時（古い順）",
            "タイトル昇順",
            "タイトル降順",
            "作者昇順",
            "登録日時（新しい順）",
            "未読話数（多い順）",
            "お気に入り優先",
        };
        var selected = await Shell.Current.DisplayActionSheet("並び順", "キャンセル", null, options);
        if (string.IsNullOrEmpty(selected) || selected == "キャンセル") return;

        SortKey = selected switch
        {
            "更新日時（新しい順）" => "updated_desc",
            "更新日時（古い順）" => "updated_asc",
            "タイトル昇順" => "title_asc",
            "タイトル降順" => "title_desc",
            "作者昇順" => "author_asc",
            "登録日時（新しい順）" => "registered_desc",
            "未読話数（多い順）" => "unread_desc",
            "お気に入り優先" => "favorite_first",
            _ => SortKey,
        };
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await _updateCheckService.CheckAllAsync();
            _needsReload = true;
            await LoadNovelsAsync();
            _needsReload = false;
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
    private async Task ToggleFavoriteAsync(NovelCardViewModel card)
    {
        var newValue = !card.IsFavorite;
        await _novelRepo.SetFavoriteAsync(card.Id, newValue);
        card.IsFavorite = newValue;
        if (SortKey == "favorite_first")
        {
            await LoadNovelsAsync();
        }
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
            _needsReload = true;
        }
    }
}
