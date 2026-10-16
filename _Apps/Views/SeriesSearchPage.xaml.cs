using NewReleaseChecker.App.ViewModels;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.App.Views;

public partial class SeriesSearchPage : ContentPage
{
    private readonly SeriesSearchViewModel _vm;

    public SeriesSearchPage(SeriesSearchViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView cv) cv.SelectedItem = null;
        if (e.CurrentSelection.FirstOrDefault() is not RakutenBook book) return;

        var (key, authors, media) = _vm.BuildDefault(book);
        var ctx = new RegisterConfirmContext(key, authors, media);
        var tcs = new TaskCompletionSource<SeriesRegistration?>();

        await Navigation.PushModalAsync(new RegisterConfirmPage(ctx, tcs));
        var reg = await tcs.Task;
        if (reg is not null)
        {
            await _vm.RegisterConfirmedAsync(reg);
        }
    }
}
