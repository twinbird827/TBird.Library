using System.Text;
using ComicViewer.Data;
using ComicViewer.Platforms.Android;
using ComicViewer.Services;
using ComicViewer.Sources;
using ComicViewer.ViewModels;
using ComicViewer.Views;
using ComicViewer.Views.Controls;
using TBird.Core;
using TBird.Maui;

namespace ComicViewer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // zip の CP932 エントリ名を読むため。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // DI 構築中のログも [ComicViewer] で出すため、MauiAppBuilder 生成より前に差し替える。
        MessageService.SetService(new MauiMessageService("ComicViewer"));

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<PageImageView, PageImageViewHandler>());

        builder.Services.AddSingleton<ComicViewerDatabase>();
        builder.Services.AddSingleton<SmbSourceRepository>();
        builder.Services.AddSingleton<FavoriteRepository>();
        builder.Services.AddSingleton<ReadingStateRepository>();
        builder.Services.AddSingleton<FolderKindRepository>();
        builder.Services.AddSingleton<PdfCacheRepository>();

        builder.Services.AddSingleton<SmbConnectionPool>();
        builder.Services.AddSingleton<FileSourceProvider>();
        builder.Services.AddSingleton<ThumbnailService>();
        builder.Services.AddSingleton<PdfCacheService>();
        builder.Services.AddSingleton<StoragePermissionService>();
        builder.Services.AddSingleton<BrightnessService>();
        builder.Services.AddSingleton<SystemBarsService>();
        builder.Services.AddSingleton<ToastService>();
        builder.Services.AddSingleton<ComicSession>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<FlyoutViewModel>();
        builder.Services.AddTransient<TopViewModel>();
        builder.Services.AddTransient<FolderViewModel>();
        builder.Services.AddTransient<ViewerViewModel>();
        builder.Services.AddTransient<SmbEditViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<TopPage>();
        builder.Services.AddTransient<FolderPage>();
        builder.Services.AddTransient<ViewerPage>();
        builder.Services.AddTransient<SmbEditPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
