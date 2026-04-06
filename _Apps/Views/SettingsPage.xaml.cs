using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private bool _initialized;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();

        // Set radio buttons to match loaded values
        switch (_viewModel.BackgroundTheme)
        {
            case 1: ThemeBlack.IsChecked = true; break;
            case 2: ThemeSepia.IsChecked = true; break;
            default: ThemeWhite.IsChecked = true; break;
        }

        switch (_viewModel.LineSpacing)
        {
            case 0: SpacingNarrow.IsChecked = true; break;
            case 2: SpacingWide.IsChecked = true; break;
            default: SpacingNormal.IsChecked = true; break;
        }

        _initialized = true;
    }

    private void OnThemeChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!_initialized || !e.Value) return;

        if (sender == ThemeWhite) _viewModel.BackgroundTheme = 0;
        else if (sender == ThemeBlack) _viewModel.BackgroundTheme = 1;
        else if (sender == ThemeSepia) _viewModel.BackgroundTheme = 2;
    }

    private void OnSpacingChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!_initialized || !e.Value) return;

        if (sender == SpacingNarrow) _viewModel.LineSpacing = 0;
        else if (sender == SpacingNormal) _viewModel.LineSpacing = 1;
        else if (sender == SpacingWide) _viewModel.LineSpacing = 2;
    }
}
