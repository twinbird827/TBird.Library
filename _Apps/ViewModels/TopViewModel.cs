using ComicViewer.Data;
using ComicViewer.Logic;
using ComicViewer.Services;
using ComicViewer.Sources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TBird.Core;
using TBird.Maui.Collections;
using TBird.Maui.ViewModels;

namespace ComicViewer.ViewModels;

public enum SourceItemKind
{
    Favorite,
    Local,
    Smb,
    History,
    Placeholder,
}

public class SourceItem
{
    public SourceItemKind Kind { get; init; }

    public string SourceKey { get; init; } = "";

    public string Path { get; init; } = "";

    public string Name { get; init; } = "";

    public string SubText { get; init; } = "";

    public bool IsEnabled { get; init; } = true;

    public int Id { get; init; }

    public ImageSource Icon { get; init; } = "folder.png";

    public string StatusText { get; init; } = "";

    public string KindBadge { get; init; } = "";

    public bool HasStatus => StatusText.Length > 0;

    public bool HasSubText => SubText.Length > 0;

    private (string Name, string Author)? _title;

    // 名前がファイル・フォルダ名の行だけ分解する（ストレージ・SMB の名前は利用者が付けた表示名のため）
    private (string Name, string Author) Title => _title ??= Kind is SourceItemKind.Favorite or SourceItemKind.History ? TitleParser.Parse(Name) : (Name, "");

    public string DisplayName => Title.Name;

    public string Author => Title.Author;

    public bool HasDetail => Author.Length > 0 || StatusText.Length > 0;
}

public class SourceGroup : List<SourceItem>
{
    public SourceGroup(string title, IEnumerable<SourceItem> items, bool canAdd = false) : base(items)
    {
        Title = title;
        CanAdd = canAdd;
    }

    public string Title { get; }

    /// <summary>SMB グループのヘッダに「＋」を出す。</summary>
    public bool CanAdd { get; }
}

public partial class TopViewModel : ErrorAwareViewModel
{
    private readonly FileSourceProvider _sources;
    private readonly SmbSourceRepository _smbSources;
    private readonly FavoriteRepository _favorites;
    private readonly StoragePermissionService _permission;
    private readonly SmbConnectionPool _pool;
    private readonly PdfCacheService _pdfCache;
    private readonly ToastService _toast;

    public TopViewModel(FileSourceProvider sources, SmbSourceRepository smbSources, FavoriteRepository favorites,
        StoragePermissionService permission, SmbConnectionPool pool, PdfCacheService pdfCache, ToastService toast)
    {
        _sources = sources;
        _smbSources = smbSources;
        _favorites = favorites;
        _permission = permission;
        _pool = pool;
        _pdfCache = pdfCache;
        _toast = toast;
    }

    public BulkObservableCollection<SourceGroup> Groups { get; } = new();

    [ObservableProperty]
    public partial bool HasStoragePermission { get; set; }

    /// <summary>S03 を表示したまま終わっていれば、その漫画を開き直す。読み込み元・権限が無ければ何もしない。</summary>
    public async Task RestoreAsync()
    {
        try
        {
            var sourceKey = AppSettings.ViewingSourceKey;
            var path = AppSettings.ViewingPath;
            if (sourceKey.Length == 0 || path.Length == 0) return;
            var source = await _sources.GetAsync(sourceKey);
            if (source is null || (!source.IsRemote && !_permission.IsGranted)) return;
            await Navigator.OpenComicFromTopAsync(sourceKey, path);
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            ClearError();
            HasStoragePermission = _permission.IsGranted;
            var favorites = (await _favorites.GetAllAsync()).Select(x => new SourceItem
            {
                Kind = SourceItemKind.Favorite,
                Id = x.Id,
                SourceKey = x.SourceKey,
                Path = x.Path,
                Name = x.Name,
            }).ToList();
            if (favorites.Count == 0) favorites.Add(Placeholder("お気に入りはありません"));

            var locals = _sources.GetLocalVolumes().Select(x => new SourceItem
            {
                Kind = SourceItemKind.Local,
                SourceKey = x.SourceKey,
                Name = x.DisplayName,
                IsEnabled = HasStoragePermission,
                SubText = HasStoragePermission ? "" : "権限なし（タップで設定）",
                Icon = "storage.png",
            });

            var smbs = (await _smbSources.GetAllAsync()).Select(x => new SourceItem
            {
                Kind = SourceItemKind.Smb,
                Id = x.Id,
                SourceKey = x.SourceKey,
                Name = x.Name,
                SubText = $@"\\{x.Host}\{x.Share}",
                Icon = "network.png",
            });

            Groups.ReplaceAll(new[]
            {
                new SourceGroup("お気に入り", favorites),
                new SourceGroup("ストレージ", locals),
                new SourceGroup("SMB", smbs, canAdd: true),
            });

            if (!HasStoragePermission && !_permission.HasAsked)
            {
                _permission.HasAsked = true;
                if (Shell.Current is not null && await Shell.Current.DisplayAlertAsync("ストレージへのアクセス",
                        "端末内と SD カードの漫画を読むには「すべてのファイルへのアクセス」の許可が必要です。", "設定を開く", "あとで"))
                {
                    _permission.OpenSettings();
                }
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            SetError("データを読み込めませんでした");
        }
    }

    private static SourceItem Placeholder(string text) => new() { Kind = SourceItemKind.Placeholder, Name = text, IsEnabled = false };

    [RelayCommand]
    private async Task OpenItemAsync(SourceItem item)
    {
        try
        {
            switch (item.Kind)
            {
                case SourceItemKind.Local when !item.IsEnabled:
                    _permission.OpenSettings();
                    break;
                case SourceItemKind.Local:
                case SourceItemKind.Smb:
                    await Navigator.OpenFolderAsync(item.SourceKey, "");
                    break;
                case SourceItemKind.Favorite:
                    if (await _sources.GetAsync(item.SourceKey) is null) _toast.Show("読み込み元が見つかりません");
                    else await Navigator.OpenFolderAsync(item.SourceKey, item.Path);
                    break;
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
            switch (item.Kind)
            {
                case SourceItemKind.Favorite:
                    if (await Dialogs.ActionSheetAsync(item.Name, "削除") is null) return;
                    if (!await Dialogs.ConfirmAsync($"{item.Name} をお気に入りから削除しますか？", "削除")) return;
                    await _favorites.DeleteAsync(item.Id);
                    await LoadAsync();
                    break;
                case SourceItemKind.Smb:
                    var action = await Dialogs.ActionSheetAsync(item.Name, "編集", "削除");
                    if (action == "編集")
                    {
                        await Navigator.OpenSmbEditAsync(item.Id);
                    }
                    else if (action == "削除" && await Dialogs.ConfirmAsync($"{item.Name} を削除しますか？", "削除"))
                    {
                        await DeleteSmbAsync(item);
                        await LoadAsync();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    private async Task DeleteSmbAsync(SourceItem item)
    {
        var row = await _smbSources.GetAsync(item.Id);
        if (row is null) return;
        _pool.Remove(row.SourceKey);
        await _pdfCache.DeleteFilesAsync(row.SourceKey);
        await _smbSources.DeleteAsync(row);
        try
        {
            SecureStorage.Remove(SmbConnectionPool.PasswordKey(row.Id));
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    [RelayCommand]
    private Task AddSmbAsync() => Navigator.OpenSmbEditAsync(null);

    [RelayCommand]
    private Task OpenSettingsAsync() => Shell.Current.GoToAsync(Navigator.Settings);
}
