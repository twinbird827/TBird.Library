using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// アラーム発火時に Doze を貫通して新着チェックを「その場で完遂」する前面サービス。
/// WorkManager は Doze 中、ネットワーク制約付きジョブを次のメンテ窓まで延期しうるため、
/// アラーム（exact / AllowWhileIdle）の起床ウィンドウ内で同期的に処理して延期を回避する。
/// 背面からの前面起動が拒否された場合（例外）は WorkManager 経路へフォールバックする。
///
/// 種別 = shortService: 背面起動される短時間タスク専用（API 34+）。背面起動の許可条件が緩く
/// exact アラームの一時 allowlist と相性が良い。dataSync の API 35 背面起動制限・6h/日上限を回避。
/// 制約は「約3分以内に終える」こと（チェックは通常数秒。超過時は <see cref="OnTimeout(int)"/> で停止）。
/// </summary>
// Name を明示し、テスト時に `adb shell am start-foreground-service` で安定して指定できるようにする。
[Service(Name = "com.tbird.lanobereader.UpdateCheckForegroundService",
    Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeShortService)]
public class UpdateCheckForegroundService : Service
{
    public const string CHANNEL_ID = "update_check_progress";
    // novel.Id を notificationId に使う更新通知と衝突しない固定 ID。
    private const int OngoingNotificationId = 1_000_000;

    // shortService の3分上限到達(OnTimeout)時にチェック処理をきれいに中断するためのトークン。
    private CancellationTokenSource? _cts;

    /// <summary>前面サービスを起動する。Android 8.0+ の背面起動規約に従い StartForegroundService 経由。</summary>
    public static void Start(Context context)
    {
        var intent = new Intent(context, typeof(UpdateCheckForegroundService));
        ContextCompat.StartForegroundService(context, intent);
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            // 8.0+ は起動直後の startForeground が必須。12+ で背面起動が許可されない場合は
            // ここで例外となるため、WorkManager 経路へフォールバックして即終了する。
            StartInForeground();
        }
        catch (Exception ex)
        {
            MessageService.Warn($"startForeground denied; fallback to WorkManager: {ex.Message}");
            try { UpdateCheckScheduler.EnqueueOneTimeCheck(ApplicationContext!); }
            catch (Exception ex2) { MessageService.Error($"Fallback enqueue failed: {ex2.Message}"); }
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        _cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                var outcome = await UpdateCheckRunner.RunAsync(ApplicationContext!, _cts.Token).ConfigureAwait(false);
                if (outcome == UpdateCheckRunner.Outcome.Retry)
                {
                    // プロセス未初期化等でチェックを実行できなかった。FGS は再試行機構を持たないため、
                    // WorkManager のバックオフ再試行へ委ねて取りこぼしを防ぐ(コールドブート窓対策)。
                    MessageService.Warn("FGS check returned Retry; enqueueing WorkManager fallback");
                    try { UpdateCheckScheduler.EnqueueOneTimeCheck(ApplicationContext!); }
                    catch (Exception ex2) { MessageService.Error($"Fallback enqueue failed: {ex2.Message}"); }
                }
            }
            catch (Exception ex)
            {
                MessageService.Error($"Foreground update check failed: {ex.Message}");
            }
            finally
            {
                try { StopForeground(StopForegroundFlags.Remove); } catch { /* 既に停止済み */ }
                StopSelf(startId);
            }
        });

        // 処理中に kill されても自動再起動しない（次回はアラーム/定期ワークが担う）。
        return StartCommandResult.NotSticky;
    }

    private void StartInForeground()
    {
        EnsureChannel();

        var notification = new NotificationCompat.Builder(this, CHANNEL_ID)!
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)!
            .SetContentTitle("更新を確認中")!
            .SetContentText("新着をチェックしています…")!
            .SetPriority(NotificationCompat.PriorityLow)!
            .SetOngoing(true)!
            .Build();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(OngoingNotificationId, notification!, ForegroundService.TypeShortService);
        }
        else
        {
            StartForeground(OngoingNotificationId, notification!);
        }
    }

    /// <summary>
    /// shortService の実行上限（約3分）に達した場合にシステムから呼ばれる安全網。
    /// 通常はチェックが数秒で終わり自己停止するため呼ばれない。呼ばれたら速やかに停止する。
    /// </summary>
    public override void OnTimeout(int startId)
    {
        MessageService.Warn("FGS short-service timeout reached; cancelling check and stopping");
        // チェックを中断。進行中の作品は LastCheckedAt 未更新のまま残り、次回その作品から再開する。
        try { _cts?.Cancel(); } catch { /* 破棄済み */ }
        try { StopForeground(StopForegroundFlags.Remove); } catch { /* 既に停止済み */ }
        StopSelf(startId);
    }

    public override void OnDestroy()
    {
        try { _cts?.Cancel(); } catch { /* 破棄済み */ }
        _cts?.Dispose();
        _cts = null;
        base.OnDestroy();
    }

    private void EnsureChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var manager = GetSystemService(NotificationService) as NotificationManager;
        if (manager is null) return;
        if (manager.GetNotificationChannel(CHANNEL_ID) is not null) return;

        // Low 重要度 = 音/ヘッドアップなし。バッジも出さない（更新通知本体とは別チャンネル）。
        var channel = new NotificationChannel(CHANNEL_ID, "更新チェック", NotificationImportance.Low)
        {
            Description = "バックグラウンドで新着を確認している間に表示する通知"
        };
        channel.SetShowBadge(false);
        manager.CreateNotificationChannel(channel);
    }
}
