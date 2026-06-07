# TBird.IO.Img

SkiaSharpを使用した画像操作ユーティリティ（TFM: `netstandard2.0`）。

## 主要クラス

- `ImgUtil`（static）
  - `ResizeUnder(src, width, height, quality)` — 指定幅・高さを下回るよう縮小
  - `ResizeOver(src, width, height, quality)` — 指定幅・高さを上回るよう拡縮
  - `GetEncodedExtension(path)` — 画像フォーマットの拡張子を返す

## 開発時の注意

- SkiaSharp のネイティブライブラリがプラットフォームに合わせて必要
- **リサイズ結果は常に JPEG 出力**（`{元ファイル名}.jpg`）。`quality` は JPEG 品質（0-100）
- **元ファイルは削除される**（スケール計算後にリサイズ＆別名保存）。ただしスケール=1 かつ入力が既に JPEG の場合は何もせず中断（[ImgUtil.cs:67-75](ImgUtil.cs#L67-L75)）
- スケールはアスペクト比維持で算出: `ResizeUnder` は幅・高さ比の `Min`、`ResizeOver` は `Max` を取り、さらに 1 と比較（拡大しない/縮小しない方向にクランプ）。`quality` は SkiaSharp の JPEG エンコード品質に直接渡る
