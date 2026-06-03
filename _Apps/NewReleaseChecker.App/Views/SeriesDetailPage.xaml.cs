using NewReleaseChecker.App.ViewModels;

namespace NewReleaseChecker.App.Views;

public partial class SeriesDetailPage : ContentPage
{
    private readonly SeriesDetailViewModel _vm;

    public SeriesDetailPage(SeriesDetailViewModel vm)
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
