using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class EpisodeListPage : ContentPage
{
    public EpisodeListPage(EpisodeListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
