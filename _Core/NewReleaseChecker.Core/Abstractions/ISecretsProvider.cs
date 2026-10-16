namespace NewReleaseChecker.Core.Abstractions;

/// <summary>
/// 秘密情報へのアクセスを抽象化する。実装は App 層の Secrets（ビルド時に _Tools/NewReleaseChecker/appsettings.Secrets.json から生成。コミットしない）。
/// </summary>
public interface ISecretsProvider
{
    /// <summary>
    /// 楽天API中継サーバー（NewReleaseChecker.Relay）との共有シークレット。X-Relay-Auth ヘッダで送信。
    /// 中継サーバーと同じ appsettings.Secrets.json の RelayAuth:SharedSecret を読む。
    /// </summary>
    string RelayServerApiKey { get; }

    /// <summary>宅内でヘアピン NAT が効かないときの接続先。中継サーバー機の LAN IP と IIS の内部ポート（"&lt;IP&gt;:&lt;ポート&gt;"）。</summary>
    string RelayLanEndPoint { get; }

}
