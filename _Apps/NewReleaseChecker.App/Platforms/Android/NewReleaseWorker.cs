using Android.Content;
using AndroidX.Work;
using Microsoft.Extensions.DependencyInjection;
using NewReleaseChecker.Core.Services;
using TBird.Core;

namespace NewReleaseChecker.App.Platforms.Android;

/// <summary>
/// WorkManager の定期実行ワーカー（要件 §3.2.6 / §7.6）。
/// Android ランタイムが生成するためコンストラクタ DI が効かない。
/// 共通チェックサービス等は IPlatformApplication.Current.Services（MAUI の Singleton）から解決する。
/// </summary>
public class NewReleaseWorker : Worker
{
    public NewReleaseWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null)
            {
                MessageService.Error("WorkManager: ServiceProvider を取得できませんでした");
                return Result.InvokeRetry();
            }

            var check = services.GetRequiredService<NewReleaseCheckService>();
            // WorkManager の約10分制限内に収めるため 9 分でキャンセルする予算を渡す。
            // ct は SiteRateLimiter 末端まで配管済みで、超過時は協調キャンセルして部分状態の書込み途中停止を避ける。
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(9));
            // Worker はバックグラウンドスレッドで動くため同期待機してよい
            check.CheckAsync(CheckTrigger.Auto, cts.Token).GetAwaiter().GetResult();

            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            // 自動チェック失敗はユーザー通知せずログのみ（次回周期/バックオフに任せる）
            MessageService.Exception(ex);
            return Result.InvokeRetry();
        }
    }
}
