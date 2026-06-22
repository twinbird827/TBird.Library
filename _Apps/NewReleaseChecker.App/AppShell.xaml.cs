using Microsoft.Extensions.DependencyInjection;
using NewReleaseChecker.App.Platforms.Android;
using NewReleaseChecker.App.Views;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Data.Database;
using TBird.Core;
using TBird.Maui;

namespace NewReleaseChecker.App;

public partial class AppShell : Shell
{
    private bool _startupDone;

    public AppShell()
    {
        InitializeComponent();

        // タブ内からのプッシュ遷移ルート（SCR-004/005/006/011）
        Routing.RegisterRoute(Routes.SeriesSearch, typeof(SeriesSearchPage));
        Routing.RegisterRoute(Routes.SeriesDetail, typeof(SeriesDetailPage));
        Routing.RegisterRoute(Routes.BookDetail, typeof(BookDetailPage));
        Routing.RegisterRoute(Routes.ExcludeKeywords, typeof(ExcludeKeywordsPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_startupDone) return;
        _startupDone = true;
        await RunStartupAsync();
    }

    /// <summary>起動時処理（要件 §6.2）。</summary>
    private async Task RunStartupAsync()
    {
        var services = IPlatformApplication.Current?.Services;
        if (services is null) return;

        try
        {
            // 1. DB 接続確認・テーブル未作成なら作成
            await services.GetRequiredService<NewReleaseDatabase>().EnsureInitializedAsync();

            // 2. 設定読込は各サービスが遅延ロードするため明示処理不要

            // 3. 通知権限の確認・リクエスト（初回）
            await services.GetRequiredService<NotificationPermissionService<PostNotificationsPermission>>()
                .EnsureRequestedAsync();

            // 4. WorkManager 定期タスク登録（auto_check_enabled が ON のときのみ）
            var prefs = services.GetRequiredService<IPreferencesService>();
            var scheduler = services.GetRequiredService<IWorkScheduler>();
            if (prefs.AutoCheckEnabled) scheduler.Schedule(prefs.AutoCheckInterval);
            else scheduler.Cancel();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            await DisplayAlertAsync("初期化エラー", "アプリの初期化に失敗しました。", "OK");
        }
    }
}

/// <summary>Shell ナビゲーションのルート名。</summary>
public static class Routes
{
    public const string SeriesSearch = "seriesSearch";
    public const string SeriesDetail = "seriesDetail";
    public const string BookDetail = "bookDetail";
    public const string ExcludeKeywords = "excludeKeywords";
}
