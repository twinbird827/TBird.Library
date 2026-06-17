using CommunityToolkit.Mvvm.Messaging;
using LanobeReader.Services;
using TBird.Core;
using TBird.Maui.ViewModels;

namespace LanobeReader.ViewModels;

/// <summary>
/// 前面滞在中に背面チェックが新着を検出した場合、システム通知は抑止されるため、画面内の一覧を
/// 自動再読込して NEW 表示へ即時反映する一覧系 VM の共通基底。
///
/// 購読の二重登録防止・UI スレッドへのマーシャリング・例外ログを 1 箇所へ集約し、各 VM は
/// <see cref="OnUpdatesDetectedAsync"/> の実装だけを担う(NovelList と EpisodeList で重複していた
/// 購読定型を撤去)。VM は AddTransient のため、購読は画面表示中のみ(Page の
/// OnAppearing/OnDisappearing で <see cref="SubscribeToUpdates"/>/<see cref="UnsubscribeFromUpdates"/>)
/// 有効化し、非表示の旧 VM が積み上がって再読込される事態を防ぐ。
/// </summary>
public abstract class AutoReloadViewModel : ErrorAwareViewModel
{
    /// <summary>新着メッセージの購読を開始する(表示中のみ有効化する想定)。</summary>
    public void SubscribeToUpdates()
    {
        // OnAppearing が複数回呼ばれても二重登録にならないよう、登録前に必ず解除する。
        WeakReferenceMessenger.Default.Unregister<UpdatesDetectedMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdatesDetectedMessage>(this, (_, message) =>
        {
            // Send は背面スレッドから呼ばれうるため UI スレッドへ戻す。
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await OnUpdatesDetectedAsync(message); }
                catch (Exception ex) { MessageService.Warn($"Auto-reload on updates failed: {ex.Message}"); }
            });
        });
    }

    /// <summary>画面非表示時に購読を解除する(非表示中は次回 OnAppearing の再読込が NEW を反映する)。</summary>
    public void UnsubscribeFromUpdates()
        => WeakReferenceMessenger.Default.Unregister<UpdatesDetectedMessage>(this);

    /// <summary>新着検出時の再読込処理。UI スレッド上で呼ばれる。</summary>
    protected abstract Task OnUpdatesDetectedAsync(UpdatesDetectedMessage message);
}
