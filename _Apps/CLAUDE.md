# CLAUDE.md

このファイルは `_Apps` フォルダ（app-zipconverter ブランチ）のガイド。

## ZIPConverter

画像フォルダを D&D すると、画像を縮小・連番化して同じ場所に同名の ZIP を作り、元フォルダを消すコンソールアプリ。SkiaSharp を使う。

- 配備は `_Apps/deploy.ps1` を実行して `_Tools\ZIPConverter\` を Release の publish 出力で入れ替え（`app-setting.json` と `log` は残す）、`_Tools\ZIPConverter\ZIPConverter.exe` を D&D 先にする（ビルドでは配備しない）

## ビルド

```bash
dotnet build _Apps/App.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -File _Apps/deploy.ps1
```
