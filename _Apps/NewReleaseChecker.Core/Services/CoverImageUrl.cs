using System.Globalization;

namespace NewReleaseChecker.Core.Services;

/// <summary>書影 URL を表示用に変換する。</summary>
public static class CoverImageUrl
{
    /// <summary>
    /// 発売日当日まで（未定を含む）は日付クエリでキャッシュキーを日替わりにし、発売日を過ぎたら元の URL を返す。
    /// </summary>
    public static string? ForDisplay(string? imageUrl, string? releaseIso, DateTime now)
    {
        // 楽天は表紙差し替え後も同じ URL を返し、Android の Glide は URL キーのディスクキャッシュを期限なしで返し続けるため。
        if (string.IsNullOrEmpty(imageUrl)) return imageUrl;
        if (ReleaseDateParser.Parse(releaseIso) is DateTime release && release.Date < now.Date) return imageUrl;
        var separator = imageUrl.Contains('?') ? '&' : '?';
        return $"{imageUrl}{separator}d={now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
    }
}
