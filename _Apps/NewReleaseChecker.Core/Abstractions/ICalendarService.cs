namespace NewReleaseChecker.Core.Abstractions;

/// <summary>
/// カレンダー連携。インテント方式（前景のみ）。将来 OAuth+Calendar API へ差し替え可能にするため抽象化。
/// </summary>
public interface ICalendarService
{
    /// <summary>発売日をイベントとしてカレンダー追加インテントを発火する。発火できたら true。</summary>
    Task<bool> AddEventAsync(string title, DateTime date, string? description = null);
}
