using LanobeReader.Helpers;
using LanobeReader.ViewModels;

namespace LanobeReader.Views;

public partial class EpisodeListPage : ContentPage
{
    private int? _pendingScrollIndex;

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

                // 初回 OnAppearing で 1 回だけスクロール。Take... が null クリアするので
                // Reader から戻った再表示時には再スクロールしない。
                var idx = vm.TakePendingInitialScrollIndex();
                if (idx is int i)
                {
                    // SizeChanged フォールバック用にもインデックスを保持。
                    // TryScrollToPending() の成功時に null クリアすることで二重実行を防ぐ。
                    _pendingScrollIndex = i;

                    // OnAppearing 時点でページは visible だが、ItemsSource 差し替え直後で
                    // CollectionView の measure/layout 未完だと ScrollTo が空振りする。
                    // DispatchDelayed で 150ms 待ち、Android の最初の layout サイクル完了を確実にする。
                    // それでも空振りする場合は OnEpisodesViewSizeChanged が初回 size 確定時に再 ScrollTo する。
                    Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
                    {
                        // このデリゲートは外側 try/catch の保護対象外。OnAppearing → 即 OnDisappearing の
                        // 遷移直後に走った場合 ObjectDisposed で例外する可能性があるため明示的に握り潰す。
                        try
                        {
                            TryScrollToPending();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Warn(nameof(EpisodeListPage),
                                $"Delayed ScrollTo failed: {ex.Message}");
                        }
                    });
                }
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
    /// _pendingScrollIndex が指す行へ ScrollTo を試みる。Center / animate=false で画面中央に置く。
    /// 成功時に _pendingScrollIndex を null クリアして 1 回限り消費を保証する。
    /// DispatchDelayed と SizeChanged の両経路から呼ばれるため、必ずここで null 化する。
    /// </summary>
    private void TryScrollToPending()
    {
        if (_pendingScrollIndex is not int i) return;
        if (BindingContext is not EpisodeListViewModel vm) return;
        if (i < 0 || i >= vm.Episodes.Count) return;

        EpisodesView.ScrollTo(i, position: ScrollToPosition.Center, animate: false);
        _pendingScrollIndex = null;
    }

    /// <summary>
    /// CollectionView の measure/layout 確定時に発火。DispatchDelayed(150ms) が
    /// 遅い実機で空振りしたケースを救うフォールバック経路。
    /// _pendingScrollIndex が null (= 既に消費済 or 不要) なら何もしない。
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
