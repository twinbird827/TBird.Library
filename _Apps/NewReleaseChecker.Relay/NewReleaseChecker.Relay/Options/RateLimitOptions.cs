namespace NewReleaseChecker.Relay.Options;

/// <summary>
/// 受信側レート制限の設定（"RateLimit" セクション。appsettings.json 由来）。
/// クライアントIP単位の FixedWindow。クライアント暴発時の粗い保険であり、上流429の抑止は目的としない。
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>ウィンドウあたりの許可リクエスト数。</summary>
    public int PermitLimit { get; set; } = 2;

    /// <summary>ウィンドウ秒数。</summary>
    public int WindowSeconds { get; set; } = 1;
}
