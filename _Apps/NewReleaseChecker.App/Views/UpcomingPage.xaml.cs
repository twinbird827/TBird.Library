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

    // 選択モード中の戻るボタンは画面を離脱せず選択モードを解除する（F-015）。
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is SelectableBookListViewModel { IsSelectionMode: true } vm)
        {
            vm.ExitSelectionModeCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
