using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class SeriesListPage : ContentPage
{
    private readonly SeriesListViewModel _vm;

    public SeriesListPage(SeriesListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
