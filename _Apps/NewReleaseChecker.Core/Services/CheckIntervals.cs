namespace NewReleaseChecker.Core.Services;

/// <summary>自動チェック間隔の定義（Preferences キー・設定画面の表示ラベル・WorkManager の周期[時間]）。</summary>
public sealed record CheckInterval(string Key, string Label, int Hours);

/// <summary>
/// 自動チェック間隔の語彙を一元管理する（要件 §7.6）。
/// キー・ラベル・周期(時間)を1か所で定義し、追加時の更新漏れ（スケジューラ側 switch の
/// 無言フォールバックや、ラベル配列とキー配列のインデックスずれ）を防ぐ。
/// </summary>
public static class CheckIntervals
{
    public static readonly IReadOnlyList<CheckInterval> All = new[]
    {
        new CheckInterval("daily_once", "1日1回", 24),
        new CheckInterval("daily_twice", "1日2回", 12),
        new CheckInterval("every_6h", "6時間ごと", 6),
        new CheckInterval("every_12h", "12時間ごと", 12),
    };

    /// <summary>既定の間隔キー（1日1回）。</summary>
    public static string DefaultKey => All[0].Key;

    /// <summary>間隔キーから WorkManager 周期（時間）へ。未知キーは既定（24h）。</summary>
    public static int ToHours(string? key)
        => All.FirstOrDefault(i => i.Key == key)?.Hours ?? All[0].Hours;

    /// <summary>間隔キーから設定画面の選択インデックスへ。未知キーは 0。</summary>
    public static int IndexOf(string? key)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].Key == key) return i;
        }
        return 0;
    }
}
