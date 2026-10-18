using ComicViewer.Services;
using ComicViewer.Views;
using TBird.Core;

namespace ComicViewer;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) MessageService.Exception(ex);
            else MessageService.Error($"Unhandled exception: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            MessageService.Exception(args.Exception);
            args.SetObserved();
        };
        _ = Task.Run(LogCleanupService.Run);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        window.Stopped += async (_, _) =>
        {
            try
            {
                if (Shell.Current?.CurrentPage is ViewerPage page) await page.ViewModel.SaveNowAsync();
            }
            catch (Exception ex)
            {
                MessageService.Exception(ex);
            }
        };
        return window;
    }
}
