namespace NewReleaseChecker.Core.Abstractions;

/// <summary>
/// 秘密情報へのアクセスを抽象化する。実装は App 層の Secrets（.gitignore 対象でコミットしない）。
/// </summary>
public interface ISecretsProvider
{
    /// <summary>
    /// 楽天API中継サーバー（NewReleaseChecker.Relay）との共有シークレット。X-Relay-Auth ヘッダで送信。
    /// 中継サーバー側 appsettings.Secrets.json の RelayAuth:SharedSecret と一致させること。
    /// </summary>
    string RelayServerApiKey { get; }
}
