using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class RankingPage : ContentPage
{
    private readonly RankingViewModel _vm;

    public RankingPage(RankingViewModel vm)
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
