# TBird.IO

IO操作およびWebView2によるHTML解析ユーティリティ（TFM: `net8.0-windows`）。

## 主要クラス

- `HeadlessWebView2`（`Html/`, static）: `Call(Uri uri, Func<WebView2, Task> func)` — ヘッドレスで URI を開き、ナビゲーション完了後に `func` を実行する

## 開発時の注意

- WebView2 ランタイムがインストールされている必要あり
- `HeadlessWebView2.Call` は内部で **STA スレッド + Dispatcher** を起動して WebView2 を駆動する（WebView2 は STA 必須）。`await HeadlessWebView2.Call(uri, async view => { ... })` の形で使用
- API は async/await 対応。処理完了後に Dispatcher は自動シャットダウンされる
