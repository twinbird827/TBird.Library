using LanobeReader.Helpers;
using LanobeReader.Services;
using LanobeReader.Services.Database;

namespace LanobeReader;

public partial class App : Application
{
    private readonly DatabaseService _dbService;
    private readonly AppSettingsRepository _settingsRepo;
    private readonly EpisodeCacheRepository _cacheRepo;
    private readonly NovelRepository _novelRepo;
    private readonly UpdateCheckService _updateCheckService;

    public App(
        DatabaseService dbService,
        AppSettingsRepository settingsRepo,
        EpisodeCacheRepository cacheRepo,
        NovelRepository novelRepo,
        UpdateCheckService updateCheckService)
    {
        InitializeComponent();

        _dbService = dbService;
        _settingsRepo = settingsRepo;
        _cacheRepo = cacheRepo;
        _novelRepo = novelRepo;
        _updateCheckService = updateCheckService;

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            LogHelper.Error("App", $"Unhandled exception: {args.ExceptionObject}");
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Created += async (s, e) =>
        {
            await InitializeAppAsync();
        };

        return window;
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            // 1. Initialize database
            await _dbService.InitializeAsync();

            // 2. Delete expired cache in background
            _ = Task.Run(async () =>
            {
                var cacheMonths = await _settingsRepo.GetIntValueAsync(SettingsKeys.CACHE_MONTHS, 3);
                await _cacheRepo.DeleteExpiredAsync(cacheMonths);
            });

            // 3. Check novel count for navigation
            var novelCount = await _novelRepo.CountAsync();
            if (novelCount == 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.GoToAsync("//search");
                });
            }
            else
            {
                // 4. Run update check in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _updateCheckService.CheckAllAsync();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Warn("App", $"Background update check failed: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error("App", $"InitializeAppAsync failed: {ex.Message}");
        }
    }
}
