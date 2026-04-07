using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class ReaderPage : ContentPage
{
    private readonly ReaderViewModel _viewModel;

    public ReaderPage(ReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView) return;

        // Check if scrolled to bottom (within 10px tolerance)
        if (scrollView.ScrollY + scrollView.Height >= scrollView.ContentSize.Height - 10)
        {
            await _viewModel.MarkAsReadCommand.ExecuteAsync(null);
        }
    }
}
