using System;
using Microsoft.Maui.Networking;
using TBird.Core;

namespace TBird.Maui.Background;

// [DI-LIFETIME: SINGLETON]
// AddTransient/AddScoped causes ConnectivityChanged handler leak because
// this class subscribes in the ctor without unsubscribe (no IDisposable).
/// <summary>
/// <see cref="INetworkPolicy"/> を Microsoft.Maui.Networking.Connectivity で実装する。
///
/// 【ライフタイム】Singleton 前提。コンストラクタで Connectivity.ConnectivityChanged を
/// 購読し、解除コードは持たない（プロセス終了時の GC に委ねる）。
/// AddTransient / AddScoped 登録は禁止（ハンドラリーク）。
///
/// 例外ガードを維持: MAUI Essentials が MainActivity 初期化前に呼ばれるケースや一部 Android
/// 端末で Connectivity.Current が例外を投げる事象への防御として、購読・getter を try-catch
/// で囲む。リファクタの「クリーンアップ」名目で削ってはならない（起動時クラッシュ防止）。
/// </summary>
public class MauiNetworkPolicy : INetworkPolicy
{
    public MauiNetworkPolicy()
    {
        try
        {
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }
        catch (Exception ex)
        {
            MessageService.Warn($"Failed to hook ConnectivityChanged: {ex.Message}");
        }
    }

    public event EventHandler? WifiConnected;
    public event EventHandler? WifiDisconnected;

    public bool IsOnline
    {
        get
        {
            try { return Connectivity.Current.NetworkAccess == NetworkAccess.Internet; }
            catch (Exception ex)
            {
                MessageService.Warn($"IsOnline check failed: {ex.Message}");
                return false;
            }
        }
    }

    public bool IsWifiConnected
    {
        get
        {
            try
            {
                return Connectivity.Current.NetworkAccess == NetworkAccess.Internet
                    && Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
            }
            catch { return false; }
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var isWifi = e.NetworkAccess == NetworkAccess.Internet
            && e.ConnectionProfiles.Contains(ConnectionProfile.WiFi);
        if (isWifi) WifiConnected?.Invoke(this, EventArgs.Empty);
        else WifiDisconnected?.Invoke(this, EventArgs.Empty);
    }
}
