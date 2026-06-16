using Android.Content;
using AndroidX.Work;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

public class UpdateCheckWorker : Worker
{
    public const string WORK_TAG = "lanobe_update_check";

    public UpdateCheckWorker(Context context, WorkerParameters workerParams) : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        try
        {
            // Worker threads have no SynchronizationContext, so blocking on Task.Run is safe here.
            // 実処理は前面サービス経路と共通の UpdateCheckRunner に集約。
            var outcome = Task.Run(() => UpdateCheckRunner.RunAsync(ApplicationContext)).GetAwaiter().GetResult();
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
    }
}
