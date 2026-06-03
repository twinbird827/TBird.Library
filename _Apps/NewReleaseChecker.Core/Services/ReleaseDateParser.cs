using System.Globalization;
using System.Text.RegularExpressions;

namespace NewReleaseChecker.Core.Services;

/// <summary>
/// 楽天 API の発売日文字列を ISO8601（yyyy-MM-dd）へ正規化する（要件 §5.6）。
/// 「2026年03月15日」「2026-03-15」等に対応。年月のみ/未定/パース不能は NULL。
/// </summary>
public static class ReleaseDateParser
{
    private static readonly Regex YmdRegex = new(@"(\d{4})\D+(\d{1,2})\D+(\d{1,2})", RegexOptions.Compiled);

    /// <summary>API の発売日文字列を ISO8601 へ。日まで特定できない/パース不能なら NULL。</summary>
    public static string? ToIso(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim().Replace("頃", "").Replace("予定", "").Trim();

        var m = YmdRegex.Match(s);
        if (m.Success)
        {
            return BuildIso(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? BuildIso(string y, string mo, string d)
    {
        if (int.TryParse(y, out var yy) && int.TryParse(mo, out var mm) && int.TryParse(d, out var dd))
        {
            try { return new DateTime(yy, mm, dd).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
            catch { return null; }
        }
        return null;
    }

    /// <summary>ISO 文字列を DateTime へ。失敗時 null。</summary>
    public static DateTime? Parse(string? iso)
        => DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>未来日（＝予約）か。NULL は予約判定対象外（false）。</summary>
    public static bool IsFuture(string? iso, DateTime now)
    {
        var d = Parse(iso);
        return d.HasValue && d.Value.Date > now.Date;
    }
}
