namespace NewReleaseChecker.Core.Abstractions;

/// <summary>定期チェックの登録/解除（WorkManager）。実装は Platforms/Android。</summary>
public interface IWorkScheduler
{
    /// <summary>指定周期で定期タスクを登録（既存があれば再登録）。interval は IPreferencesService.AutoCheckInterval の値。</summary>
    void Schedule(string interval);

    /// <summary>定期タスクをキャンセルする。</summary>
    void Cancel();
}
