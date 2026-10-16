namespace NewReleaseChecker.Core.Abstractions;

/// <summary>アプリ設定（MAUI Preferences）。除外キーワードは JSON 文字列で保存（要件 §5.5）。</summary>
public interface IPreferencesService
{
    bool NotificationEnabled { get; set; }
    bool AutoCheckEnabled { get; set; }

    /// <summary>"daily_once" / "daily_twice" / "every_6h" / "every_12h"。</summary>
    string AutoCheckInterval { get; set; }

    IReadOnlyList<string> ExcludeKeywords { get; set; }

    /// <summary>除外キーワードを初期値に戻す。</summary>
    void ResetExcludeKeywords();
}
