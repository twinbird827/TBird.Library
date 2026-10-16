namespace NewReleaseChecker.Relay.Options;

/// <summary>
/// Android クライアントとの共有シークレット認証の設定（"RelayAuth" セクション。Secrets 由来）。
/// </summary>
public sealed class RelayAuthOptions
{
    public const string SectionName = "RelayAuth";

    /// <summary>共有シークレット。X-Relay-Auth ヘッダの値と定数時間比較する。</summary>
    public string SharedSecret { get; set; } = string.Empty;
}
