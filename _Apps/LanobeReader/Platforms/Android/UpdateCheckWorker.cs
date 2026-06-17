using Android.Content;
using AndroidX.Work;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

public class UpdateCheckWorker : Worker
{
    public const string WORK_TAG = "lanobe_update_check";

    // WorkManager が停止(quota 失効/制約喪失/システム回収)を通知したとき、進行中の巡回を
    // 協調キャンセルするためのトークン。これにより CheckAll のラウンドロビン「続きから再開」が機能する。
    private readonly CancellationTokenSource _cts = new();

    public UpdateCheckWorker(Context context, WorkerParameters workerParams) : base(context, workerParams)
    {
    }

    public override void OnStopped()
    {
        try { _cts.Cancel(); } catch { /* 破棄済み */ }
        base.OnStopped();
    }

    public override Result DoWork()
    {
        try
        {
            // Worker threads have no SynchronizationContext, so blocking on Task.Run is safe here.
            // 実処理は前面サービス経路と共通の UpdateCheckRunner に集約。
            var outcome = Task.Run(() => UpdateCheckRunner.RunAsync(ApplicationContext, _cts.Token)).GetAwaiter().GetResult();
            return outcome switch
            {
                UpdateCheckRunner.Outcome.Retry => Result.InvokeRetry(),
                UpdateCheckRunner.Outcome.Failure => Result.InvokeFailure(),
                _ => Result.InvokeSuccess(),
            };
        }
        catch (Exception ex)
        {
            MessageService.Error($"Unexpected error: {ex.Message}");
            return Result.InvokeFailure();
        }
        finally
        {
            _cts.Dispose();
        }
    }
}
