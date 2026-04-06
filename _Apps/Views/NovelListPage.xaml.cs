using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class NovelListPage : ContentPage
{
    private readonly NovelListViewModel _viewModel;

    public NovelListPage(NovelListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnGoToSearchClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//search");
    }
}
