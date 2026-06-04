using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class FavoritesPage : ContentPage
{
    private readonly FavoritesViewModel _vm;

    public FavoritesPage(FavoritesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Singleton VM のため、タブ復帰時に前回の選択モードを持ち越さない（F-015）。
        _vm.ExitSelectionModeCommand.Execute(null);
        await _vm.LoadAsync();
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
