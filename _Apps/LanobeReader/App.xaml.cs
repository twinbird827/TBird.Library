using LanobeReader.Helpers;
using LanobeReader.Services;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using TBird.Core;

namespace LanobeReader;

public partial class App : Application
{
    private readonly DatabaseService _dbService;
    private readonly AppSettingsRepository _settingsRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly NovelRepository _novelRepo;
    private readonly UpdateCheckService _updateCheckService;
    private readonly PrefetchService _prefetchService;
    private readonly IUpdateNotificationService _notifier;

    public App(
        DatabaseService dbService,
        AppSettingsRepository settingsRepo,
        EpisodeCacheRepository cacheRepo,
        NovelRepository novelRepo,
        UpdateCheckService updateCheckService,
        PrefetchService prefetchService,
        IUpdateNotificationService notifier)
    {
        InitializeComponent();

        _dbService = dbService;
        _settingsRepo = settingsRepo;
        _cacheRepo = cacheRepo;
        _novelRepo = novelRepo;
        _updateCheckService = updateCheckService;
        _prefetchService = prefetchService;
        _notifier = notifier;

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            MessageService.Error($"Unhandled exception: {args.ExceptionObject}");
        };

        // fire-and-forget Task の未観測例外を捕捉してプロセス終了を抑止する
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            MessageService.Error($"Unobserved task exception: {args.Exception}");
            args.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Created += (s, e) =>
        {
            // Fire-and-forget: DB初期化をバックグラウンドで実行しUIスレッドをブロックしない
            _ = Task.Run(InitializeAppAsync);
        };

        return window;
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            // 1. Initialize database (background thread)
            await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
            await _settingsRepo.LoadAllAsync().ConfigureAwait(false);

            // 2. Delete expired cache
            var cacheMonths = await _settingsRepo.GetIntValueAsync(SettingsKeys.CACHE_MONTHS, 3).ConfigureAwait(false);
            await _cacheRepo.DeleteExpiredAsync(cacheMonths).ConfigureAwait(false);

            // 3. Check novel count for navigation
            var novelCount = await _novelRepo.CountAsync().ConfigureAwait(false);
            if (novelCount == 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        if (Shell.Current is not null)
                            await Shell.Current.GoToAsync("//search");
                    }
                    catch (Exception ex)
                    {
                        MessageService.Warn($"Navigation to search failed: {ex.Message}");
                    }
                });
            }
            else
            {
                // 4. Run update check (fire-and-forget, already on background thread)
                _ = RunUpdateCheckAsync();

                // 5. Scan unread+uncached episodes and enqueue for prefetch
                _ = RunPrefetchAsync();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"InitializeAppAsync failed: {ex.Message}");
        }
    }

    private async Task RunUpdateCheckAsync()
    {
        try
        {
            // 起動時チェックは新着を DB へ取り込み、アプリ内一覧の NEW 表示を最新化する。
            // ShowUpdatesAsync は前面中（IsForeground）はシステム通知を抑止するため、通常の
            // コールドスタート（OnResume で前面確定済み）では通知は出ない＝ユーザは一覧で新着を見る。
            // 初期化中にアプリが背面化した稀なケースのみ、ここで通知が出る。
            // バックグラウンド検出時の確実な通知は WorkManager / アラーム経路が担う。
            var updates = await _updateCheckService.CheckAllAsync().ConfigureAwait(false);
            await _notifier.ShowUpdatesAsync(updates).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            MessageService.Warn($"Background update check failed: {ex.Message}");
        }
    }

    private async Task RunPrefetchAsync()
    {
        try
        {
            await _prefetchService.EnqueueAllUnreadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            MessageService.Warn($"Prefetch scan failed: {ex.Message}");
        }
    }
}
