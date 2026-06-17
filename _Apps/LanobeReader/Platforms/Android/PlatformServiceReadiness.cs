namespace LanobeReader.Platforms.Android;

/// <summary>
/// MAUI の DI コンテナ(<see cref="IPlatformApplication.Current"/>.Services)が準備できるまで待つ共通ヘルパ。
/// FGS / Worker / MainActivity いずれも MainApplication 初期化完了前に起動しうるため、短時間ポーリングで待つ。
/// 待ち budget / 間隔 / null 処理を 1 箇所に集約し、複数経路でのコピー乖離(レース再発)を防ぐ。
/// </summary>
internal static class PlatformServiceReadiness
{
    /// <summary>
    /// DI コンテナが利用可能になるまで最大 <paramref name="maxAttempts"/>×<paramref name="delayMs"/>ms 待つ。
    /// 取得できれば <see cref="IServiceProvider"/> を、時間内に準備できなければ null を返す
    /// (呼び出し側は WorkManager のバックオフ等にリトライを委ねる)。
    /// </summary>
    public static async Task<IServiceProvider?> WaitForServicesAsync(int maxAttempts = 30, int delayMs = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is not null) return services;
            await Task.Delay(delayMs).ConfigureAwait(false);
        }
        return null;
    }
}
