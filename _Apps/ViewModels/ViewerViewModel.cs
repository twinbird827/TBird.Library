using ComicViewer.Comics;
using ComicViewer.Data;
using ComicViewer.Logic;
using ComicViewer.Models;
using ComicViewer.Services;
using ComicViewer.Sources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TBird.Core;
using TBird.Maui.ViewModels;
using Bitmap = Android.Graphics.Bitmap;

namespace ComicViewer.ViewModels;

/// <summary>ビューアの横スライド表示。dir = +1 は次の単位（右開きのため画面は右へ流れる）。</summary>
public interface ISlideHost
{
    Task SlideAsync(int dir);

    Task CancelSlideAsync();

    void ResetSlide();
}

public partial class ViewerViewModel : ErrorAwareViewModel, IQueryAttributable
{
    private const int PrefetchRange = 2;
    private const int SmbInitialSizeRange = 4;

    private readonly FileSourceProvider _sources;
    private readonly ReadingStateRepository _readingStates;
    private readonly PdfCacheService _pdfCache;
    private readonly ComicSession _session;
    private readonly ToastService _toast;
    private readonly BrightnessService _brightness;

    private ComicRef? _comic;
    private IComicReader? _reader;
    private bool[] _wide = Array.Empty<bool>();
    private List<DisplayUnit> _units = new();
    private int _index;
    private int _screenWidth;
    private int _screenHeight;
    private readonly Dictionary<DisplayUnit, Bitmap> _bitmaps = new();
    private readonly HashSet<DisplayUnit> _failed = new();
    private CancellationTokenSource? _comicCts;
    private CancellationTokenSource? _prefetchCts;
    private CancellationTokenSource? _saveCts;
    private CancellationTokenSource? _downloadCts;
    private bool _busy;
    private bool _closed;
    private bool _initializingBrightness;

    public ViewerViewModel(FileSourceProvider sources, ReadingStateRepository readingStates,
        PdfCacheService pdfCache, ComicSession session, ToastService toast, BrightnessService brightness)
    {
        _sources = sources;
        _readingStates = readingStates;
        _pdfCache = pdfCache;
        _session = session;
        _toast = toast;
        _brightness = brightness;
        _initializingBrightness = true;
        Brightness = AppSettings.Brightness;
        _initializingBrightness = false;
    }

    public ISlideHost? SlideHost { get; set; }

