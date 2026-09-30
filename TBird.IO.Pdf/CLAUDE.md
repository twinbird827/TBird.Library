# TBird.IO.Pdf

GhostScriptを使用したPDF操作ユーティリティ（TFM: `net8.0-windows`、`OutputType=Exe`）。

## アーキテクチャ

- `PdfUtil`（static ファサード）が公開 API。内部で**自プロセスを別 exe として spawn** し、親側 `PdfUtilExecutor` が起動・子側 `PdfUtilWrapper` が GhostScript を直接呼ぶ（プロセス分離）
- 公開 API（`PdfUtil`）:
  - `async Task Pdf2Jpg(string pdffile, int parallel, int dpi)` — 全ページを画像化。`parallel` は一度に処理するページ数（バッチサイズ）で、`parallel` ページ単位に分割して並列実行する。出力先は PDF と同名フォルダ
  - `Task PutPageNumberAsync(string pdffile)` — フッタにページ番号付与
- `Pdf2Jpg` / `PutPageNumberAsync` とも失敗時は `InvalidOperationException`（空フォルダ／フッタ「1/0」による原本置換にはならない）。`Pdf2Jpg` は子が exit 0 でも出力 JPEG がページ数に満たなければ例外になる。テキスト走査のフォールバック（`GetPageSizeFromPdfText`）でページ数を過大に取った PDF もこの照合で例外になりうる
- `Pdf2Jpg` が失敗した場合、再試行前に出力フォルダを空にすること（成功済みバッチの残骸と `OrganizeNumber` 済み連番が同居し重複混入するため）
- 内部（プロセス間プロトコル）の `Pdf2Jpg(pdffile, start, end, dpi)` はページ**範囲**指定（`start` = 画像化する最初のページ番号、`end` = 最後のページ番号）。公開 API の `parallel`（バッチサイズ）とは引数の意味が異なる点に注意。親側 `PdfUtilExecutor` と子側 `PdfUtilWrapper` は**3 操作（`GetPageSize` / `Pdf2Jpg` / `PutPageNumber`）とも同じ引数・同じ順序**を保つこと（親→子は値がコマンドライン引数の**位置**で渡るため、送信順・個数の食い違いは型検査に掛からない。各呼び出し式そのものはビルドで検査されるが、片側の引数を変えても他方はビルドで落ちない）

## 開発時の注意

- 実行形式（`OutputType=Exe`）のプロジェクト。ライブラリ側 `PdfUtilExecutor` が自プロセスを spawn して GhostScript 処理を隔離する
- GhostScript DLL（`gsdll32/64.dll`）はサイズが大きい（計約26MB）ためGit管理に注意
- `Pdf2Jpg` は `parallel` ページ単位のバッチを async fan-out し、`SemaphoreSlim(Environment.ProcessorCount)` で同時プロセス数を制限。処理後 `DirectoryUtil.OrganizeNumber` で連番整理する
- `Pdf2Jpg` / `PutPageNumberAsync` とも呼び出し元スレッドをブロックしない（子プロセスの起動は `PdfUtil.ExecuteAsync` 内の `Task.Run` で pool へ逃がす）
- 別プロセス起動は `Assembly.GetExecutingAssembly().Location` から `.exe` を spawn（exe 名は DLL ベース名と一致が前提）。引数は stdout 経由でやり取り、`KEY_DATA`（`TBird.IO.Pdf.PdfUtil`）で自プロセス実行を判定
