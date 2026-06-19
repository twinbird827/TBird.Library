namespace LanobeReader.Platforms.Android;

/// <summary>
/// MainApplication 初期化完了前に起動しうる経路(MainActivity.OnCreate・前面サービス・WorkManager
/// いずれもコールドブート直後に走りうる)向けに、DI コンテナ
/// (<see cref="IPlatformApplication.Current"/>.Services)が利用可能になるまで短時間待つ共通ヘルパ。
/// 取得できれば <see cref="IServiceProvider"/> を、タイムアウトすれば null を返す。
/// 同一の待機ロジックを各経路で重複させない(調整は本メソッド 1 箇所)ために抽出。
/// </summary>
internal static class PlatformServiceReadiness
{
    private const int MaxAttempts = 30;
    private const int DelayMs = 100; // 合計 最大 ~3 秒待つ。

    /// <summary>DI が準備できるまで最大 ~3 秒待ち、IServiceProvider を返す(不可なら null)。</summary>
    public static async Task<IServiceProvider?> WaitForServicesAsync()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null) return services;
            await Task.Delay(DelayMs).ConfigureAwait(false);
        }
        return null;
    }
}
