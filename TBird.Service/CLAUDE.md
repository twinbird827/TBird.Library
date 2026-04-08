# TBird.Service

Windowsサービス基盤（コンソールフォールバック付き）。

## 開発時の注意

- .NET Framework 4.8のレガシー形式csproj（SDK形式でない）
- コンソール実行とWindowsサービス実行の両方に対応
- 自己インストール機能あり（/i:インストール、/u:アンインストール）
