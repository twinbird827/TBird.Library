# TBird.Plugin

動的DLL読み込みによるプラグインシステム。

## 開発時の注意

- プラグイン作成：`IPlugin`を実装 → DLLビルド → "plugins"ディレクトリに配置 → `PluginManager`が自動検出
