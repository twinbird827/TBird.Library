namespace TradeAnalyzer.Data.Options;

/// <summary>EDINET API v2 接続設定。Subscription-Key は user-secrets で注入。</summary>
public class EdinetOptions
{
    public const string SectionName = "Edinet";

    /// <summary>APIベースURL。</summary>
    public string BaseUrl { get; set; } = "https://api.edinet-fsa.go.jp/api/v2/";

    /// <summary>サブスクリプションキー（クエリ `Subscription-Key`。user-secrets `Edinet:SubscriptionKey`）。</summary>
    public string? SubscriptionKey { get; set; }
}
