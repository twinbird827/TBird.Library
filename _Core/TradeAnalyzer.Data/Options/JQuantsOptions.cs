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
    /// 全要求を直列化する際の最低呼出間隔（秒）。Standard は 120 件/分だが /v2/fins/summary は
    /// 専用上限 60 件/分のため 1 秒。Light（60 件/分）も満たす。
    /// </summary>
    public int MinIntervalSeconds { get; set; } = 1;
}
