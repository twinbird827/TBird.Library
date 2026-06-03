using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class UpcomingPage : ContentPage
{
    private readonly UpcomingViewModel _vm;

    public UpcomingPage(UpcomingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
