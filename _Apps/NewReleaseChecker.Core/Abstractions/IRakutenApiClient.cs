using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.Core.Abstractions;

/// <summary>楽天Kobo電子書籍検索 API / ジャンル検索 API クライアント。</summary>
public interface IRakutenApiClient
{
    /// <summary>
    /// タイトルキーワードで検索（新刊チェック・シリーズ既刊収集）。
    /// 1 シリーズキー＝1 検索。レート制限は実装側（SiteRateLimiter）で担保する。
    /// </summary>
    Task<IReadOnlyList<RakutenBook>> SearchByKeywordAsync(string keyword, CancellationToken ct = default);

    /// <summary>汎用検索（発売予定表・ランキング・シリーズ検索画面）。</summary>
    Task<IReadOnlyList<RakutenBook>> SearchAsync(RakutenSearchQuery query, CancellationToken ct = default);

    /// <summary>ジャンル階層を取得（発売予定表・ランキングの絞り込みメニュー生成）。</summary>
    Task<RakutenGenreNode> GetGenreAsync(string koboGenreId, CancellationToken ct = default);
}
