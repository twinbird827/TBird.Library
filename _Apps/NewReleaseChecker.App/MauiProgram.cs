using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using NewReleaseChecker.App.Platforms.Android;
using NewReleaseChecker.App.Services;
using NewReleaseChecker.App.ViewModels;
using NewReleaseChecker.App.Views;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Services;
using NewReleaseChecker.Data.Api;
using NewReleaseChecker.Data.Database;
using NewReleaseChecker.Data.Preferences;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;
using TBird.Core;
using TBird.Maui;
using TBird.Maui.Background;

namespace NewReleaseChecker.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // ロギング基盤の初期化（静的ファサードの差し替え。要件 §6.6 / CLAUDE.md §5）
        MessageService.SetService(new MauiMessageService("NewReleaseChecker"));

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLocalNotification();

        var s = builder.Services;

        // --- 秘密情報 ---
        s.AddSingleton<ISecretsProvider, Secrets>();

        // --- HTTP / レート制限（1 リクエストごとに 1 秒以上。要件 §6.1 / §7.3） ---
        // 中継サーバー（NewReleaseChecker.Relay）専用 HttpClient。共有シークレットを X-Relay-Auth
        // デフォルトヘッダで全リクエストに付与する。タイムアウトは中継→楽天の上流(15秒)＋余裕で 20 秒。
        s.AddSingleton(sp =>
        {
            var secrets = sp.GetRequiredService<ISecretsProvider>();
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.Add("X-Relay-Auth", secrets.RelayServerApiKey);
            return http;
        });
        s.AddSingleton(sp => new SiteRateLimiter(
            sp.GetRequiredService<HttpClient>(),
            new[] { RakutenKoboApiClient.SiteKey },
            () => Task.FromResult(1000)));
        s.AddSingleton<INetworkPolicy, MauiNetworkPolicy>();

        // --- データ層 ---
        s.AddSingleton(_ => new NewReleaseDatabase(NewReleaseDatabase.DefaultPath));
        s.AddSingleton<ISeriesRepository, SeriesRepository>();
        s.AddSingleton<IBookRepository, BookRepository>();
        s.AddSingleton<IRakutenApiClient, RakutenKoboApiClient>();
        s.AddSingleton<IPreferencesService, PreferencesService>();

        // --- プラットフォーム実装（インターフェース越し） ---
        s.AddSingleton<ILocalNotifier, PluginLocalNotifier>();
        s.AddSingleton<IUserNotifier, CommunityToolkitUserNotifier>();
        s.AddSingleton<ICalendarService, AndroidCalendarService>();
        s.AddSingleton<IWorkScheduler>(_ => new AndroidWorkScheduler(global::Android.App.Application.Context));
        s.AddSingleton(_ => new NotificationPermissionService<PostNotificationsPermission>(
            "通知の許可", "新刊を通知でお知らせするために通知を許可してください。", "許可する", "あとで"));

        // --- ドメインサービス（共通チェック） ---
        s.AddSingleton<NewReleaseCheckService>();
        // 非永続巻の SeriesId=NULL 永続化を集約（巻詳細・一括操作で共有。F-015）。
        s.AddSingleton<BookActionService>();

        // --- ViewModels ---
        s.AddSingleton<SeriesListViewModel>();
        s.AddTransient<SeriesSearchViewModel>();
        s.AddTransient<SeriesDetailViewModel>();
        s.AddTransient<BookDetailViewModel>();
        s.AddSingleton<FavoritesViewModel>();
        s.AddSingleton<UpcomingViewModel>();
        s.AddSingleton<RankingViewModel>();
        s.AddSingleton<SettingsViewModel>();
        s.AddTransient<ExcludeKeywordsViewModel>();

        // --- Views ---
        s.AddSingleton<SeriesListPage>();
        s.AddTransient<SeriesSearchPage>();
        s.AddTransient<SeriesDetailPage>();
        s.AddTransient<BookDetailPage>();
        s.AddSingleton<FavoritesPage>();
        s.AddSingleton<UpcomingPage>();
        s.AddSingleton<RankingPage>();
        s.AddSingleton<SettingsPage>();
        s.AddTransient<ExcludeKeywordsPage>();

        var app = builder.Build();

        // 通知タップ → 該当シリーズ詳細へ遷移（要件 F-005）
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationTapped;

        return app;
    }

    private static void OnNotificationTapped(NotificationActionEventArgs e)
    {
        var data = e.Request?.ReturningData;
        if (string.IsNullOrEmpty(data) || !int.TryParse(data, out var seriesId)) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync($"{Routes.SeriesDetail}?seriesId={seriesId}");
            }
            catch (Exception ex)
            {
                MessageService.Exception(ex);
            }
        });
    }
}