    [ObservableProperty] public partial string ComicName { get; set; } = "";
    [ObservableProperty] public partial int PageCount { get; set; }
    [ObservableProperty] public partial int MaxPage { get; set; } = 1;
    [ObservableProperty] public partial double SliderValue { get; set; }
    [ObservableProperty] public partial string PageLabel { get; set; } = "";
    [ObservableProperty] public partial object? CurrentBitmap { get; set; }
    [ObservableProperty] public partial object? PrevBitmap { get; set; }
    [ObservableProperty] public partial object? NextBitmap { get; set; }
    [ObservableProperty] public partial bool IsOverlayVisible { get; set; }
    [ObservableProperty] public partial bool IsPageLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    public partial bool HasPageError { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelDownloadCommand))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty] public partial double DownloadProgress { get; set; }
    [ObservableProperty] public partial string DownloadText { get; set; } = "";
    [ObservableProperty] public partial double Brightness { get; set; }

    private bool HasPages => !IsDownloading && _units.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var comic = (ComicRef)query["comic"];
        var start = (int)query["start"];
        UpdateScreenSize();
        _ = OpenComicAsync(comic, start);
    }

    partial void OnSliderValueChanged(double value) => PageLabel = PageCount == 0 ? "" : $"{(int)Math.Round(value) + 1} / {PageCount}";

    partial void OnBrightnessChanged(double value)
    {
        if (!_initializingBrightness) _brightness.SetBrightness((float)value);
    }

    // ---- 漫画を開く ----

    /// <summary>start = -1 は最終単位。開けなければ、開いている漫画があれば留まり、無ければ一覧へ戻る。</summary>
    private async Task OpenComicAsync(ComicRef comic, int start)
    {
        if (_busy) return;
        _busy = true;
        IsPageLoading = true;
        var cts = new CancellationTokenSource();
        try
        {
            var source = await _sources.GetAsync(comic.SourceKey) ?? throw new DirectoryNotFoundException(comic.SourceKey);
            string? pdfPath = null;
            if (comic.Kind == ComicKind.Pdf && source.IsRemote)
            {
                pdfPath = await _pdfCache.FindAsync(comic) ?? await DownloadAsync(source, comic);
            }
            var reader = await ComicReaderFactory.OpenAsync(source, comic, pdfPath, cts.Token);
            if (_closed)
            {
                _ = Task.Run(reader.Dispose);
                return;
            }

            await SaveNowAsync();
            CloseComic();
            _comic = comic;
            _reader = reader;
            _comicCts = cts;
            ComicName = comic.Name;
            PageCount = reader.PageCount;
            MaxPage = Math.Max(1, reader.PageCount - 1);
            _wide = new bool[reader.PageCount];
            var startPage = start < 0 ? reader.PageCount - 1 : start < reader.PageCount ? start : 0;

            await TouchHistoryAsync(comic);
            if (source.IsRemote && comic.Kind != ComicKind.Pdf)
            {
                await LoadSizesAsync(Enumerable.Range(startPage - SmbInitialSizeRange, SmbInitialSizeRange * 2 + 1), cts.Token);
                _ = LoadRemainingSizesAsync(cts.Token);
            }
            else
            {
                await LoadSizesAsync(Enumerable.Range(0, reader.PageCount), cts.Token);
            }
            BuildUnits();
            _index = DisplayUnitBuilder.IndexOfPage(_units, startPage);
            ShowCurrent();
        }
        catch (OperationCanceledException)
        {
            cts.Dispose();
            if (_reader is null) await LeaveAsync();
        }
        catch (Exception ex)
        {
            cts.Dispose();
            MessageService.Exception(ex);
            await Dialogs.AlertAsync(GetOpenErrorMessage(ex));
            if (_reader is null) await LeaveAsync();
        }
        finally
        {
            IsDownloading = false;
            IsPageLoading = _units.Count > 0 && CurrentBitmap is null && !HasPageError;
            _busy = false;
        }
    }

    private static string GetOpenErrorMessage(Exception ex) => ex switch
    {
        Java.Lang.SecurityException => "パスワード付き PDF は開けません",
        DownloadException d when PdfCacheService.IsDiskFull(d.InnerException!) => "端末の空き容量が足りません",
        DownloadException => "ダウンロードに失敗しました",
        _ => "開けませんでした",
    };

    private sealed class DownloadException(Exception inner) : Exception(inner.Message, inner);

    private async Task<string> DownloadAsync(IFileSource source, ComicRef comic)
    {
        var cts = _downloadCts = new CancellationTokenSource();
        IsDownloading = true;
        DownloadProgress = 0;
        var totalMb = comic.Size / 1024.0 / 1024.0;
        DownloadText = $"0 / {totalMb:0.0} MB";
        var progress = new Progress<long>(n =>
        {
            DownloadProgress = comic.Size == 0 ? 0 : (double)n / comic.Size;
            DownloadText = $"{n / 1024.0 / 1024.0:0.0} / {totalMb:0.0} MB";
        });
        try
        {
            return await _pdfCache.DownloadAsync(source, comic, progress, cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DownloadException(ex);
        }
        finally
        {
            IsDownloading = false;
            _downloadCts = null;
        }
    }

    private async Task TouchHistoryAsync(ComicRef comic)
    {
        try
        {
            await _readingStates.TouchOpenedAsync(comic.SourceKey, comic.Path);
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    // ---- ページサイズと表示単位 ----

    private async Task LoadSizesAsync(IEnumerable<int> pages, CancellationToken ct)
    {
        var reader = _reader!;
        var targets = pages.Where(i => i >= 0 && i < reader.PageCount).ToList();
        await Task.Run(async () =>
        {
            foreach (var i in targets)
            {
                ct.ThrowIfCancellationRequested();
                _wide[i] = await GetIsWideAsync(reader, i, ct).ConfigureAwait(false);
            }
        }, ct);
    }

    private async Task LoadRemainingSizesAsync(CancellationToken ct)
    {
        var reader = _reader!;
        var wide = _wide;
        try
        {
            for (var i = 0; i < reader.PageCount; i++)
            {
                if (wide[i]) continue;
                var isWide = await Task.Run(() => GetIsWideAsync(reader, i, ct), ct);
                // 横長でなければ単位は変わらない（未取得は横長でない扱いのため）。
                if (!isWide || ct.IsCancellationRequested) continue;
                wide[i] = true;
                var current = _units[_index];
                var window = WindowUnits();
                BuildUnits();
                _index = DisplayUnitBuilder.IndexOfUnit(_units, current);
                if (!window.SequenceEqual(WindowUnits())) ShowCurrent();
                else UpdatePageIndicator();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    private static async Task<bool> GetIsWideAsync(IComicReader reader, int index, CancellationToken ct)
    {
        try
        {
            return (await reader.GetPageSizeAsync(index, ct).ConfigureAwait(false))?.IsWide ?? false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessageService.Warn($"Page size failed: {index} {ex.Message}");
            return false;
        }
    }

    private void BuildUnits() => _units = DisplayUnitBuilder.Build(_wide, _screenWidth > _screenHeight);

    private List<DisplayUnit> WindowUnits()
        => Enumerable.Range(_index - PrefetchRange, PrefetchRange * 2 + 1).Where(i => i >= 0 && i < _units.Count).Select(i => _units[i]).ToList();

    private void UpdateScreenSize()
    {
        var info = DeviceDisplay.Current.MainDisplayInfo;
        _screenWidth = (int)info.Width;
        _screenHeight = (int)info.Height;
    }

    /// <summary>画面回転。回転前の単位の代表元ページを含む単位へ移る。</summary>
    public void OnDisplayChanged()
    {
        UpdateScreenSize();
        if (_units.Count == 0) return;
        var page = _units[_index].Representative;
        BuildUnits();
        _index = DisplayUnitBuilder.IndexOfPage(_units, page);
        ClearBitmaps();
        ShowCurrent();
    }

    // ---- 表示と先読み ----

    private void ShowCurrent()
    {
        if (_units.Count == 0) return;
        var unit = _units[_index];
        CurrentBitmap = _bitmaps.GetValueOrDefault(unit);
        PrevBitmap = _index > 0 ? _bitmaps.GetValueOrDefault(_units[_index - 1]) : null;
        NextBitmap = _index + 1 < _units.Count ? _bitmaps.GetValueOrDefault(_units[_index + 1]) : null;
        HasPageError = _failed.Contains(unit);
        IsPageLoading = CurrentBitmap is null && !HasPageError;
        UpdatePageIndicator();

        // 表示中の Bitmap を差し替えた後なので、範囲外のものは解放してよい。
        var window = WindowUnits().ToHashSet();
        foreach (var key in _bitmaps.Keys.Where(x => !window.Contains(x)).ToList())
        {
            _bitmaps.Remove(key, out var bitmap);
            bitmap!.Recycle();
        }
        StartPrefetch();
        ScheduleSave();
    }

    private void UpdatePageIndicator()
    {
        SliderValue = _units[_index].Representative;
        OnSliderValueChanged(SliderValue);
    }

    private void StartPrefetch()
    {
        _prefetchCts?.Cancel();
        if (_reader is null || _comicCts is null) return;
        var cts = _prefetchCts = CancellationTokenSource.CreateLinkedTokenSource(_comicCts.Token);
        var reader = _reader;
        var order = new[] { 0, 1, -1, 2, -2 }
            .Select(d => _index + d)
            .Where(i => i >= 0 && i < _units.Count)
            .Select(i => _units[i])
            .Where(u => !_bitmaps.ContainsKey(u) && !_failed.Contains(u))
            .ToList();
        var (w, h) = (_screenWidth, _screenHeight);
        _ = Task.Run(async () =>
        {
            foreach (var unit in order)
            {
                if (cts.IsCancellationRequested) return;
                var bitmap = await RenderAsync(reader, unit, w, h, cts.Token).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() => OnRendered(reader, unit, bitmap));
            }
        });
    }

    private async Task<Bitmap?> RenderAsync(IComicReader reader, DisplayUnit unit, int w, int h, CancellationToken ct)
    {
        try
        {
            return await UnitRenderer.RenderAsync(reader, unit, w, h, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Java.Lang.OutOfMemoryError)
        {
            MessageService.Warn($"Out of memory: {unit}");
            await MainThread.InvokeOnMainThreadAsync(DropPrefetched);
            try
            {
                return await UnitRenderer.RenderAsync(reader, unit, w / 2, h / 2, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessageService.Exception(ex);
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
        if (!ct.IsCancellationRequested) await MainThread.InvokeOnMainThreadAsync(() => _failed.Add(unit));
        return null;
    }

    private void OnRendered(IComicReader reader, DisplayUnit unit, Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            if (_reader == reader && _units.Count > 0 && _units[_index] == unit && _failed.Contains(unit))
            {
                HasPageError = true;
                IsPageLoading = false;
            }
            return;
        }
        if (_reader != reader || _closed || !WindowUnits().Contains(unit) || _bitmaps.ContainsKey(unit))
        {
            bitmap.Recycle();
            return;
        }
        _bitmaps[unit] = bitmap;
        if (_units[_index] == unit)
        {
            CurrentBitmap = bitmap;
            IsPageLoading = false;
        }
        else if (_index > 0 && _units[_index - 1] == unit)
        {
            PrevBitmap = bitmap;
        }
        else if (_index + 1 < _units.Count && _units[_index + 1] == unit)
        {
            NextBitmap = bitmap;
        }
    }

    /// <summary>メモリ不足時に、表示中の 3 単位以外の Bitmap を捨てる。</summary>
    private void DropPrefetched()
    {
        var visible = new[] { CurrentBitmap, PrevBitmap, NextBitmap };
        foreach (var key in _bitmaps.Where(x => !visible.Contains(x.Value)).Select(x => x.Key).ToList())
        {
            _bitmaps.Remove(key, out var bitmap);
            bitmap!.Recycle();
        }
    }

    private void ClearBitmaps()
    {
        CurrentBitmap = PrevBitmap = NextBitmap = null;
        foreach (var bitmap in _bitmaps.Values) bitmap.Recycle();
        _bitmaps.Clear();
        _failed.Clear();
    }

    // ---- 操作 ----

    public async Task OnTapAsync(Point point, Size size)
    {
        if (IsDownloading || _busy) return;
        if (IsOverlayVisible)
        {
            HideOverlay();
            return;
        }
        switch (TapZoneResolver.Resolve(point.X, point.Y, size.Width, size.Height))
        {
            case TapAction.ShowOverlay: ShowOverlay(); break;
            case TapAction.NextUnit: await NextAsync(); break;
            case TapAction.PrevUnit: await PrevAsync(); break;
            case TapAction.FirstUnit: First(); break;
            case TapAction.LastUnit: Last(); break;
            case TapAction.NextComic: await NextComicAsync(); break;
            case TapAction.PrevComic: await PrevComicAsync(); break;
        }
    }

    public bool CanSwipe => !IsOverlayVisible && !IsDownloading && !_busy && _units.Count > 0;

    /// <summary>スワイプ確定。dir = +1 は次の単位。端を越えるなら元位置へ戻して前後の漫画へ。</summary>
    public async Task CommitSwipeAsync(int dir)
    {
        var target = _index + dir;
        if (target < 0 || target >= _units.Count)
        {
            if (SlideHost is not null) await SlideHost.CancelSlideAsync();
            if (dir > 0) await NextComicAsync();
            else await PrevComicAsync();
            return;
        }
        await SlideToAsync(dir);
    }

    private async Task SlideToAsync(int dir)
    {
        _busy = true;
        try
        {
            if (SlideHost is not null) await SlideHost.SlideAsync(dir);
            _index += dir;
            ShowCurrent();
        }
        finally
        {
            SlideHost?.ResetSlide();
            _busy = false;
        }
    }

    private void ShowOverlay()
    {
        IsOverlayVisible = true;
        OverlayChanged?.Invoke(true);
    }

    private void HideOverlay()
    {
        IsOverlayVisible = false;
        OverlayChanged?.Invoke(false);
    }

    /// <summary>オーバーレイの表示切替（ページがシステムバーを出し入れする）。</summary>
    public event Action<bool>? OverlayChanged;

    private async Task NextAsync()
    {
        if (!HasPages) return;
        if (_index + 1 >= _units.Count) await NextComicAsync();
        else await SlideToAsync(+1);
    }

    private async Task PrevAsync()
    {
        if (!HasPages) return;
        if (_index == 0) await PrevComicAsync();
        else await SlideToAsync(-1);
    }

    [RelayCommand]
    private void First() => JumpTo(0);

    [RelayCommand]
    private void Last() => JumpTo(_units.Count - 1);

    [RelayCommand]
    private void Seek()
    {
        if (_units.Count == 0) return;
        JumpTo(DisplayUnitBuilder.IndexOfPage(_units, (int)Math.Round(SliderValue)));
    }

    private void JumpTo(int index)
    {
        if (_units.Count == 0 || index == _index) return;
        _index = index;
        ShowCurrent();
    }

    [RelayCommand]
    private Task NextComicAsync() => MoveComicAsync(+1);

    [RelayCommand]
    private Task PrevComicAsync() => MoveComicAsync(-1);

    private async Task MoveComicAsync(int dir)
    {
        if (_comic is null || IsDownloading || _busy) return;
        var siblings = _session.Siblings;
        var i = siblings.ToList().FindIndex(x => x.Path == _comic.Path);
        var target = i < 0 ? -1 : i + dir;
        if (target < 0 || target >= siblings.Count)
        {
            _toast.Show(dir > 0 ? "最後の漫画です" : "最初の漫画です");
            return;
        }
        await OpenComicAsync(siblings[target], dir > 0 ? 0 : -1);
    }

    [RelayCommand(CanExecute = nameof(HasPageError))]
    private void Reload()
    {
        if (_units.Count == 0) return;
        _failed.Remove(_units[_index]);
        ShowCurrent();
    }

    [RelayCommand(CanExecute = nameof(IsDownloading))]
    private void CancelDownload() => _downloadCts?.Cancel();

    /// <summary>戻るキー。DL 中はキャンセル扱い（OpenComicAsync が一覧へ戻す）。</summary>
    public async Task BackAsync()
    {
        if (IsDownloading)
        {
            _downloadCts?.Cancel();
            return;
        }
        await LeaveAsync();
    }

    private async Task LeaveAsync()
    {
        if (_closed) return;
        await SaveNowAsync();
        Close();
        await Navigator.BackAsync();
    }

    // ---- 位置の保存 ----

    private void ScheduleSave()
    {
        _saveCts?.Cancel();
        if (_units.Count > 0 && _index == _units.Count - 1)
        {
            // 最終単位は既読にするため待たずに保存する。
            _ = SaveNowAsync();
            return;
        }
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromSeconds(1), cts.Token).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => _ = SaveNowAsync()),
            cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    public async Task SaveNowAsync()
    {
        _saveCts?.Cancel();
        if (_comic is null || _units.Count == 0) return;
        var comic = _comic;
        var page = _units[_index].Representative;
        var count = PageCount;
        var isLast = _index == _units.Count - 1;
        try
        {
            await _readingStates.SaveProgressAsync(comic.SourceKey, comic.Path, page, count, isLast);
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    private void CloseComic()
    {
        _prefetchCts?.Cancel();
        _comicCts?.Cancel();
        _comicCts = null;
        ClearBitmaps();
        _units = new List<DisplayUnit>();
        _index = 0;
        var reader = _reader;
        _reader = null;
        // SMB の zip はストリームを閉じるときに通信するため UI スレッドで破棄しない。
        if (reader is not null) _ = Task.Run(reader.Dispose);
    }

    public void Close()
    {
        _closed = true;
        _downloadCts?.Cancel();
        CloseComic();
    }
}
