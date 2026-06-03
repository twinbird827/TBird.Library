# 新刊チェッカー Android アプリ 引き継ぎメモ（中継サーバー導入に伴う改修指示）

このドキュメントは、既に進行中の **新刊チェッカー Android アプリ** に対して、**楽天 API 中継サーバーを介して通信する構成**への変更を反映するための引き継ぎ指示です。Claude Code に対し、本書と既存の `新刊チェッカー_要件定義書.md` / `CLAUDE.md` を併せて渡してください。

**前提**: 楽天ウェブサービス 2026 年新仕様により、Android ネイティブからの楽天 API 直接呼び出しは不可能になりました。よって自宅サーバー（Windows + IIS、`kaz.server-on.net:49443`）上に ASP.NET Core で薄い中継サーバーを構築し、Android アプリはそこ経由で楽天 API を利用します。中継サーバー側の要件は別資料 `中継サーバー_要件定義書.md` を参照。

---

## 1. 変更スコープのサマリ

| 項目 | 変更前 | 変更後 |
|---|---|---|
| 楽天 API への直接アクセス | あり | **なし**（中継サーバー経由のみ） |
| API ベース URL | `https://openapi.rakuten.co.jp/...`（楽天直） | `https://kaz.server-on.net:49443/api/kobo/...`（中継サーバー） |
| HTTP メソッド | GET（クエリパラメータ） | **POST**（JSON 本文） |
| 認証 | `applicationId` + `accessKey` をクエリに付与 | **`X-Relay-Auth` ヘッダのみ**（共有シークレット） |
| `applicationId` / `accessKey` の保持 | Android 側 `Secrets` クラス | **中継サーバー側へ移譲**（Android は持たない） |
| Referer / Origin ヘッダ | 不要（直接アクセス想定で問題視せず） | **中継サーバー側で付与**（Android は無関心でよい） |
| レート制限（`SiteRateLimiter`） | 1 シリーズ 1 秒間隔 | **変更なし**（クライアント側でも継続） |
| レスポンス形式 | 楽天 API の JSON | **変更なし**（中継サーバーは透過プロキシ） |

> ⭐ **重要**: 中継サーバーは**透過プロキシ**として実装されるため、楽天 API のレスポンスはそのまま返ってきます。**JSON パース・モデル定義・Series 同定ロジック・新刊判定ロジックなど、既存の Android 側実装は変更不要**です。変わるのは「どこに何を投げるか」だけです。

---

## 2. 既存資料との整合

### 2.1 `新刊チェッカー_要件定義書.md` への上書き
要件定義書本体は維持しつつ、以下のセクションを本書の内容で**上書き／追補**してください。

- **§1.3 利用環境 → 「配置形態: スタンドアロン（端末内完結、サーバー不要）」**
  - **修正**: 「自宅サーバー上の楽天 API 中継サーバー（NewReleaseChecker.Relay）を併用する構成。楽天 API への直接アクセスは行わない。中継サーバーの所在: `https://kaz.server-on.net:49443/`」
- **§6.5 秘密情報管理**
  - **修正**: 「Android 側 `Secrets` クラスは `applicationId` / `accessKey` を**保持しない**。代わりに `RelayServerApiKey`（中継サーバーとの共有シークレット）のみを保持する」
- **§7.1 楽天Kobo電子書籍検索API（メイン）**
  - **修正**: 「直接アクセスではなく、中継サーバー経由（§7.7 参照）」
- **§7.2 楽天Koboジャンル検索API（補助）**
  - **修正**: 同上
- **§7.3 レート制限対策**
  - **追記**: 「中継サーバー側でも 1 秒あたりのリクエスト上限が設けられているため、超過時は 429 が返る。アプリ側 `SiteRateLimiter` でこの状況には基本到達しないが、もし 429 を受信した場合は当該シリーズのチェックをスキップして次回へ繰り越す」
- **§7（新規追加）§7.7 中継サーバー連携仕様**
  - 本書の §3 を要件定義書に転記

