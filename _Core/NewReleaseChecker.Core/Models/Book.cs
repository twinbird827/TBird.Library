using SQLite;

namespace NewReleaseChecker.Core.Models;

/// <summary>
/// 巻。ItemNumber（Kobo ITEM番号）が全体 UNIQUE な同定キー。
/// ユーザーフラグ列（IsPurchased / IsFavorite / IsCalendarRegistered / IsNewDetected / DetectedAt）は
/// 書誌更新で絶対に上書きしない（要件 §3.2.5）。
/// </summary>
[Table("Book")]
public sealed class Book
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>FK→Series.Id。発掘導線の単発お気に入り巻は NULL。</summary>
    [Indexed]
    public int? SeriesId { get; set; }

    /// <summary>Kobo ITEM番号（同定キー、UNIQUE）。</summary>
    [Unique]
    public string ItemNumber { get; set; } = string.Empty;

    /// <summary>ISBN（補助情報。Kobo 検索 API は返さないため通常 NULL）。</summary>
    public string? Isbn { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }

    /// <summary>発売日（ISO8601 "yyyy-MM-dd" に正規化。パース不能時 NULL）。</summary>
    [Indexed]
    public string? ReleaseDate { get; set; }

    public string? ImageUrl { get; set; }
    public string? ItemUrl { get; set; }
    public string? Caption { get; set; }

    // ----- ユーザーフラグ列（0/1。書誌更新で上書き禁止） -----
    public int IsPurchased { get; set; }
    public int IsFavorite { get; set; }
    public int IsCalendarRegistered { get; set; }

    /// <summary>未通知の予約新刊があるか（内部用）。予約検知時のみ 1。通知発行で 0 に降ろす。</summary>
    public int IsNewDetected { get; set; }

    /// <summary>検知日時（ISO8601）。</summary>
    public string? DetectedAt { get; set; }
}
