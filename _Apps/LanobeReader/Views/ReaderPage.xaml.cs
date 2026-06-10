using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class ReaderPage : ContentPage
{
    public ReaderPage(ReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.ScrollToTop = () => Dispatcher.Dispatch(async () =>
            await ContentScrollView.ScrollToAsync(0, 0, false));
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
            if (BindingContext is ReaderViewModel vm && vm.AutoMarkReadEnabled)
            {
                await vm.MarkAsReadFromAutoCommand.ExecuteAsync(null);
            }
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("lanobe://", StringComparison.OrdinalIgnoreCase) != true) return;
        e.Cancel = true;

        if (BindingContext is not ReaderViewModel vm) return;

        if (e.Url.Contains("read-end", StringComparison.OrdinalIgnoreCase))
        {
            if (vm.AutoMarkReadEnabled)
                await vm.MarkAsReadFromAutoCommand.ExecuteAsync(null);
        }
        else if (e.Url.Contains("next-episode", StringComparison.OrdinalIgnoreCase))
        {
            if (vm.NextEpisodeCommand.CanExecute(null))
                await vm.NextEpisodeCommand.ExecuteAsync(null);
        }
        else if (e.Url.Contains("prev-episode", StringComparison.OrdinalIgnoreCase))
        {
            if (vm.PrevEpisodeCommand.CanExecute(null))
                await vm.PrevEpisodeCommand.ExecuteAsync(null);
        }
    }
}
