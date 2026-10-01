# coretest

TBird.Core の実行可能テストアプリケーション（ユニットテストではなく、`Program.cs` で機能を実行確認する形式）。

## 開発時の注意

- .NET Framework 4.8 のレガシー形式 csproj（非 SDK 形式、`ToolsVersion="15.0"`）。`dotnet run` は使えないため、**Visual Studio で実行**するか `msbuild` でビルドして `bin\Debug\coretest.exe` を実行する
- 参照: TBird.Core のみ
- 検証対象は `Program.cs` に直書き（MessageService / 拡張メソッド / TBirdObject 等）。新規にテストしたい機能は `Program.cs` に追記する
