using System.Globalization;
using LanobeReader.ViewModels;
using TBird.Core;

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
        else if (e.Url.Contains("scroll", StringComparison.OrdinalIgnoreCase))
        {
            var i = e.Url.IndexOf("r=", StringComparison.Ordinal);
            if (i < 0 || !double.TryParse(e.Url.AsSpan(i + 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return;
            try
            {
                await vm.SaveScrollRatioAsync(Math.Clamp(r, 0, 1));
            }
            catch (Exception ex)
            {
                // async void の例外は TaskScheduler.UnobservedTaskException で拾えないため、
                // ここで握り潰してプロセスクラッシュを防ぐ。
                MessageService.Warn($"Save scroll ratio failed: {ex.Message}");
            }
        }
    }
}
