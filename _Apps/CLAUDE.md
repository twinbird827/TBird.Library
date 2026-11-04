# CLAUDE.md

このファイルは `_Apps` フォルダ（app-image-grabber ブランチ）のガイド。

## ImageGrabber

指定 URL を Playwright の Chromium で開いてスクロールし、ブラウザが受信した image/* 応答を保存するコンソールアプリ。既定の保存先は `<ダウンロード>\yyyyMMdd_HHmmss`、短辺 200px 未満と SkiaSharp で寸法を読めない形式（SVG・AVIF 等）は保存しない（`--min-side 0` で無効）。

- 配備は `_Apps/deploy.ps1` を実行して `_Tools\ImageGrabber\` を Release の publish 出力で入れ替え、`_Tools\ImageGrabber\ImageGrabber.exe <url>` で起動する（ビルドでは配備しない）
- Chromium は初回起動時に `%LOCALAPPDATA%\ms-playwright` へ自動インストールされる。`Microsoft.Playwright` の版を上げると要求する Chromium の版も変わり、次回起動時に再インストールが走る

## ビルド

```bash
dotnet build _Apps/App.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -File _Apps/deploy.ps1
```
