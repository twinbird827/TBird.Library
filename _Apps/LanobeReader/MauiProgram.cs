using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Platforms.Android;
using LanobeReader.Services;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using LanobeReader.Services.Kakuyomu;
using LanobeReader.Services.Narou;
using LanobeReader.Services.Network;
using LanobeReader.ViewModels;
using LanobeReader.Views;
using Microsoft.Extensions.Logging;
using TBird.Core;
using TBird.Maui;
using TBird.Maui.Background;

namespace LanobeReader;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // MessageService は MauiAppBuilder 生成より前に差し替える（DI 構築中のログも正しく
        // [LanobeReader] プレフィックスで logcat / AppDataDirectory/log に出力するため）。
        MessageService.SetService(new MauiMessageService("LanobeReader"));

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        // OpenSans*.ttf は Resources/Fonts に存在せず、XAML からも参照していないため AddFont を削除。
        // 縦書き WebView は Reader 側 CSS で font-family:serif を直指定しているため影響なし。

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
        // スクレイピング系 UA を共有 HttpClient に集約する(各 ApiService から USER_AGENT を撤去し、
        // 「SiteRateLimiter 側の HttpClient に集約」とした際の唯一の設定点)。未設定だと既定 UA となり、
        // Kakuyomu/Narou 側で弾かれうる + requirements の UA 要件に反するため、ここで明示付与する。
        builder.Services.AddSingleton<HttpClient>(_ =>
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; LanobeReader/1.0)");
            return http;
        });

        // Network policy: 抽象 + SiteRateLimiter + 既存 NetworkPolicyService ラッパー
        builder.Services.AddSingleton<INetworkPolicy, MauiNetworkPolicy>();
        builder.Services.AddSingleton(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            var settings = sp.GetRequiredService<AppSettingsRepository>();
            return new SiteRateLimiter(
                httpClient: http,
                // SiteTypeExtension.GetApiKey() を唯一のソースオブトゥルースとし、enum 値全列挙で構築。
                // リテラル "narou" / "kakuyomu" のハードコードは禁止。
                siteKeys: Enum.GetValues<SiteType>().Select(s => s.GetApiKey()).ToArray(),
                getDelayMs: async () =>
                {
                    var v = await settings.GetIntValueAsync(
                        SettingsKeys.REQUEST_DELAY_MS,
                        SettingsKeys.DEFAULT_REQUEST_DELAY_MS);
                    return Math.Clamp(v,
                        SettingsKeys.MIN_REQUEST_DELAY_MS,
                        SettingsKeys.MAX_REQUEST_DELAY_MS);
                });
            // maxAttempts / retryDelayMs は既定値（3 / 500）を採用
        });
        // NetworkPolicyService は INetworkPolicy + SiteRateLimiter を組み合わせる薄いラッパー（具象登録）。
        // 既存消費者（NarouApiService / KakuyomuApiService）の DI シグネチャは変えない。
        builder.Services.AddSingleton<NetworkPolicyService>();

        // Background
        builder.Services.AddSingleton<BackgroundJobQueue>();
        builder.Services.AddSingleton<PrefetchService>();

        // API Services
        builder.Services.AddSingleton<NarouApiService>();
        builder.Services.AddSingleton<INovelService>(sp => sp.GetRequiredService<NarouApiService>());
        builder.Services.AddSingleton<KakuyomuApiService>();
        builder.Services.AddSingleton<INovelService>(sp => sp.GetRequiredService<KakuyomuApiService>());
        builder.Services.AddSingleton<INovelServiceFactory, NovelServiceFactory>();
        builder.Services.AddSingleton<UpdateCheckService>();

        // 新着通知 / 定期スケジュールの Android 実装。フォアグラウンド(App.xaml.cs)・
        // バックグラウンド(UpdateCheckWorker)・設定変更(SettingsViewModel)から共通利用する。
        builder.Services.AddSingleton<IUpdateNotificationService, UpdateNotificationService>();
        builder.Services.AddSingleton<IUpdateScheduler, AndroidUpdateScheduler>();

        // NotificationPermissionService<PostNotificationsPermission>: コンストラクタ引数 4 つを
        // ファクトリで渡す。タイトル / 本文 / ボタン文言は現行 LanobeReader のローカライズ済テキスト。
        builder.Services.AddSingleton(sp =>
            new NotificationPermissionService<PostNotificationsPermission>(
                title: "通知の許可",
                message: "小説の更新をお知らせするために通知権限が必要です。許可しますか？",
                acceptLabel: "許可する",
                declineLabel: "後で"));

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
