using LanobeReader.Views;

namespace LanobeReader;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for non-tab pages
        Routing.RegisterRoute("episodes", typeof(EpisodeListPage));
        Routing.RegisterRoute("reader", typeof(ReaderPage));
    }
}
