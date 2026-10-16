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
    /// <summary>
    /// この画面が「新着を即時表示する一覧」としてシステム通知抑止に寄与するか。
    /// true の VM が前面で可視の間はシステム通知を抑止する(アプリ内一覧が NEW を表示するため)。
    /// 全作品の NEW を反映する本棚(NovelList)は true。表示中作品の話一覧しか反映しない目次
    /// (EpisodeList)は、別作品の更新通知まで握り潰してしまうため false を返してオプトアウトする。
    /// </summary>
    protected virtual bool ParticipatesInNotificationSuppression => true;

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

        // 「新着を即時表示する一覧が可視」であることを可視一覧トラッカへ通知する。前面でも一覧非表示の
        // 画面(リーダー/設定/目次)滞在中はこのカウントが 0 となり、システム通知が抑止されず確実に届く。
        // トラッカ側が ReferenceEquals で冪等化するため、OnAppearing が複数回呼ばれても 1 エントリに収束する。
        // 旧実装はインスタンスのフラグ(_countedAsVisibleList)で二重登録を防いでいたが、背面化時の
        // UpdateListVisibilityTracker.Reset() がトラッカだけをクリアするとフラグが true のまま desync し、
        // 前面復帰後の再購読で OnSubscribed がスキップされて「可視なのに通知抑止が恒久的に効かない」事故が
        // あった。フラグを廃しトラッカ登録を唯一の真実源とすることで、Reset 後でも再購読が確実に再登録する。
        if (ParticipatesInNotificationSuppression)
        {
            UpdateListVisibilityTracker.OnSubscribed(this);
        }
    }

    /// <summary>画面非表示時に購読を解除する(非表示中は次回 OnAppearing の再読込が NEW を反映する)。</summary>
    public void UnsubscribeFromUpdates()
    {
        WeakReferenceMessenger.Default.Unregister<UpdatesDetectedMessage>(this);
        // OnUnsubscribed は当該 owner を除去するだけ(未登録なら no-op)で冪等。
        if (ParticipatesInNotificationSuppression)
        {
            UpdateListVisibilityTracker.OnUnsubscribed(this);
        }
    }

    /// <summary>新着検出時の再読込処理。UI スレッド上で呼ばれる。</summary>
    protected abstract Task OnUpdatesDetectedAsync(UpdatesDetectedMessage message);
}