### 2.2 `CLAUDE.md` への上書き
- **§2 NuGet パッケージ**: 変更なし
- **§5 既存ライブラリ §「TBird.Maui.Web」**: 引き続き使用（`TransientHttpErrorHelper.IsTransient` などはそのまま中継サーバー応答にも適用可能）
- **§6 秘密情報の取り扱い**: 以下に差し替え（または追加）
  ```csharp
  public interface ISecretsProvider
  {
      // 削除: RakutenApplicationId, RakutenAffiliateId
      string RelayServerApiKey { get; }  // 中継サーバーとの共有シークレット
  }

  internal sealed class Secrets : ISecretsProvider
  {
      public string RelayServerApiKey => "ここに中継サーバー側と同じ値";
  }
  ```
  > アフィリエイトURLの生成は、必要になったら中継サーバー側で `applicationId`/`accessKey` と一緒に `affiliateId` も付与する形に移行する（Android 側は今後もアフィリエイト ID を持たない）

- **§11 やってはいけないこと**: 以下を追加
  - ❌ Android アプリから楽天 API（`openapi.rakuten.co.jp`）に直接アクセスする
  - ❌ Android アプリで楽天 `applicationId` / `accessKey` を保持する

---

## 3. 中継サーバー連携仕様（新規）

要件定義書 §7 に新セクションとして追加する内容です。

### 3.1 中継サーバーへの接続
| 項目 | 内容 |
|---|---|
| ベース URL | `https://kaz.server-on.net:49443` |
| プロトコル | HTTPS（Let's Encrypt 証明書） |
| 認証 | HTTP ヘッダ `X-Relay-Auth: <共有シークレット>` |
| Content-Type | `application/json`（POST 本文用） |

### 3.2 エンドポイント

#### 3.2.1 Kobo 電子書籍検索: `POST /api/kobo/search`
**用途**: F-001 シリーズ検索、F-003/F-004 新刊チェック、F-009 発売予定表、F-011 ランキング

**リクエスト**:
```http
POST /api/kobo/search HTTP/1.1
Host: kaz.server-on.net:49443
X-Relay-Auth: <Secrets.RelayServerApiKey>
Content-Type: application/json

{
  "keyword": "薬屋のひとりごと",
  "hits": 30,
  "sort": "+releaseDate",
  "format": "json",
  "formatVersion": 2
}
```

- **本文の JSON のキー名・値は、楽天 Kobo 電子書籍検索 API のクエリパラメータと 1:1 対応**
- `applicationId` / `accessKey` / `affiliateId` は**送らない**（中継サーバーが付与）
- Referer / Origin / User-Agent も**送らない**（中継サーバーが付与）

**レスポンス**: 楽天 Kobo 電子書籍検索 API の JSON が**そのまま返る**。
既存の DTO・パース処理がそのまま使える。

#### 3.2.2 Kobo ジャンル検索: `POST /api/kobo/genres`
**用途**: F-009/F-011 のジャンルメニュー生成

**リクエスト**:
```http
POST /api/kobo/genres HTTP/1.1
Host: kaz.server-on.net:49443
X-Relay-Auth: <Secrets.RelayServerApiKey>
Content-Type: application/json

{
  "koboGenreId": "000",
  "format": "json",
  "formatVersion": 2
}
```

**レスポンス**: 楽天 Kobo ジャンル検索 API の JSON がそのまま返る。

#### 3.2.3 ヘルスチェック: `GET /healthz`
通常は使わないが、デバッグ・接続確認用。認証不要。返り値 `{"status":"ok"}`。

### 3.3 エラー応答とハンドリング

| HTTP ステータス | 意味 | Android 側の対応 |
|---|---|---|
| 200 | 正常（楽天 API 応答が透過） | 既存の JSON パース処理へ |
| 401 | 共有シークレット不一致 | エラーログ＋ユーザーへ「中継サーバー設定不備」を伝える（通常は実装/設定バグなので発生したらすぐ気づくべき） |
| 429 | 中継サーバー側のレート制限超過 | 該当シリーズをスキップ、次回 Work へ繰り越し |
| 502 | 中継→楽天の接続失敗 | 自動チェックなら無視＋ログ、手動なら「更新に失敗しました」 |
| 504 | 中継→楽天のタイムアウト | 同上 |
| 4xx（その他、楽天 API が返したエラー） | 楽天 API のエラー本文がそのまま返る | 既存の楽天 API エラーハンドリングを流用 |
| 5xx（502/504 以外） | 中継サーバー内部エラー | ログのみ |

**注意**:
- 楽天 API が 4xx/5xx を返した場合、中継サーバーは**ステータスと本文をそのまま透過**します（中継側で 500 にラップしません）。よって既存の楽天 API エラーパースロジックがそのまま動きます
- 502 と 504 は**中継サーバー独自の応答**で、本文は `{"error":"upstream_unreachable"}` / `{"error":"upstream_timeout"}` です

