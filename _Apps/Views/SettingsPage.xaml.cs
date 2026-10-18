using ComicViewer.ViewModels;

namespace ComicViewer.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
    }

    public SettingsViewModel ViewModel { get; }

    private void OnCacheLimitDone(object? sender, EventArgs e) => ViewModel.ApplyCacheLimitCommand.Execute(null);
}
