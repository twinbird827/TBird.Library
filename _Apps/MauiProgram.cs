using LanobeReader.Services;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using LanobeReader.Services.Kakuyomu;
using LanobeReader.Services.Narou;
using LanobeReader.Services.Network;
using LanobeReader.ViewModels;
using LanobeReader.Views;
using Microsoft.Extensions.Logging;

namespace LanobeReader;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        // Database
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<NovelRepository>();
        builder.Services.AddSingleton<EpisodeRepository>();
        builder.Services.AddSingleton<EpisodeCacheRepository>();
        builder.Services.AddSingleton<AppSettingsRepository>();

        // HttpClient
        builder.Services.AddSingleton<HttpClient>();

        // Network / Background
        builder.Services.AddSingleton<NetworkPolicyService>();
        builder.Services.AddSingleton<BackgroundJobQueue>();
        builder.Services.AddSingleton<PrefetchService>();

        // API Services
        builder.Services.AddSingleton<NarouApiService>();
        builder.Services.AddSingleton<INovelService>(sp => sp.GetRequiredService<NarouApiService>());
        builder.Services.AddSingleton<KakuyomuApiService>();
        builder.Services.AddSingleton<INovelService>(sp => sp.GetRequiredService<KakuyomuApiService>());
        builder.Services.AddSingleton<INovelServiceFactory, NovelServiceFactory>();
        builder.Services.AddSingleton<UpdateCheckService>();
        builder.Services.AddSingleton<NotificationPermissionService>();

        // ViewModels
        builder.Services.AddTransient<NovelListViewModel>();
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<EpisodeListViewModel>();
        builder.Services.AddTransient<ReaderViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages
        builder.Services.AddTransient<NovelListPage>();
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<EpisodeListPage>();
        builder.Services.AddTransient<ReaderPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
