# CLAUDE.md

このファイルは `_Apps` フォルダ（app-ebook2pdf ブランチ）のガイド。

## EBook2PDF

電子書籍（azw / epub / htmlz / 展開済み HTML フォルダ）を D&D で縦書き PDF にするコンソールアプリ。Calibre と WebView2 を使う。

- 配備は `_Apps/deploy.ps1` を実行して `_Tools\EBook2PDF\` を Release の publish 出力で入れ替え（`app-setting.json` と `log` は残す）、`_Tools\EBook2PDF\EBook2PDF.exe` を D&D 先にする（ビルドでは配備しない）

## ビルド

```bash
dotnet build _Apps/App.sln
# 配備 (_Tools\EBook2PDF\ を Release の publish 出力で入れ替える。app-setting.json と log は残す)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File _Apps/deploy.ps1
```
