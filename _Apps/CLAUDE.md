# CLAUDE.md

このファイルは `_Apps` フォルダ（app-dojin-name ブランチ）のガイド。

## DojinRename

フォルダを D&D すると、直下の各ファイル名から `[...]`・`(...)` と前後の空白（全角含む）を取り除いてリネームするコンソールアプリ。

- 配備は `_Apps/deploy.ps1` を実行して `_Tools\DojinRename\` を Release の publish 出力で入れ替え（`log` は残す）、`_Tools\DojinRename\DojinRename.exe` を D&D 先にする（ビルドでは配備しない）

## ビルド

```bash
dotnet build _Apps/App.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -File _Apps/deploy.ps1
```
