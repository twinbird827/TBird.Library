# TBird.Wpf

WPF向けMVVMフレームワークとUIコンポーネント集（TFM: `net8.0-windows`, `UseWPF` + `UseWindowsForms`）。

## ディレクトリ構成

- `_ROOT/` - MVVM基盤: `BindableBase`（ViewModel基底）, `IBindable`, `RelayCommand` / `IRelayCommand`（ICommand実装）, `TaskManager` / `TaskViewModel`, `Combobox*`
- `Controls/` - `MainViewModelBase`, `WindowViewModel`, `DialogViewModel`（ダイアログ基底）, `WpfMessageService`（`IMessageService` のWPF実装）, メッセージ／入力ウィンドウ
- `Collections/` - `BindableCollection` とその派生（Sorted / Where / Select / Distinct / Context / Child）。`IBindableCollection`
- `Converters/` - 10種の `IValueConverter`（Boolean2Visibility / Boolean2Enum / Enum2String / Null2Boolean / Type2Boolean / ValueConverterGroup 等）
- `Behaviors/` - `{Control}Behavior_{機能}.cs` 命名のビヘイビア群（例: `ButtonBehavior_ClearFocus.cs`, `FrameworkElementBehavior_DragDrop.cs`, `WindowBehavior_Closing.cs`）
- `Utils/` - `WpfUtil`（Dispatcher経由実行）, `WpfToast`, `WpfDialog`, `BehaviorUtil`, `ControlUtil`
- `Reports/` - `ReportViewModel` / `ReportSetting`
- `Styles/` - MahApps.Metro ベースの XAML リソース

## 開発時の注意

- スタイリングは MahApps.Metro（2.4.11）+ IconPacks.Material ベース
- `UseWindowsForms=true` は `WpfDialog`（WinForms の `FolderBrowserDialog` 等）連携のため必須。依存: `System.Drawing.Common`, `Microsoft.Toolkit.Uwp.Notifications`（トースト）。`TBird.Core` / `TBird.Windows` を参照
- ViewModel は `BindableBase` を継承、ダイアログ ViewModel は `DialogViewModel` を継承
- コレクションは `BindableCollection` 系を使用し、標準の `ObservableCollection` は使わない（スレッド安全・Dispatcher対応のため）
- バックグラウンドスレッドからUI更新する場合は `WpfUtil` の Dispatcher 経由メソッドを使う（`BindableCollection` も内部で同様に処理）
- ICommand は `RelayCommand` を使用
