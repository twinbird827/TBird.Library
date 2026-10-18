using ComicViewer.Data;
using ComicViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TBird.Core;
using TBird.Maui.ViewModels;

namespace ComicViewer.ViewModels;

public partial class SettingsViewModel : ErrorAwareViewModel
{
    private readonly PdfCacheService _pdfCache;
    private readonly ReadingStateRepository _readingStates;
    private readonly BrightnessService _brightness;
    private readonly ToastService _toast;

    public SettingsViewModel(PdfCacheService pdfCache, ReadingStateRepository readingStates, BrightnessService brightness, ToastService toast)
    {
        _pdfCache = pdfCache;
        _readingStates = readingStates;
        _brightness = brightness;
        _toast = toast;
        CacheLimitText = AppSettings.CacheLimitMb.ToString();
        BrightnessFollowSystem = AppSettings.BrightnessFollowSystem;
        _ = UpdateUsageAsync();
    }

    [ObservableProperty] public partial string CacheLimitText { get; set; }
    [ObservableProperty] public partial string CacheLimitError { get; set; } = "";
    [ObservableProperty] public partial string CacheUsageText { get; set; } = "";
    [ObservableProperty] public partial bool BrightnessFollowSystem { get; set; }

    partial void OnBrightnessFollowSystemChanged(bool value)
    {
        if (AppSettings.BrightnessFollowSystem == value) return;
        AppSettings.BrightnessFollowSystem = value;
        _brightness.Apply();
    }

    [RelayCommand]
    private async Task ApplyCacheLimitAsync()
    {
        if (!int.TryParse(CacheLimitText.Trim(), out var mb) || mb < AppSettings.MinCacheLimitMb || mb > AppSettings.MaxCacheLimitMb)
        {
            CacheLimitError = $"{AppSettings.MinCacheLimitMb}〜{AppSettings.MaxCacheLimitMb} の数値を入力してください";
            CacheLimitText = AppSettings.CacheLimitMb.ToString();
            return;
        }
        CacheLimitError = "";
        if (mb == AppSettings.CacheLimitMb) return;
        AppSettings.CacheLimitMb = mb;
        await _pdfCache.EvictAsync(null);
        await UpdateUsageAsync();
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        if (!await Dialogs.ConfirmAsync("サムネイルと PDF のキャッシュをすべて削除しますか？", "削除")) return;
        try
        {
            var ok = await Task.Run(_pdfCache.ClearAllAsync);
            _toast.Show(ok ? "削除しました" : "一部のファイルを削除できませんでした");
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            _toast.Show("一部のファイルを削除できませんでした");
        }
        await UpdateUsageAsync();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!await Dialogs.ConfirmAsync("履歴をすべて削除しますか？（既読・ページ位置は残ります）", "削除")) return;
        try
        {
            await _readingStates.ClearHistoryAsync();
            _toast.Show("削除しました");
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    private async Task UpdateUsageAsync()
    {
        try
        {
            var bytes = await Task.Run(PdfCacheService.GetUsageBytes);
            CacheUsageText = $"使用中: {bytes / 1024.0 / 1024.0:0.0} MB";
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }
}
