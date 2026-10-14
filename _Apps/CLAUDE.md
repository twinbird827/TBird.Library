# _Apps - Moviewer (app-moviewer ブランチ)

## 概要

ニコニコ動画とYouTubeの動画を横断的に検索・閲覧・管理するWPFアプリケーション。

## 配備

`_Apps/deploy.ps1` を実行して `_Tools\Moviewer\` を Release の publish 出力で入れ替え（`lib`・`cache`・`log` は残す）、`_Tools\Moviewer\Moviewer.exe` から起動する。ビルドでは配備しない。

## ビルド

```bash
dotnet build /c/Work/Github/TBird.Library/_Apps/App.sln
# 配備 (_Tools\Moviewer\ を Release の publish 出力で入れ替える)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File _Apps/deploy.ps1
```

ターゲット: .NET 8.0 Windows (WPF)
