namespace NewReleaseChecker.Relay.Services;

/// <summary>楽天 Kobo API への透過プロキシ抽象。将来のテスト時にモック差し替え可能にする。</summary>
public interface IRakutenProxy
{
    /// <summary>
    /// クライアント本文由来のクエリにサーバー保持の楽天認証情報を上書き付与し、指定パスの楽天 API へ
    /// GET 転送、レスポンス（ステータス・Content-Type・本文）を <paramref name="httpContext"/> へ透過する。
    /// </summary>
    /// <param name="upstreamPath">
    /// 楽天 API のパス。電子書籍検索 = /services/api/Kobo/EbookSearch/20170426、
    /// ジャンル検索 = /services/api/Kobo/GenreSearch/20131010（バージョン番号が異なる点に注意）。
    /// </param>
    /// <param name="queryFromClient">クライアント JSON 本文由来のクエリ（認証系キーは無視・上書きされる）。</param>
    /// <param name="httpContext">レスポンス書き戻し先。RequestAborted で切断検出も行う。</param>
    /// <param name="ct">呼出側のキャンセルトークン（通常 RequestAborted）。</param>
    Task ProxyAsync(
        string upstreamPath,
        IDictionary<string, string?> queryFromClient,
        HttpContext httpContext,
        CancellationToken ct);
}
