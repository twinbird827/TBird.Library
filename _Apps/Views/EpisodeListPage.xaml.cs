using LanobeReader.Helpers;
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class EpisodeListPage : ContentPage
{
    private int? _pendingScrollIndex;
    private bool _pendingToCenter;

    public EpisodeListPage(EpisodeListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // VM の PageContentReset は InitializeAsync (ApplyQueryAttributes 経由) からも発火するため、
        // OnAppearing より前のコンストラクタ時点で購読しないと初回イベントを取りこぼす。
        // Page と VM は共に Transient (MauiProgram.cs) で寿命が一致するため購読解除は不要。
        viewModel.PageContentReset += OnPageContentReset;
    }

    // XAML の DataTemplate 内から x:Reference 経由でアクセスするための型付きプロパティ。
    // BindingContext を直接参照すると object 扱いになりコンパイル済みバインディングが効かない。
    public EpisodeListViewModel ViewModel => (EpisodeListViewModel)BindingContext;

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

    /// <summary>
    /// VM からのスクロール指示。LoadPageAsync 完了と同じ UI tick で発火されるため、ここで同期的に
    /// ScrollTo を呼べば RecyclerView は「アイテム配置 + スクロール target」を 1 回のレイアウト
    /// パスで処理する。ユーザーには中間スクロールが見えない。
    /// それでも空振りした場合 (measure 未完等) のため _pendingScrollIndex に target を残し、
    /// 後続の OnEpisodesViewSizeChanged で再試行する。
    /// </summary>
    private void OnPageContentReset(object? sender, PageContentResetArgs e)
    {
        _pendingScrollIndex = e.ScrollIndex;
        _pendingToCenter = e.ToCenter;
        TryScrollToPending();
    }

    /// <summary>
    /// _pendingScrollIndex が指す行へ ScrollTo を試みる。ToCenter フラグで Center / Start を選択。
    /// 成功時に _pendingScrollIndex を null クリアして二重実行を防ぐ。
    /// </summary>
    private void TryScrollToPending()
    {
        if (_pendingScrollIndex is not int i) return;
        if (BindingContext is not EpisodeListViewModel vm) return;
        if (i < 0 || i >= vm.Episodes.Count) return;

        var position = _pendingToCenter ? ScrollToPosition.Center : ScrollToPosition.Start;
        EpisodesView.ScrollTo(i, position: position, animate: false);
        _pendingScrollIndex = null;
    }

    /// <summary>
    /// CollectionView の measure/layout 確定時に発火。同期 ScrollTo が silent no-op だった場合の
    /// フォールバック。_pendingScrollIndex が消費済なら何もしない。
    /// </summary>
    private void OnEpisodesViewSizeChanged(object? sender, EventArgs e)
    {
        try
        {
            TryScrollToPending();
        }
        catch (Exception ex)
        {
            LogHelper.Warn(nameof(EpisodeListPage),
                $"SizeChanged ScrollTo failed: {ex.Message}");
        }
    }
}
