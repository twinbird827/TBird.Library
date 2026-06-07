# TBird.Maui

Android MAUI アプリ共通基盤（ViewModel / Converter / 通知許可 / メッセージサービス）。

## 開発時の注意

- TFM は `net9.0-android`、`<UseMaui>true</UseMaui>` で MAUI コアを有効化
- `Microsoft.Maui.Controls` は明示的に PackageReference する（純粋クラスライブラリでは auto FrameworkReference が不安定）
- `Microsoft.Maui.Controls.Compatibility` は意図的に含めない（lib 依存膨張防止）
- `ErrorAwareViewModel` は `[ObservableProperty]` で `HasError` / `ErrorMessage` を公開する基底 ViewModel（CommunityToolkit.Mvvm の partial class + source generator を使用）
- `NotificationPermissionService<TPermission>` は `Permissions.BasePlatformPermission` 派生型を型パラメータで受け取り、アプリ側で `PostNotificationsPermission` 等を渡す
- `MauiMessageService` は `ConsoleMessageService` を継承し全 5 メソッド (Error/Exception/Info/Warn/Debug) を override（Android logcat は `Trace.WriteLine` を拾わないため `System.Diagnostics.Debug.WriteLine` 明示）
- ファイル出力先は `FileSystem.AppDataDirectory/log/yyyy-MM-dd.log`（Error/Exception のみ）
- 通知許可ダイアログは `Shell.Current.DisplayAlert` 直接呼出のため、消費アプリは Shell ナビゲーション構成前提
- `Converters/` に 5 種の `IValueConverter`: `BoolToColorConverter` / `BoolToOpacityConverter` / `HasValueConverter` / `IntToBoolConverter` / `InverseBoolConverter`（XAML バインディング用、いずれも `IValueConverter` 実装）
