using ComicViewer.Services;
using ComicViewer.ViewModels;
using ComicViewer.Views;

namespace ComicViewer;

public partial class AppShell : Shell
{
    public AppShell(FlyoutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;

        Routing.RegisterRoute(Navigator.Folder, typeof(FolderPage));
        Routing.RegisterRoute(Navigator.Viewer, typeof(ViewerPage));
        Routing.RegisterRoute(Navigator.SmbEdit, typeof(SmbEditPage));
        Routing.RegisterRoute(Navigator.Settings, typeof(SettingsPage));
    }

    public FlyoutViewModel ViewModel { get; }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlyoutIsPresented) && FlyoutIsPresented) ViewModel.LoadCommand.Execute(null);
    }
}
