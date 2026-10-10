# CLAUDE.md

このファイルは `_Apps` フォルダ（app-white-copy ブランチ）のガイド。

## WhiteCopy

画像ファイルを白一色で上書きする D&D ツール（コンソールアプリ、.NET 8 / Magick.NET）。
exe に画像ファイルまたはフォルダをドラッグ＆ドロップして使う。見開き位置の調整用に blank を作るため、引数の種類で対象を絞る。

- 単一ファイル: そのファイルだけを白塗りする
- ファイル 2 つ以上、またはファイルとフォルダの混在: 何も変更せずエラー終了する（終了コード 1）
- フォルダ（複数可）: 各フォルダ直下の対象画像をエクスプローラーと同じ名前順（`StrCmpLogicalW`）に並べ、2 番目だけを白塗りする。2 枚未満のフォルダはスキップ

- 対象拡張子: `.jpg` `.jpeg` `.png` `.webp` `.bmp` `.gif` `.tif` `.tiff`
- 処理前に原本のバックアップを同フォルダへ作成する。命名は初回 `name - copy.ext`、衝突時のみ `name - copy (2).ext` 以降の連番（`Program.cs` の `UniquePath`）
- バックアップ名パターンに一致するファイルは二重処理防止のためスキップ
- 白塗り時に `image.Strip()` で EXIF 等のプロファイル（サムネイル・GPS・撮影日時）も除去
- ビルド成果物は PostBuild で `_Tools\WhiteCopy\` へコピーされる

## ビルド

```bash
dotnet build _Apps/App.sln
```
