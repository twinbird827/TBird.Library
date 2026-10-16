namespace NewReleaseChecker.Core.Abstractions;

/// <summary>ローカル通知（新刊検知時のプッシュ通知）。実装は Plugin.LocalNotification（App 層）。</summary>
public interface ILocalNotifier
{
    /// <summary>通知を発行する。tapSeriesId を渡すとタップ時に該当シリーズ詳細へ遷移する。</summary>
    Task ShowAsync(string title, string message, int? tapSeriesId = null);
}
