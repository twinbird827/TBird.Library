using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.App.Models;

/// <summary>表示用フォーマットのヘルパ。</summary>
public static class DisplayFormat
{
    /// <summary>ISO 発売日 → "yyyy/MM/dd"。NULL は「発売日未定」。</summary>
    public static string Release(string? iso)
    {
        var d = ReleaseDateParser.Parse(iso);
        return d.HasValue ? d.Value.ToString("yyyy/MM/dd") : "発売日未定";
    }

    /// <summary>著者集合の保存文字列（改行区切り）→ "A / B"。</summary>
    public static string Authors(string? stored)
        => string.IsNullOrEmpty(stored) ? string.Empty : string.Join(" / ", stored.Split('\n', StringSplitOptions.RemoveEmptyEntries));
}
