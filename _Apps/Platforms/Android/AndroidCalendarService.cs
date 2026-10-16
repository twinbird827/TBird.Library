using Android.Content;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using NewReleaseChecker.Core.Abstractions;
using TBird.Core;

namespace NewReleaseChecker.App.Platforms.Android;

/// <summary>
/// カレンダー追加インテント（ICalendarService 実装）。要件 §3.3 F-013。
/// OAuth・API キー不要。前景のみ実行可能。
/// </summary>
public sealed class AndroidCalendarService : ICalendarService
{
    public Task<bool> AddEventAsync(string title, DateTime date, string? description = null)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity is null) return Task.FromResult(false);

            // 終日イベントの begin/end は UTC 真夜中で渡す必要がある（Android は AllDay を UTC で日付解釈する）。
            // ローカル時刻として解釈すると JST(UTC+9) では UTC へ変換した時点で前日にずれるため TimeSpan.Zero を明示する。
            var begin = new DateTimeOffset(date.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var end = new DateTimeOffset(date.Date.AddDays(1), TimeSpan.Zero).ToUnixTimeMilliseconds();

            var intent = new Intent(Intent.ActionInsert)
                .SetData(CalendarContract.Events.ContentUri)
                .PutExtra(CalendarContract.Events.InterfaceConsts.Title, title)
                .PutExtra(CalendarContract.Events.InterfaceConsts.Description, description ?? string.Empty)
                .PutExtra(CalendarContract.Events.InterfaceConsts.AllDay, true)
                .PutExtra(CalendarContract.ExtraEventBeginTime, begin)
                .PutExtra(CalendarContract.ExtraEventEndTime, end);

            intent.SetFlags(ActivityFlags.NewTask);
            activity.StartActivity(intent);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            return Task.FromResult(false);
        }
    }
}
