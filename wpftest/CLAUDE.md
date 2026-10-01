# wpftest

TBird.Wpf の実行可能テストアプリケーション（DB関連 SQLite / SQLServer の検証も含む）。

## 開発時の注意

- モダン WPF（`net8.0-windows`, `UseWPF`, SDK形式 csproj）。.NET Framework ではない
- 実行: `dotnet run --project wpftest`（Windows）または Visual Studio
- 参照: TBird.Core / TBird.Wpf / TBird.DB / TBird.DB.SQLite / TBird.DB.SQLServer
- スタイリングは MahApps.Metro。`MainViewModel.cs` / `MainWindow.xaml` が起点
