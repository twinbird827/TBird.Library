namespace TradeAnalyzer.Data.Options;

/// <summary>J-Quants V2 接続設定。APIキーは user-secrets で注入。</summary>
public class JQuantsOptions
{
    public const string SectionName = "JQuants";

    /// <summary>APIベースURL。</summary>
    public string BaseUrl { get; set; } = "https://api.jquants.com";

    /// <summary>APIキー（`x-api-key` ヘッダ。user-secrets `JQuants:ApiKey`）。</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 全要求を直列化する際の最低呼出間隔（秒）。Free=5req/分=12秒上限のため、
    /// 安全マージンを見て既定13秒（12秒は上限ちょうどでマージン0）。
    /// </summary>
    public int MinIntervalSeconds { get; set; } = 13;
}
