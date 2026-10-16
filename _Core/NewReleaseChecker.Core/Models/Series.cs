using SQLite;

namespace NewReleaseChecker.Core.Models;

/// <summary>
/// 追跡シリーズ。タイトル語（SeriesKey）と正規化済み著者集合（AuthorSet）で同定する。
/// 巻数・種別フラグは持たない（要件 §3.2）。
/// </summary>
[Table("Series")]
public sealed class Series
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>追跡キー（シリーズ名）。登録時にタイトルから抽出、ユーザー編集可。</summary>
    public string SeriesKey { get; set; } = string.Empty;

    /// <summary>著者集合（正規化済み・改行区切り保存）。同定の集合一致判定に使う。</summary>
    public string AuthorSet { get; set; } = string.Empty;

    /// <summary>"novel" / "comic"。</summary>
    public string MediaType { get; set; } = Core.MediaType.Novel;

    /// <summary>登録日時（ISO8601）。</summary>
    public string RegisteredAt { get; set; } = string.Empty;

    /// <summary>最終チェック日時（ISO8601）。未チェックは NULL。</summary>
    public string? LastCheckedAt { get; set; }
}
