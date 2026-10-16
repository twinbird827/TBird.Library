using LanobeReader.ViewModels;
using TBird.Core;

namespace LanobeReader.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
        {
            try
            {
                await vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                // async void の例外は TaskScheduler.UnobservedTaskException で拾えないため、
                // ここで握り潰してプロセスクラッシュを防ぐ。
                MessageService.Warn($"OnAppearing failed: {ex.Message}");
            }
        }
    }
}
