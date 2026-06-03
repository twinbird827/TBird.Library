using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.App.Models;

/// <summary>
/// 巻の一覧行（SCR-005/007/008/009 共用）。
/// 永続巻は BookId を持ち、ランキング/発売予定表の非永続巻は Source（RakutenBook）を持つ。
/// </summary>
public sealed class BookListItem
{
    /// <summary>永続巻の Book.Id（非永続なら null）。</summary>
    public int? BookId { get; init; }

    /// <summary>非永続巻（ランキング/発売予定表）の元データ。</summary>
    public RakutenBook? Source { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string? Publisher { get; init; }
    public string? ImageUrl { get; init; }
    public string ReleaseDisplay { get; init; } = string.Empty;

    /// <summary>シリーズ名（未所属の単発巻は「未追跡」）。</summary>
    public string SeriesName { get; init; } = string.Empty;

    public bool IsPurchased { get; init; }

    /// <summary>カレンダー未登録バッジ（未発売かつ未登録）。</summary>
    public bool ShowCalendarBadge { get; init; }

    /// <summary>ランキング順位（ランキング以外は null）。</summary>
    public int? Rank { get; init; }

    public bool HasRank => Rank.HasValue;
}
