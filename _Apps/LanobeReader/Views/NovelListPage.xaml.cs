using System.Threading;
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class NovelListPage : ContentPage
{
    private readonly NovelListViewModel _viewModel;

    /// <summary>
    /// 一覧(本棚)ページが現在表示中か。表示中はアプリ内 NEW 表示が新着を直接見せるため、
    /// 背面チェックのシステム通知を抑止してよい(<see cref="Platforms.Android.UpdateNotificationService"/>)。
    /// 他ページ滞在中(本棚が非表示)は通知を出す。購読(SubscribeToUpdates)のライフサイクルと一致する。
    /// (名称は VisualElement.IsVisible との衝突を避けるため IsShowing とする)
    /// 書込は UI スレッド(OnAppearing/OnDisappearing)、読取は WorkManager/FGS の背面スレッド
    /// (UpdateNotificationService)のため、メモリ可視性を担保する volatile とする
    /// (対になる AppForegroundTracker.IsForeground が Volatile.Read を使うのと同じ理由)。
    /// </summary>
    public static bool IsShowing => Volatile.Read(ref _isShowing);
    private static bool _isShowing;

    public NovelListPage(NovelListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Volatile.Write(ref _isShowing, true);
        // 表示中だけ新着メッセージを購読し、非表示時に解除する(VM は Transient のため購読を
        // ライフサイクルに束ねないと旧 VM が積み上がり、非表示ページまで再読込してしまう)。
        _viewModel.SubscribeToUpdates();
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Volatile.Write(ref _isShowing, false);
        _viewModel.UnsubscribeFromUpdates();
    }
}
