namespace NewReleaseChecker.Relay.Options;

/// <summary>
/// 楽天ウェブサービス連携の設定。非機密値（URL・ドメイン・タイムアウト）は appsettings.json、
/// 機密値（<see cref="ApplicationId"/> / <see cref="AccessKey"/>）は appsettings.Secrets.json から
/// いずれも "Rakuten" セクションでバインドされる（構成のマージで合成）。
/// </summary>
public sealed class RakutenOptions
{
    public const string SectionName = "Rakuten";

    /// <summary>楽天アプリケーションID（2026 新仕様の UUID 形式。Secrets 由来。クエリ必須）。</summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>楽天アクセスキー（2026 新仕様で必須化。Secrets 由来。本サーバーはクエリで付与）。</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>上流（楽天API）のベースURL。例: https://openapi.rakuten.co.jp</summary>
    public string UpstreamBaseUrl { get; set; } = "https://openapi.rakuten.co.jp";

    /// <summary>Kobo 電子書籍検索 API のパス（バージョン 20170426）。</summary>
    public string SearchPath { get; set; } = "/services/api/Kobo/EbookSearch/20170426";

    /// <summary>Kobo ジャンル検索 API のパス（バージョン 20131010。電子書籍検索とは版が異なる点に注意）。</summary>
    public string GenrePath { get; set; } = "/services/api/Kobo/GenreSearch/20131010";

    /// <summary>Referer / Origin に付与する出所ドメイン（末尾スラッシュなし）。「許可されたWebサイト」と一致必須。</summary>
    public string OriginDomain { get; set; } = "https://kaz.server-on.net";

    /// <summary>上流へのタイムアウト秒数。HttpClient.Timeout ではなく CancellationToken の CancelAfter に使う。</summary>
    public int UpstreamTimeoutSeconds { get; set; } = 15;
}
