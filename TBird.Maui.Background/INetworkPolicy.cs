using System;

namespace TBird.Maui.Background;

/// <summary>
/// ネットワーク接続ポリシーの抽象。
/// <see cref="PriorityJobQueue{TJob, TKey}"/> 内部の WiFi ゲート判定とイベント購読に使う。
///
/// 【境界規約】当面は PriorityJobQueue 内部の DI 接合専用とし、アプリ層 (Repository /
/// Service / ViewModel) が INetworkPolicy を直接コンストラクタ DI で受け取ることは禁止
/// （具象 NetworkPolicyService 経由のみ）。
/// </summary>
public interface INetworkPolicy
{
    bool IsOnline { get; }
    bool IsWifiConnected { get; }

    event EventHandler? WifiConnected;
    event EventHandler? WifiDisconnected;
}
