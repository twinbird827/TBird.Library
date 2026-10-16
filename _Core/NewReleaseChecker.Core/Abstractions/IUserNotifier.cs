namespace NewReleaseChecker.Core.Abstractions;

/// <summary>トースト/スナックバー（手動チェック失敗時など）。実装は CommunityToolkit.Maui（App 層）。</summary>
public interface IUserNotifier
{
    Task ShowToastAsync(string message);
}
