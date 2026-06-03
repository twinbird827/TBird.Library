namespace NewReleaseChecker.Data.Api;

/// <summary>
/// 商品URL生成（要件 §6.5 / §7.6）。アフィリエイトID が空なら通常URL、値があればアフィリエイトURLを生成する。
/// </summary>
public static class UrlBuilder
{
    /// <summary>
    /// Book.ItemUrl とアフィリエイトID から「Koboで購入」用 URL を生成する。
    /// affiliateId が空なら itemUrl をそのまま返す。
    /// </summary>
    public static string GetProductUrl(string? itemUrl, string? affiliateId)
    {
        if (string.IsNullOrEmpty(itemUrl)) return string.Empty;
        if (string.IsNullOrEmpty(affiliateId)) return itemUrl;

        var enc = Uri.EscapeDataString(itemUrl);
        // 楽天アフィリエイトのリンク変換（hb.afl.rakuten.co.jp）
        return $"https://hb.afl.rakuten.co.jp/hgc/{affiliateId}/?pc={enc}&m={enc}";
    }
}
