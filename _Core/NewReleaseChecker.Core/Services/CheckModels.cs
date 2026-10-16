namespace NewReleaseChecker.Core.Services;

/// <summary>チェックの起動契機。</summary>
public enum CheckTrigger
{
    /// <summary>手動（更新ボタン/プルリフレッシュ）。失敗時トースト。</summary>
    Manual,

    /// <summary>自動（WorkManager 周期）。失敗時ログのみ。</summary>
    Auto,
}

/// <summary>チェック結果サマリ。</summary>
public sealed record CheckSummary(int TargetCount, int NewCount, int ReservationCount);

/// <summary>シリーズ登録の入力（F-001 登録確認ダイアログの確定値）。</summary>
public sealed record SeriesRegistration
{
    public string SeriesKey { get; init; } = string.Empty;

    /// <summary>ユーザーが選択した著者（生の人物名。正規化前）。</summary>
    public IReadOnlyList<string> SelectedAuthors { get; init; } = Array.Empty<string>();

    /// <summary>"novel" / "comic"。</summary>
    public string MediaType { get; init; } = Core.MediaType.Novel;
}
