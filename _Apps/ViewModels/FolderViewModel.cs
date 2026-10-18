using ComicViewer.Comics;
using ComicViewer.Data;
using ComicViewer.Logic;
using ComicViewer.Models;
using ComicViewer.Services;
using ComicViewer.Sources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TBird.Core;
using TBird.Maui.Collections;
using TBird.Maui.ViewModels;

namespace ComicViewer.ViewModels;

public partial class FolderViewModel : ErrorAwareViewModel, IQueryAttributable
{
    private readonly FileSourceProvider _sources;
    private readonly FolderKindRepository _folderKinds;
    private readonly ReadingStateRepository _readingStates;
    private readonly FavoriteRepository _favorites;
    private readonly ThumbnailService _thumbnails;
    private readonly ComicSession _session;
    private readonly ToastService _toast;

    private IFileSource? _source;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _thumbnailCts;
    private string? _openAfterLoad;
    private bool _loaded;

    public FolderViewModel(FileSourceProvider sources, FolderKindRepository folderKinds, ReadingStateRepository readingStates,
        FavoriteRepository favorites, ThumbnailService thumbnails, ComicSession session, ToastService toast)
    {
        _sources = sources;
        _folderKinds = folderKinds;
        _readingStates = readingStates;
        _favorites = favorites;
        _thumbnails = thumbnails;
        _session = session;
        _toast = toast;
    }

    public string SourceKey { get; private set; } = "";

    public string Path { get; private set; } = "";

    public BulkObservableCollection<FolderItemViewModel> Items { get; } = new();

