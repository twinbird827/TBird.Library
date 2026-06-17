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
        // 表示中だけ新着メッセージを購読し、非表示時に解除する(VM は Transient のため購読を
        // ライフサイクルに束ねないと旧 VM が積み上がり、非表示ページまで再読込してしまう)。
        _viewModel.SubscribeToUpdates();
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.UnsubscribeFromUpdates();
    }
}
