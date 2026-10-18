using ComicViewer.ViewModels;

namespace ComicViewer.Views;

public partial class SmbEditPage : ContentPage
{
    public SmbEditPage(SmbEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
