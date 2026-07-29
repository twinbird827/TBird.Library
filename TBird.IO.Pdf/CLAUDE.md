# TBird.IO.Pdf

GhostScriptを使用したPDF操作ユーティリティ（TFM: `net8.0-windows`、`OutputType=Exe`）。

## アーキテクチャ

- `PdfUtil`（static ファサード）が公開 API。内部は `IPdfUtil` 実装を**独立した exe プロセス**（`PdfUtilWrapper` / `PdfUtilExecutor`）として起動し GhostScript を呼ぶ（プロセス分離）
- 公開 API（`PdfUtil`）:
  - `int GetPageSize(string pdffile)` — ページ数取得
  - `async Task Pdf2Jpg(string pdffile, int parallel, int dpi)` — 全ページを画像化。`parallel` は一度に処理するページ数（バッチサイズ）で、`parallel` ページ単位に分割して並列実行する。出力先は PDF と同名フォルダ
  - `void PutPageNumber(string pdffile)` — フッタにページ番号付与
- 3 操作とも失敗時は `InvalidOperationException`（silent な 0 返却／空フォルダ／フッタ「1/0」による原本置換にはならない）
- `Pdf2Jpg` が失敗した場合、再試行前に出力フォルダを空にすること（成功済みバッチの残骸と `OrganizeNumber` 済み連番が同居し重複混入するため）
- `IPdfUtil`（internal、別プロセス側の実装契約）の `Pdf2Jpg(pdffile, start, end, dpi)` はページ**範囲**指定（公開 API の `parallel` とは引数が異なる点に注意）

## 開発時の注意

- 実行形式（`OutputType=Exe`）のプロジェクト。ライブラリ側 `PdfUtilExecutor` が自プロセスを spawn して GhostScript 処理を隔離する
- GhostScript DLL（`gsdll32/64.dll`）はサイズが大きい（計約26MB）ためGit管理に注意
- `Pdf2Jpg` は `parallel` ページ単位のバッチを async fan-out し、`SemaphoreSlim(Environment.ProcessorCount)` で同時プロセス数を制限。処理後 `DirectoryUtil.OrganizeNumber` で連番整理する
- 別プロセス起動は `Assembly.GetExecutingAssembly().Location` から `.exe` を spawn（exe 名は DLL ベース名と一致が前提）。引数は stdout 経由でやり取り、`KEY_DATA`（`TBird.IO.Pdf.PdfUtil`）で自プロセス実行を判定
