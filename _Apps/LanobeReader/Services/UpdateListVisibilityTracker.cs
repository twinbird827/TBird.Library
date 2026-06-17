namespace LanobeReader.Services;

/// <summary>
/// 「新着(NEW)を即時表示する一覧画面(本棚)が現在可視で購読中か」を追跡するプラットフォーム非依存の
/// トラッカ。前面判定(Activity ライフサイクル=<c>AppForegroundTracker.IsForeground</c>)と組み合わせ、
/// アプリ内一覧が新着を直接表示している間だけシステム通知を抑止するために使う。
///
/// 購読中の VM を<b>弱参照</b>で保持する。VM は AddTransient のため、OnDisappearing が(高速ナビゲーション
/// やプロセス再生成等で)対にならず購読解除が漏れても、対象 VM が GC されれば次回参照時に自動的に一覧から
/// 外れて自己修復する。旧実装の int カウンタは漏れると正に固着し、前面の非一覧画面(リーダー/設定)滞在中に
/// システム通知が恒久的に抑止される事故(=新着が通知にもアプリ内にも現れない)があった。
///
/// ViewModel 層(<see cref="ViewModels.AutoReloadViewModel"/>)から増減されるため、Platforms.Android の
/// 静的クラスではなく中立な Services 層に置く(VM がプラットフォーム実装へ逆依存しないようにする)。
/// </summary>
public static class UpdateListVisibilityTracker
{
    private static readonly object _gate = new();
    private static readonly List<WeakReference> _visible = new();

    /// <summary>新着を即時表示する一覧が 1 つ以上可視で購読中か。</summary>
    public static bool HasVisibleUpdateList
    {
        get
        {
            lock (_gate)
            {
                Prune();
                return _visible.Count > 0;
            }
        }
    }

    /// <summary>一覧画面が可視(購読開始)になった。owner は購読中の VM インスタンス。</summary>
    public static void OnSubscribed(object owner)
    {
        lock (_gate)
        {
            Prune();
            // 同一 owner の二重登録を防ぐ(OnAppearing が複数回呼ばれても 1 エントリに保つ)。
            if (!_visible.Any(w => ReferenceEquals(w.Target, owner)))
            {
                _visible.Add(new WeakReference(owner));
            }
        }
    }

    /// <summary>一覧画面が非可視(購読解除)になった。owner は購読を解除する VM インスタンス。</summary>
    public static void OnUnsubscribed(object owner)
    {
        lock (_gate)
        {
            // 当該 owner と、既に GC 済みの死んだ弱参照をまとめて除去する。
            _visible.RemoveAll(w => !w.IsAlive || ReferenceEquals(w.Target, owner));
        }
    }

    /// <summary>
    /// 全購読をクリアする。アプリが背面化(可視 Activity 数 0)した時点で呼び、OnDisappearing が
    /// 万一発火しなかった場合の漏れ(=前面復帰後に通知が恒久抑止される事故)を自己修復する。
    /// 背面では可視一覧は存在し得ないためクリアは常に安全。
    /// </summary>
    public static void Reset()
    {
        lock (_gate)
        {
            _visible.Clear();
        }
    }

    // GC 済み(購読解除が漏れた transient VM)の弱参照を除去する。_gate 保持下で呼ぶこと。
    private static void Prune() => _visible.RemoveAll(w => !w.IsAlive);
}
