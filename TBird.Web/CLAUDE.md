# TBird.Web

Webスクレイピング・HTTP通信ユーティリティ（TFM: `netstandard2.0`）。

## 主要クラス（`_ROOT/`）

- `WebUtil`（static）: `CreateClient()`（Polly リトライ付き `HttpClient` ファクトリ, ロックで生成）, `Browse(url)`, `GetUrl()` / `ToParameter()`（クエリ組立）
- `TBirdSelenium`: ChromeDriver ラッパー（ヘッドレス対応）
- `WebListener`: `HttpListener` ラッパー（空きポート自動割当）
- `WebImageUtil`: バイトデータのファイルキャッシュ。`GetBytesAsync(key, urls)` は `key` をファイル名にキャッシュ（`cache\bytes\{key}`、`PathSetting` 基準）し、無ければ `urls` を順に試行して最初に成功したものを保存（`Locker` で key 単位ロック）。キーは呼び出し側指定（URL ハッシュではない）。起動時に `CreationTime` が 7 日より古いキャッシュを一度だけ削除。`GetBytesAsync(params urls)` はキャッシュ無しの直接取得
- `WebSetting`: シングルトン設定（ブラウザパス等）
- `ListenerUtil`: ポート関連ユーティリティ

## 開発時の注意

- ChromeDriver のバージョンは Chrome ブラウザと一致させる必要あり（更新時は csproj の `Selenium.WebDriver.ChromeDriver` バージョンを合わせる）
- HTTP は `WebUtil.CreateClient()` 経由を推奨。Polly による指数バックオフのリトライ（5回: 2/4/6/8/10秒）が組み込まれている
