using LanobeReader.Helpers;
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class EpisodeListPage : ContentPage
{
    public EpisodeListPage(EpisodeListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EpisodeListViewModel vm)
        {
            try
            {
                // ApplyQueryAttributes が起動した InitializeAsync の完了を待ってから RefreshReadStatusAsync。
                // 旧実装は両者が並列実行され、初回表示時に DB クエリの二重実行が発生していた。
                await vm.EnsureInitializedAsync();
                await vm.RefreshReadStatusAsync();
            }
            catch (Exception ex)
            {
                // async void の例外は TaskScheduler.UnobservedTaskException で拾えないため、
                // ここで握り潰してプロセスクラッシュを防ぐ。
                LogHelper.Warn(nameof(EpisodeListPage),
                    $"OnAppearing failed: {ex.Message}");
            }
        }
    }
}
