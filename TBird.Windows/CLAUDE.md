# TBird.Windows

Windows 専用 Win32 ラッパー（TBird.Core から分離）。

## 開発時の注意

- TFM は `net8.0-windows`（`System.Management 10.0.1` 依存のため）
- `Win32Methods` クラスに User32 / Kernel32 / Shell32 / GDI32 等の DllImport を集約
- `Win32Messages` クラスに WM_* / EM_* 等の Win32 メッセージ定数を集約
- `Win32Shutdown` / `Win32Logoff` / `Win32Reboot` / `Win32PowerOff` は `System.Management` 経由で WMI を叩く（要管理者権限）
- このプロジェクトは TBird.Core に依存するが、TBird.Core からは参照しない（依存方向を逆転させた）
