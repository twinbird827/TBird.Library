using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class FavoritesPage : SelectableBookListPage
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
}
