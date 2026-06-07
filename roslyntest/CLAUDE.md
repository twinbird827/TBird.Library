# roslyntest

TBird.Roslyn の実行可能テストアプリケーション（`.csx` スクリプト実行の検証）。

## 開発時の注意

- TFM: `net5.0`（SDK形式）。実行: `dotnet run --project roslyntest`
- `Roslyn.csx` / `scripts/roslyn1.csx` / `scripts/roslyn2.csx` がビルド時に出力ディレクトリへコピーされる（`CopyToOutputDirectory=Always`）。`RoslynManager` は `"scripts"` ディレクトリを走査する
- **FluentValidation 不要カルチャー除外設定が csproj に含まれる**（[roslyntest.csproj:37-46](roslyntest.csproj#L37-L46) の `FluentValidationExcludedCultures` + `RemoveTranslationsAfterBuild`）。TBird.Roslyn を使う側の参考例で、ロケールファイルによる出力肥大を防ぐ