    /// <summary>一覧で表示中の先頭行。サムネイル生成の優先順に使う。</summary>
    public int FirstVisibleIndex { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(RefreshCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        SourceKey = (string)query["source"];
        Path = (string)query["path"];
        _openAfterLoad = query.TryGetValue("open", out var open) ? (string)open : null;
        LoadCommand.Execute(null);
    }

    private bool CanLoad() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private Task LoadAsync() => LoadCoreAsync(refresh: false);

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private Task RefreshAsync() => LoadCoreAsync(refresh: true);

    [RelayCommand]
    private Task RetryAsync() => LoadCoreAsync(refresh: false);

    private async Task LoadCoreAsync(bool refresh)
    {
        _loadCts?.Cancel();
        StopThumbnails();
        var cts = _loadCts = new CancellationTokenSource();
        var ct = cts.Token;
        IsBusy = true;
        ClearError();
        IsEmpty = false;
        try
        {
            _source = await _sources.GetAsync(SourceKey);
            if (_source is null)
            {
                _toast.Show("読み込み元が見つかりません");
                await Navigator.BackAsync();
                return;
            }
            Title = Path.Length == 0 ? _source.DisplayName : EntryRules.Name(Path);
            if (refresh) await _folderKinds.DeleteUnderAsync(SourceKey, Path);

            var items = await Task.Run(() => BuildItemsAsync(_source, ct), ct);
            ct.ThrowIfCancellationRequested();
            Items.ReplaceAll(items);
            IsEmpty = items.Count == 0;
            _session.Siblings = items.Where(x => x.Comic is not null).Select(x => x.Comic!).ToList();
            _loaded = true;
            StartThumbnails();

            if (_openAfterLoad is { } open)
            {
                _openAfterLoad = null;
                var target = items.FirstOrDefault(x => x.Comic?.Path == open);
                if (target is null) await Dialogs.AlertAsync("開けませんでした");
                else await OpenItemAsync(target);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            Items.ReplaceAll(Array.Empty<FolderItemViewModel>());
            await HandleLoadErrorAsync(ex);
        }
        finally
        {
            if (_loadCts == cts)
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }
    }

    private async Task HandleLoadErrorAsync(Exception ex)
    {
        switch (ex)
        {
            case SmbException { Kind: SmbErrorKind.Auth }:
                await Dialogs.AlertAsync("ユーザー名またはパスワードが違います");
                if (int.TryParse(SourceKey.AsSpan("smb:".Length), out var id)) await Navigator.OpenSmbEditAsync(id);
                break;
            case DirectoryNotFoundException:
            case SmbException { Kind: SmbErrorKind.ShareNotFound }:
                SetError("フォルダが見つかりません");
                break;
            case UnauthorizedAccessException:
                SetError("ストレージへのアクセス権限がありません");
                break;
            default:
                SetError(_source?.IsRemote == true ? "接続できませんでした" : "フォルダを読み込めませんでした");
                break;
        }
    }

    private async Task<List<FolderItemViewModel>> BuildItemsAsync(IFileSource source, CancellationToken ct)
    {
        var entries = (await source.ListAsync(Path, ct)).Where(x => !EntryRules.IsIgnored(x.Name)).ToList();
        var dirs = entries.Where(x => x.IsDirectory).ToList();
        var files = entries.Where(x => !x.IsDirectory).ToList();
        var directImages = files.Count(x => EntryRules.IsImage(x.Name));

        var cached = await _folderKinds.GetChildrenAsync(SourceKey, dirs.Select(x => EntryRules.Combine(Path, x.Name)));
        var folders = new List<FolderItemViewModel>();
        var comics = new List<FolderItemViewModel>();
        foreach (var dir in dirs)
        {
            ct.ThrowIfCancellationRequested();
            var path = EntryRules.Combine(Path, dir.Name);
            if (!cached.TryGetValue(path, out var kind))
            {
                try
                {
                    kind = await FolderClassifier.ClassifyAsync(source, path, ct);
                    await _folderKinds.SetAsync(SourceKey, path, kind);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    MessageService.Warn($"Classify failed: {path} {ex.Message}");
                    kind = FolderClassifier.Folder;
                }
            }
            if (kind == FolderClassifier.ImageComic)
            {
                comics.Add(ComicItem(new ComicRef(SourceKey, path, ComicKind.ImageFolder, dir.Name, 0, dir.ModifiedAt)));
            }
            else
            {
                folders.Add(new FolderItemViewModel(FolderItemKind.Folder, dir.Name, path, null));
            }
        }
        foreach (var file in files)
        {
            var kind = EntryRules.IsZip(file.Name) ? ComicKind.Zip : EntryRules.IsPdf(file.Name) ? ComicKind.Pdf : (ComicKind?)null;
            if (kind is { } k) comics.Add(ComicItem(new ComicRef(SourceKey, EntryRules.Combine(Path, file.Name), k, file.Name, file.Size, file.ModifiedAt)));
        }

        var result = folders.OrderBy(x => x.Name, NaturalComparer.Instance).ToList();
        // 当フォルダが一覧に出ている時点で「配下が画像だけ」ではないため、直下画像があれば仮想の 1 冊を作る。
        if (directImages > 0)
        {
            var modified = files.Where(x => EntryRules.IsImage(x.Name)).Max(x => x.ModifiedAt);
            result.Add(ComicItem(new ComicRef(SourceKey, EntryRules.Combine(Path, "*"), ComicKind.DirectImages,
                FolderItemViewModel.DirectImagesName, 0, modified)));
        }
        result.AddRange(comics.OrderBy(x => x.Name, NaturalComparer.Instance));

        var states = await _readingStates.GetBySourceAsync(SourceKey);
        foreach (var item in result.Where(x => x.Comic is not null))
        {
            item.StatusText = FolderItemViewModel.GetStatusText(states.GetValueOrDefault(item.Path));
            var thumb = ThumbnailService.GetPath(item.Comic!);
            if (File.Exists(thumb)) item.ThumbnailSource = ImageSource.FromFile(thumb);
        }
        return result;
    }

    private static FolderItemViewModel ComicItem(ComicRef comic) => new(FolderItemKind.Comic, comic.Name, comic.Path, comic);

    private void StartThumbnails()
    {
        StopThumbnails();
        if (_source is null) return;
        var cts = _thumbnailCts = new CancellationTokenSource();
        var items = Items.ToList();
        var targets = items.Select(x => x.ThumbnailSource is null ? x.Comic : null).ToList();
        _ = _thumbnails.RunAsync(_source, targets, () => FirstVisibleIndex, (index, file) =>
            MainThread.BeginInvokeOnMainThread(() => items[index].ThumbnailSource = ImageSource.FromFile(file)), cts.Token);
    }

    private void StopThumbnails()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts = null;
    }

    /// <summary>ビューアから戻ったときに状態表示を更新し、サムネイル生成を再開する。</summary>
    public async Task OnAppearingAsync()
    {
        if (!_loaded || IsBusy) return;
        try
        {
            var states = await _readingStates.GetBySourceAsync(SourceKey);
            foreach (var item in Items.Where(x => x.Comic is not null))
            {
                item.StatusText = FolderItemViewModel.GetStatusText(states.GetValueOrDefault(item.Path));
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
        StartThumbnails();
    }

    public void OnDisappearing() => StopThumbnails();

    [RelayCommand]
    private async Task OpenItemAsync(FolderItemViewModel item)
    {
        try
        {
            if (item.Comic is null)
            {
                Path = item.Path;
                await LoadCoreAsync(refresh: false);
                return;
            }
            var state = await _readingStates.GetAsync(SourceKey, item.Path);
            _session.Siblings = Items.Where(x => x.Comic is not null).Select(x => x.Comic!).ToList();
            await Navigator.OpenViewerAsync(item.Comic, FolderItemViewModel.GetStartPage(state));
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    [RelayCommand]
    private async Task ItemMenuAsync(FolderItemViewModel item)
    {
        try
        {
            if (item.Comic is null)
            {
                if (await Dialogs.ActionSheetAsync(item.Name, "お気に入りに追加") is null) return;
                var added = await _favorites.AddAsync(SourceKey, item.Path, item.Name);
                _toast.Show(added ? "お気に入りに追加しました" : "登録済みです");
                return;
            }
            var action = await Dialogs.ActionSheetAsync(item.Name, "既読にする", "未読に戻す");
            if (action is null) return;
            await _readingStates.SetReadAsync(SourceKey, item.Path, action == "既読にする");
            item.StatusText = FolderItemViewModel.GetStatusText(await _readingStates.GetAsync(SourceKey, item.Path));
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    /// <summary>戻るキー: 親フォルダへ。読み込み元ルートなら S01 へ。</summary>
    public async Task BackAsync()
    {
        if (Path.Length == 0)
        {
            _loadCts?.Cancel();
            StopThumbnails();
            await Navigator.BackAsync();
            return;
        }
        Path = EntryRules.Parent(Path);
        await LoadCoreAsync(refresh: false);
    }
}
