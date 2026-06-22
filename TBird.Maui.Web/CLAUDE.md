# TBird.Maui.Web

AngleSharp 解析ヘルパと HTTP transient エラー処理。

## 開発時の注意

- TFM は `net10.0`（`HttpRequestException.StatusCode` が .NET 5.0+ プロパティのため netstandard2.0 不可。MAUI スタックの TFM 統一のため net10.0）
- `AngleSharpHelper.ParseAsync(html, ct)` は `Configuration.Default` + `BrowsingContext.New` + `OpenAsync(req => req.Content(html))` の 3 行パターンを集約
- `TransientHttpErrorHelper.IsTransient` の判定: 5xx / 408 / 429 / ステータスなし (DNS / SSL / ソケット層) を transient とみなす（4xx クライアントエラーはリトライ不要）
- `HttpRequestFailureLogger.Log` は InnerException チェーンを最大 5 段まで出力（Android の `HttpRequestException.Message` は "Connection failure" のような抽象的文字列だけだと真の原因が見えないため）
- ログは TBird.Core の `MessageService.Error` 経由（呼出元情報は `[CallerMemberName]` 等で自動取得）
