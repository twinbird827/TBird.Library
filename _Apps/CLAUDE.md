# CLAUDE.md

このファイルは `_Apps` フォルダ（app-pdf2jpg ブランチ）のガイド。

## PDF2JPG

PDF を D&D するとページごとの JPG を同名フォルダに書き出すコンソールアプリ。Ghostscript（`gsdll64.dll`）を使う。

- 配備は `_Apps/deploy.ps1` を実行して `_Tools\PDF2JPG\` を Release の publish 出力で入れ替え（`app-setting.json` と `log` は残す）、`_Tools\PDF2JPG\PDF2JPG.exe` を D&D 先にする。EBook2PDF も `..\PDF2JPG\PDF2JPG.exe` で呼ぶ（ビルドでは配備しない）

## ビルド

```bash
dotnet build _Apps/App.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -File _Apps/deploy.ps1
```
