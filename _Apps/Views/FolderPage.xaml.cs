using ComicViewer.ViewModels;
using TBird.Core;

namespace ComicViewer.Views;

public partial class FolderPage : ContentPage
{
    public FolderPage(FolderViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        // ナビバーの戻る矢印もハードウェアの戻るキーと同じく親フォルダへ戻す。
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(GoBack) });
    }

    public FolderViewModel ViewModel { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await ViewModel.OnAppearingAsync();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    protected override void OnDisappearing()
    {
        ViewModel.OnDisappearing();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        GoBack();
        return true;
    }

    private async void GoBack()
    {
        try
        {
            await ViewModel.BackAsync();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    private void OnScrolled(object? sender, ItemsViewScrolledEventArgs e) => ViewModel.FirstVisibleIndex = e.FirstVisibleItemIndex;
}
