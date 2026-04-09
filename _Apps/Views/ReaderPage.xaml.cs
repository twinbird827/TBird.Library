using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class ReaderPage : ContentPage
{
    public ReaderPage(ReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ReaderViewModel vm)
        {
            _ = vm.ReloadSettingsAsync();
        }
    }

    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView) return;

        if (scrollView.ScrollY + scrollView.Height >= scrollView.ContentSize.Height - 10)
        {
            if (BindingContext is ReaderViewModel vm)
            {
                await vm.MarkAsReadCommand.ExecuteAsync(null);
            }
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("lanobe://read-end", StringComparison.OrdinalIgnoreCase) == true)
        {
            e.Cancel = true;
            if (BindingContext is ReaderViewModel vm)
            {
                await vm.MarkAsReadCommand.ExecuteAsync(null);
            }
        }
    }
}
