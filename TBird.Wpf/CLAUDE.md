# TBird.Wpf

WPF向けMVVMフレームワークとUIコンポーネント集。

## 開発時の注意

- MahApps.Metroベースのスタイリング
- ビヘイビアは`Behaviors/`配下に`{Control}{機能}Behavior.cs`の命名規則で配置
- コレクションはBindableCollection系を使用し、標準のObservableCollectionは使わない
- MVVMビューモデルには`BindableBase`を継承
- ダイアログビューモデルは`DialogViewModel`を継承