### 3.4 リトライ方針
既存の `TBird.Maui.Web.TransientHttpErrorHelper.IsTransient` は **429 / 5xx / ステータスなし** を transient と判定します。中継サーバー経由でもこの判定はそのまま有効。自動チェックの指数バックオフリトライ（要件 §7.4）はこの判定を使うこと。

---

## 4. 実装変更ポイント（チェックリスト）

Android プロジェクト側で具体的に変更する箇所:

### 4.1 `Secrets` / `ISecretsProvider`
- `RakutenApplicationId`, `RakutenAffiliateId` を**削除**
- `RelayServerApiKey` を**追加**（中継サーバー側と同じ共有シークレットをローカル `Secrets.cs` に記入）

### 4.2 API クライアント（おそらく `NewReleaseChecker.Data/Api/` 配下）
- ベース URL を `https://kaz.server-on.net:49443/` に変更
- 全リクエストを GET から **POST** に変更（クエリ → JSON 本文）
- 全リクエストに `X-Relay-Auth: <RelayServerApiKey>` ヘッダを付与
- `applicationId` / `accessKey` をリクエストに含める処理を**削除**

### 4.3 URL 組み立てロジック
- 楽天 API の URL を組み立てていた箇所（`/services/api/Kobo/...`）を、中継サーバーの2つのパス（`/api/kobo/search`, `/api/kobo/genres`）に置き換え
- クエリパラメータを組み立てていたコードは、JSON ペイロード組み立てに変換

### 4.4 「Koboで購入」リンク
- `Book.ItemUrl` を外部ブラウザで開く部分は**変更なし**（楽天 API 応答にそのまま含まれる）
- アフィリエイト URL 生成（要件 §6.5・§7.6）は当面未使用のままでよい。将来アフィリエイト ID を使うときは「中継サーバー側で `affiliateId` をクエリに付与」する形に拡張する

### 4.5 `SiteRateLimiter` 設定
- 既存の「楽天 API への 1 秒 1 リクエスト」設定は**そのまま維持**。siteKey の値は変更しなくても機能上問題ないが、識別性のため `"relay-kobo"` 等の名称に変えてもよい（任意）

### 4.6 受信レスポンスのパース
- **変更なし**。中継サーバーは透過プロキシなので、既存の楽天 API レスポンス用 DTO・パース処理がそのまま動く

### 4.7 SSL 証明書の信頼
- Let's Encrypt 証明書はパブリック CA なので、Android 端末で**追加設定なしに信頼される**。証明書ピンニングなどは不要

### 4.8 ネットワーク不通時の挙動
- 既存設計（要件 §6.4）と同じく `MauiNetworkPolicy` で接続判定。中継サーバーへの接続失敗時も同じハンドリングで問題なし

---

## 5. 動作確認順序

実装完了後の確認手順:

1. **中継サーバーが先に稼働していること**（`中継サーバー_要件定義書.md` / `中継サーバー_CLAUDE.md` の手順で構築）
2. ブラウザから `https://kaz.server-on.net:49443/healthz` にアクセス → 200 OK
3. `Secrets.cs` の `RelayServerApiKey` が中継サーバー側と一致していることを確認
4. Android アプリから検索を実行（F-001 シリーズ検索）し、楽天 API 応答が中継経由で返ることを確認
5. ログ（中継サーバー側）に受信記録が残っていることを確認
6. 故意に `RelayServerApiKey` を間違えてアプリで検索 → 401 が返り、適切にエラー表示されることを確認

---

## 6. やってはいけないこと（追加）

- ❌ Android アプリから楽天 API（`openapi.rakuten.co.jp`）に直接アクセスする
- ❌ Android アプリで楽天 `applicationId` / `accessKey` を保持する
- ❌ Android アプリで Referer / Origin ヘッダを操作する（中継サーバー任せ）
- ❌ 中継サーバーが返した楽天 API のエラー本文を握り潰し、独自エラーに変換する（既存の楽天 API エラーハンドリングを活用するため、透過させる）

---

## 7. 関連資料
- `新刊チェッカー_要件定義書.md`（Android アプリ本体の要件、本書で部分上書き）
- `CLAUDE.md`（Android アプリ実装規約、本書で部分上書き）
- `中継サーバー_要件定義書.md`（中継サーバーの要件）
- `中継サーバー_CLAUDE.md`（中継サーバー実装規約・サーバー構築手順）
