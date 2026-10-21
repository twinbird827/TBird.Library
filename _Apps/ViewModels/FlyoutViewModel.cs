using ComicViewer.Data;
using ComicViewer.Logic;
using ComicViewer.Services;
using ComicViewer.Sources;
using CommunityToolkit.Mvvm.Input;
using TBird.Core;
using TBird.Maui.Collections;

namespace ComicViewer.ViewModels;

public partial class FlyoutViewModel
{
    private const int HistoryCount = 50;

    private readonly FileSourceProvider _sources;
    private readonly FavoriteRepository _favorites;
    private readonly ReadingStateRepository _readingStates;
    private readonly ToastService _toast;

    public FlyoutViewModel(FileSourceProvider sources, FavoriteRepository favorites, ReadingStateRepository readingStates, ToastService toast)
    {
        _sources = sources;
        _favorites = favorites;
        _readingStates = readingStates;
        _toast = toast;
    }

    public BulkObservableCollection<SourceGroup> Groups { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var favorites = (await _favorites.GetAllAsync()).Select(x => new SourceItem
            {
                Kind = SourceItemKind.Favorite,
                Id = x.Id,
                SourceKey = x.SourceKey,
                Path = x.Path,
                Name = x.Name,
            }).ToList();
            var history = (await _readingStates.GetHistoryAsync(HistoryCount)).Select(x => new SourceItem
            {
                Kind = SourceItemKind.History,
                Id = x.Id,
                SourceKey = x.SourceKey,
                Path = x.Path,
                Name = x.Path.EndsWith('*') ? $"{EntryRules.Name(EntryRules.Parent(x.Path))} {FolderItemViewModel.DirectImagesName}" : EntryRules.Name(x.Path),
                StatusText = FolderItemViewModel.GetStatusText(x),
                KindBadge = EntryRules.KindBadge(x.Path),
                Icon = "comic.png",
            }).ToList();
            if (favorites.Count == 0) favorites.Add(new SourceItem { Kind = SourceItemKind.Placeholder, Name = "ありません", IsEnabled = false });
            if (history.Count == 0) history.Add(new SourceItem { Kind = SourceItemKind.Placeholder, Name = "ありません", IsEnabled = false });
            Groups.ReplaceAll(new[] { new SourceGroup("お気に入り", favorites), new SourceGroup("最近読んだ漫画", history) });
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    [RelayCommand]
    private async Task OpenItemAsync(SourceItem item)
    {
        if (item.Kind == SourceItemKind.Placeholder) return;
        try
        {
            Shell.Current.FlyoutIsPresented = false;
            if (await _sources.GetAsync(item.SourceKey) is null)
            {
                _toast.Show("読み込み元が見つかりません");
                return;
            }
            if (item.Kind == SourceItemKind.Favorite)
            {
                await Navigator.OpenFolderAsync(item.SourceKey, item.Path, fromTop: true);
            }
            else
            {
                await Navigator.OpenComicFromTopAsync(item.SourceKey, item.Path);
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    [RelayCommand]
    private async Task ItemMenuAsync(SourceItem item)
    {
        try
        {
            if (item.Kind == SourceItemKind.Favorite)
            {
                if (await Dialogs.ActionSheetAsync(item.Name, "削除") is null) return;
                if (!await Dialogs.ConfirmAsync($"{item.Name} をお気に入りから削除しますか？", "削除")) return;
                await _favorites.DeleteAsync(item.Id);
            }
            else if (item.Kind == SourceItemKind.History)
            {
                if (await Dialogs.ActionSheetAsync(item.Name, "履歴から削除") is null) return;
                await _readingStates.RemoveFromHistoryAsync(item.Id);
            }
            else
            {
                return;
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }
}
