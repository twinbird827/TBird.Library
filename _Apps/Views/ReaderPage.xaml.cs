using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class ReaderPage : ContentPage
{
    private readonly ReaderViewModel _viewModel;

    public ReaderPage(ReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReaderViewModel.EpisodeHtml) or nameof(ReaderViewModel.IsVerticalWriting))
        {
            if (_viewModel.IsVerticalWriting && !string.IsNullOrEmpty(_viewModel.EpisodeHtml))
            {
                VerticalWebView.Source = new HtmlWebViewSource { Html = _viewModel.EpisodeHtml };
            }
        }
    }

    private async void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView) return;

        if (scrollView.ScrollY + scrollView.Height >= scrollView.ContentSize.Height - 10)
        {
            await _viewModel.MarkAsReadCommand.ExecuteAsync(null);
        }
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url?.StartsWith("lanobe://read-end", StringComparison.OrdinalIgnoreCase) == true)
        {
            e.Cancel = true;
            await _viewModel.MarkAsReadCommand.ExecuteAsync(null);
        }
    }
}
