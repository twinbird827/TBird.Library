# TBird.Windows

Windows 専用 Win32 ラッパー（TBird.Core から分離）。

## ファイル構成（フラット構成、`_ROOT/` は無し）

- `Win32Methods.cs` — User32 / Kernel32 / Shell32 / GDI32 等の DllImport 定義。電源系操作（`Win32Shutdown` / `Win32Logoff` / `Win32Reboot` / `Win32PowerOff`）と `DCSafeHandle`（DC ハンドルの `SafeHandle` ラッパー）もここに含む
- `Win32Messages.cs` — WM_* / EM_* 等の Win32 メッセージ定数
- `Win32ShutdownFlags.cs` — シャットダウン用フラグ enum

## 開発時の注意

- TFM は `net8.0-windows`（`System.Management 10.0.1` 依存のため）
- `Win32Methods` クラスに User32 / Kernel32 / Shell32 / GDI32 等の DllImport を集約
- `Win32Messages` クラスに WM_* / EM_* 等の Win32 メッセージ定数を集約
- `Win32Shutdown` / `Win32Logoff` / `Win32Reboot` / `Win32PowerOff` は `System.Management` 経由で WMI を叩く（要管理者権限）。WMI 操作は STA スレッド上で実行する（`SetApartmentState(STA)`）
- `Win32Messages` 定数は `SendMessage` / `PostMessage` と組み合わせて使う
- DC ハンドルの安全な解放には `DCSafeHandle` を使用
- このプロジェクトは TBird.Core に依存するが、TBird.Core からは参照しない（依存方向を逆転させた）
