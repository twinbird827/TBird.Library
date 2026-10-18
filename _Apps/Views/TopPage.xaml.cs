using ComicViewer.Services;
using ComicViewer.ViewModels;

namespace ComicViewer.Views;

public partial class TopPage : ContentPage
{
    private readonly StoragePermissionService _permission;
    private bool _hadPermission;

    public TopPage(TopViewModel viewModel, StoragePermissionService permission)
    {
        ViewModel = viewModel;
        _permission = permission;
        InitializeComponent();
        BindingContext = viewModel;
    }

    public TopViewModel ViewModel { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _hadPermission = _permission.IsGranted;
        if (Window is not null) Window.Resumed += OnResumed;
        ViewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        if (Window is not null) Window.Resumed -= OnResumed;
        base.OnDisappearing();
    }

    // 設定画面で権限を許可して戻ってきたときにローカルボリュームを有効にする。
    private void OnResumed(object? sender, EventArgs e)
    {
        if (_permission.IsGranted == _hadPermission) return;
        _hadPermission = _permission.IsGranted;
        ViewModel.LoadCommand.Execute(null);
    }
}
