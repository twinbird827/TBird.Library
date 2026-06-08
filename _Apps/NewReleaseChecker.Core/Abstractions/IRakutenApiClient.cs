using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.Core.Abstractions;

/// <summary>楽天Kobo電子書籍検索 API / ジャンル検索 API クライアント。</summary>
public interface IRakutenApiClient
{
    /// <summary>
    /// タイトルキーワードで検索（新刊チェック・シリーズ既刊収集）。
    /// 1 シリーズキー＝1 検索。レート制限は実装側（SiteRateLimiter）で担保する。
    /// koboGenreId を指定すると当該ジャンル（とその配下）に絞り込む（本編とコミカライズの弁別／件数枠の節約）。
    /// ngKeyword を指定すると当該キーワードを含む商品を検索結果から除外する（分冊版等で件数枠を埋めないため）。
    /// </summary>
    Task<IReadOnlyList<RakutenBook>> SearchByKeywordAsync(string keyword, string? koboGenreId = null, string? ngKeyword = null, CancellationToken ct = default);

    /// <summary>汎用検索（発売予定表・ランキング・シリーズ検索画面）。</summary>
    Task<IReadOnlyList<RakutenBook>> SearchAsync(RakutenSearchQuery query, CancellationToken ct = default);

    /// <summary>ジャンル階層を取得（発売予定表・ランキングの絞り込みメニュー生成）。</summary>
    Task<RakutenGenreNode> GetGenreAsync(string koboGenreId, CancellationToken ct = default);
}
