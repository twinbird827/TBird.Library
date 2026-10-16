using NewReleaseChecker.App.ViewModels;
using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.App.Views;

/// <summary>登録確認ダイアログ（モーダルページ）。結果は TaskCompletionSource で返す。</summary>
public partial class RegisterConfirmPage : ContentPage
{
    private readonly RegisterConfirmContext _ctx;
    private readonly TaskCompletionSource<SeriesRegistration?> _tcs;

    public RegisterConfirmPage(RegisterConfirmContext ctx, TaskCompletionSource<SeriesRegistration?> tcs)
    {
        InitializeComponent();
        BindingContext = _ctx = ctx;
        _tcs = tcs;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult(null);
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult(_ctx.Build());
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
