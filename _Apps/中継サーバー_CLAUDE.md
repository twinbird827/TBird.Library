# CLAUDE.md（楽天API中継サーバー）

このファイルは Claude Code が **新刊チェッカー楽天API中継サーバー（NewReleaseChecker.Relay）** を実装する際に常時参照する規約ファイルです。
**何を作るか**は `中継サーバー_要件定義書.md` を正とし、本ファイルは**どう作るか・どこに何があるか・サーバー構築手順**を定義します。両者が食い違う場合、ライブラリ・実装詳細・コーディング規約は本ファイルを優先してください。

> ℹ️ **記入状況**: NuGet バージョンは実装時に最新安定版を確認のうえ更新すること。残る開発者手作業は **§5 秘密情報の実値配置**（`appsettings.Secrets.json` をローカル/サーバーに作成、Git 管理外）と、**§8〜10 のサーバー構築（IIS・証明書・楽天アプリ登録）**。

---

## 1. プロジェクト概要（要約）

- **アプリ**: Android 新刊チェッカーから楽天 Kobo API へ中継する HTTPS リバースプロキシ
- **なぜ必要か**: 楽天ウェブサービス 2026 年新仕様で Referer/Origin ヘッダ必須化＋ドメイン許可制になり、Android ネイティブから直接叩けなくなったため
- **やること**: Android からの認証付きリクエストを受け、Referer/Origin と楽天認証キーを付与して楽天 API に転送、レスポンスを透過で返す
- **詳細仕様**: `中継サーバー_要件定義書.md` を参照

---

## 2. 技術スタック

| 項目 | 内容 |
|---|---|
| 言語 | C# 12 |
| FW | ASP.NET Core 9.0 (Minimal API) |
| TFM | `net9.0` |
| IDE | Visual Studio 2022 |
| 配置先 | Windows + IIS（in-process ホスティング） |
| 外部公開 | `https://kaz.server-on.net:49443/`（ルーター: 外部49443→内部443） |

### NuGet
原則として外部パッケージは追加しない。標準ライブラリのみで実装する。
- `Microsoft.AspNetCore.App`（標準同梱）
- `Microsoft.Extensions.Http`（標準同梱、`IHttpClientFactory`）
- `Microsoft.AspNetCore.RateLimiting`（標準同梱、レート制限）

---

## 3. プロジェクト構成

```
NewReleaseChecker.Relay/
├── NewReleaseChecker.Relay.sln
├── .gitignore                          # appsettings.Secrets.json を必ず除外
├── NewReleaseChecker.Relay/
│   ├── NewReleaseChecker.Relay.csproj
│   ├── Program.cs                      # エントリポイント・DI・ミドルウェア・エンドポイント定義
│   ├── appsettings.json                # 公開設定（リッスンポート・上流URL・レート制限値）
│   ├── appsettings.Production.json     # 本番上書き（コミット可、機密値は含めない）
│   ├── appsettings.Secrets.json        # ★Git管理外（applicationId, accessKey, sharedSecret）
│   ├── appsettings.Secrets.json.example # 雛形（コミット可）
│   ├── web.config                      # IIS in-process 用（dotnet publish が自動生成）
│   ├── Endpoints/
│   │   ├── KoboSearchEndpoint.cs       # POST /api/kobo/search
│   │   ├── KoboGenresEndpoint.cs       # POST /api/kobo/genres
│   │   └── HealthEndpoint.cs           # GET /healthz
│   ├── Services/
│   │   ├── IRakutenProxy.cs            # プロキシ抽象
│   │   ├── RakutenProxyService.cs      # 透過プロキシ本体
│   │   └── UpstreamHeaderBuilder.cs    # Referer/Origin/Auth ヘッダ組立
│   ├── Middleware/
│   │   └── SharedSecretAuthMiddleware.cs
│   └── Options/
│       ├── RakutenOptions.cs           # ApplicationId, AccessKey, OriginDomain, UpstreamBaseUrl
│       └── RelayAuthOptions.cs         # SharedSecret
```

### レイヤー・依存
- 個人用・小規模のため、ASP.NET Core Minimal API のシンプル構成。多層プロジェクト分割は行わない
- DI は `WebApplicationBuilder.Services` に直接登録
- `IRakutenProxy` を抽象化しておき、将来テスト時にモック差し替え可能にする

---

## 4. コーディング規約

### 命名規則（.NET 標準。Android 側プロジェクトと同じ）
| 対象 | 規則 | 例 |
|---|---|---|
| クラス / メソッド / プロパティ | PascalCase | `RakutenProxyService`, `ProxyAsync`, `UpstreamBaseUrl` |
| private フィールド | `_camelCase` | `_httpClient` |
| 定数 | PascalCase | `MaxConcurrentUpstream` |
| インターフェース | `I` + PascalCase | `IRakutenProxy` |
| 非同期メソッド | 末尾 `Async` | `ProxyAsync` |

**規約**:
- **名前空間**: `NewReleaseChecker.Relay.{Endpoints|Services|Middleware|Options}`
- **null許容参照型**: `<Nullable>enable</Nullable>` 全プロジェクト有効
- **`IDisposable` / `IAsyncDisposable`**: 標準パターンで実装。`HttpClient` は `IHttpClientFactory` 経由で取得し、自身では Dispose しない
- **1ファイル1型**を基本とする

### 実装パターン（必須）
- **DI**: プロキシは**型付き HttpClient として `builder.Services.AddHttpClient<IRakutenProxy, RakutenProxyService>(...)` で登録する（これ1本でよい）**。型付きクライアントは既定で Transient であり、`HttpMessageHandler` のローテーション（DNS 滞留・ソケット枯渇回避）はこの登録に内包される。**`AddSingleton<IRakutenProxy, RakutenProxyService>()` を併用しない**こと——型付きクライアントを Singleton 化すると Handler がローテートされず、`IHttpClientFactory` を使う意味が失われる。どうしても Singleton にしたい場合は `HttpClient` を直接注入せず `IHttpClientFactory` を注入してリクエスト毎に `CreateClient()` する
- **設定**: `builder.Services.Configure<RakutenOptions>(builder.Configuration.GetSection("Rakuten"))` のように `IOptions<T>` パターン
- **ログ**: `ILogger<T>` をコンストラクタ注入。`MessageService` のような静的ファサードは使わない（ASP.NET Core 標準で完結）
- **非同期**: 全 I/O は `async/await`。同期 API は禁止
- **`CancellationToken` とタイムアウト**: `HttpClient.Timeout` は**設定しない**。代わりに `CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted)` に `CancelAfter(TimeSpan.FromSeconds(15))` を合成したトークンを上流 `SendAsync` に渡す。これでクライアント切断・上流タイムアウトの両方をキャンセルしつつ、`RequestAborted.IsCancellationRequested` で両者を区別できる（区別できないと 504 と「クライアントが消えただけ」を取り違える）

---

## 5. 秘密情報の取り扱い（重要）

中継サーバーが保持する秘密情報:
1. 楽天 `applicationId`（楽天API認証）
2. 楽天 `accessKey`（楽天API認証）
3. **共有シークレット**（Android アプリとの相互認証用。**新規生成すること**）

### 配置方法
`appsettings.Secrets.json` に集約。**サーバーへの手動配置のみ**、Git にはコミットしない。

```jsonc
// appsettings.Secrets.json（Git 管理外、本番サーバーにのみ配置）
{
  "Rakuten": {
    "ApplicationId": "ここに楽天アプリID",
    "AccessKey": "ここに楽天アクセスキー"
  },
  "RelayAuth": {
    "SharedSecret": "ここに 32 バイト以上のランダム文字列"
  }
}
```

```jsonc
// appsettings.Secrets.json.example（リポジトリにコミット）
{
  "Rakuten": {
    "ApplicationId": "REPLACE_WITH_RAKUTEN_APPLICATION_ID",
    "AccessKey": "REPLACE_WITH_RAKUTEN_ACCESS_KEY"
  },
  "RelayAuth": {
    "SharedSecret": "REPLACE_WITH_LONG_RANDOM_STRING"
  }
}
```

### `.gitignore`
```
appsettings.Secrets.json
```

### 共有シークレットの生成方法
- PowerShell:
  ```powershell
  [Convert]::ToBase64String((1..48 | ForEach-Object {Get-Random -Maximum 256}) -as [byte[]])
  ```
- または、`openssl rand -base64 48` 相当の任意のランダム生成ツール
- 同じ値を Android アプリの `Secrets.cs` の `RelayServerApiKey` にも設定する

### 設定読み込み（`Program.cs`）
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Secrets.json", optional: false, reloadOnChange: false); // Secrets を必須に
```

`optional: false` にしておけば、`appsettings.Secrets.json` の配置漏れで即座に起動失敗し、サイレントに壊れた状態で動くのを防げる。

---

## 6. 中核ロジックの実装指針

### 6.1 透過プロキシ本体（`RakutenProxyService`）
```csharp
public interface IRakutenProxy
{
    Task ProxyAsync(
        string upstreamPath,        // 楽天APIのパス（電子書籍検索=/services/api/Kobo/EbookSearch/20170426、ジャンル検索=/services/api/Kobo/GenreSearch/20131010。新ドメイン openapi.rakuten.co.jp 配下でもパスは据え置きだが、バージョン番号は API ごとに異なる＝混同しないこと）
        IDictionary<string, string?> queryFromClient,
        HttpContext httpContext,
        CancellationToken ct);
}
```

実装責務:
1. `queryFromClient` に `applicationId` と `accessKey` を追加
2. `UpstreamBaseUrl + upstreamPath + クエリ` で GET URL を組み立て
3. `Referer`, `Origin`, `User-Agent`, `Accept` ヘッダを付与（`UpstreamHeaderBuilder`）
4. `HttpClient.SendAsync` で送信。`CancellationToken` を渡す
5. レスポンスのステータス・`Content-Type`・本文を `HttpContext.Response` にコピー
6. 除外ヘッダ（`Set-Cookie`, `Transfer-Encoding`, `Connection`, `Server`, `Date`）はコピーしない
7. 楽天 API が 4xx/5xx を返したら**そのまま透過**（500 でラップしない）
8. 例外時のステータスマッピング（**`TaskCanceledException` は `OperationCanceledException` の派生**なので、型ではなく `HttpContext.RequestAborted.IsCancellationRequested` で切断とタイムアウトを判別する）:
   - `OperationCanceledException` かつ `RequestAborted.IsCancellationRequested == true`（クライアント切断）: そのまま投げる（ASP.NET Core が処理。レスポンスを書かない）
   - `OperationCanceledException` かつ `RequestAborted` は未発火（＝こちらの 15 秒 `CancelAfter` が発火＝上流タイムアウト）: 504 を返す
   - `HttpRequestException`（DNS/TLS/接続断 等）: 502 を返す
   - その他: 500 を返す

### 6.2 共有シークレット認証（`SharedSecretAuthMiddleware`）
- `/healthz` は素通し
- `X-Relay-Auth` ヘッダ未存在 → 401
- ヘッダ値と `RelayAuthOptions.SharedSecret` を `CryptographicOperations.FixedTimeEquals` で比較
- 不一致 → 401、ログには **IP とエンドポイントのみ記録**、送信値は記録しない
- 一致 → `await next(context)`

```csharp
// 比較の核心部分（コード断片）
var headerBytes = Encoding.UTF8.GetBytes(headerValue);
var secretBytes = Encoding.UTF8.GetBytes(_options.Value.SharedSecret);
if (headerBytes.Length != secretBytes.Length ||
    !CryptographicOperations.FixedTimeEquals(headerBytes, secretBytes))
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
    return;
}
```

### 6.3 レート制限（`AddRateLimiter`）
- ASP.NET Core 9 の標準 `RateLimiter` を使用
- `PartitionedRateLimiter.Create<HttpContext, string>` でクライアント IP をパーティションキーに
- `FixedWindowRateLimiter`、`PermitLimit=2, Window=1秒` を初期値とする（appsettings.json で変更可）
- 超過時は 429 と `Retry-After: 1` を返す
- `/healthz` には適用しない。**`PartitionedRateLimiter.Create<HttpContext, string>` を `options.GlobalLimiter` に設定する場合、既定で全エンドポイント（`/healthz` 含む）に掛かる**ため、`/healthz` のマッピングに `.DisableRateLimiting()` を付けて明示的に除外すること（さもないと §3.2.5 R-005 の死活監視も 2req/s 制限に巻き込まれる）

### 6.4 ログ方針
- `ILogger<T>` を各クラスでコンストラクタ注入
- レベル使い分け:
  - `LogInformation`: 受信成功（エンドポイント・楽天ステータス・所要時間）
  - `LogWarning`: 認証失敗、レート制限超過
  - `LogError`: 楽天 API 接続失敗・タイムアウト、想定外例外（`exception` 引数で渡す）
- **絶対にログに出さない**: `accessKey`, `applicationId`, `SharedSecret`, `X-Relay-Auth` の値
- 出力先: 標準では Console のみ。ファイル出力は IIS 配下なら `stdoutLog` を `web.config` で有効化（後述 §10）

### 6.5 `Program.cs` の骨格（参考）
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddJsonFile("appsettings.Secrets.json", optional: false);

builder.Services.Configure<RakutenOptions>(builder.Configuration.GetSection("Rakuten"));
builder.Services.Configure<RelayAuthOptions>(builder.Configuration.GetSection("RelayAuth"));

// 型付きクライアント1本で登録（Transient）。AddSingleton は併用しない（§4）
builder.Services.AddHttpClient<IRakutenProxy, RakutenProxyService>(c =>
{
    // c.Timeout は設定しない。15秒タイムアウトは RequestAborted とリンクした
    // CancellationToken + CancelAfter で実現する（§4「CancellationToken とタイムアウト」/ §6.1）
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddRateLimiter(options => { /* §6.3 */ });

var app = builder.Build();

app.UseRateLimiter();
app.UseMiddleware<SharedSecretAuthMiddleware>();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/kobo/search", KoboSearchEndpoint.HandleAsync);
app.MapPost("/api/kobo/genres", KoboGenresEndpoint.HandleAsync);

app.Run();
```

---

## 7. やってはいけないこと（アンチパターン）
- ❌ `appsettings.Secrets.json` を Git にコミットする
- ❌ 認証ヘッダ・楽天キーの値をログに出力する
- ❌ 共有シークレット比較を `==` や `string.Equals` で行う（→ `FixedTimeEquals`）
- ❌ 楽天 API のレスポンスを整形・型付け・キャッシュする（透過プロキシに徹する）
- ❌ クライアントから受け取った任意の URL を上流に転送する（指定エンドポイントのみ）
- ❌ HttpClient を `new` する（→ `IHttpClientFactory`）
- ❌ 例外時に楽天 API からの詳細なエラー本文を握り潰す（4xx/5xx はそのまま透過し、Android 側で扱う）

---

## 8. 楽天ウェブサービス アプリ登録手順（管理者作業）

実装の前に、楽天ウェブサービスで「Webアプリケーション」タイプのアプリを登録し、`applicationId` と `accessKey` を取得する。

1. https://webservice.rakuten.co.jp/ にアクセスし、楽天会員でログイン
2. 右上「+ アプリID発行」
3. フォーム入力:
   - **アプリケーション名**: `新刊チェッカー`（任意）
   - **アプリケーションURL**: `https://kaz.server-on.net`
   - **アプリケーションタイプ**: `Webアプリケーション`
   - **許可されたWebサイト**: `kaz.server-on.net`（1行のみ、ワイルドカード・スキーム不要）
   - **アプリケーションの説明**: `個人用の書籍新刊チェッカーアプリでの利用`
4. 規約に同意して登録
5. 表示される `アプリケーションID` と `アクセスキー` を控える（後で `appsettings.Secrets.json` に記入）
6. アフィリエイトID は今回不要

> **重要**: 「許可されたWebサイト」に登録したドメインと、中継サーバーが上流に付与する `Referer` / `Origin` のドメインが**一致**していること。両方とも `kaz.server-on.net` で揃える。

---

## 9. Let's Encrypt で SSL 証明書取得（win-acme）

IIS 用の SSL 証明書を Let's Encrypt から無料で取得し、自動更新まで仕込む。

### 9.1 前提
- 既に Windows 上で IIS がインストール済み（次節 §10 で構築）
- ドメイン `kaz.server-on.net` がサーバーのグローバル IP に解決される（MyDNS.JP の DDNS で運用中）
- ルーターの**外部 80 番ポート**が一時的に内部 80 番に転送できる状態（証明書取得時の HTTP-01 チャレンジに必要）。常時転送が難しい場合は、取得時のみ転送設定を有効化する

### 9.2 win-acme のインストール
1. https://www.win-acme.com/ から最新版（`win-acme.v2.x.x.x64.pluggable.zip`）をダウンロード
2. 任意のフォルダ（例: `C:\win-acme\`）に展開
3. **管理者として** PowerShell または cmd で `wacs.exe` を実行

### 9.3 証明書取得手順（対話モード）
```
C:\win-acme> wacs.exe
```

メニューが表示されたら以下を選択:
1. **M: Create certificate (full options)** を選ぶ（細かく設定するため）
2. **2: Manual input** を選び、ホスト名に `kaz.server-on.net` を入力
3. Validation method: **[http] Save verification files on (network) path** を選ぶ
   - もしくは、IIS に対応サイトが先に作られていれば **[http] Serve verification files from memory** が使える（IIS in-process プラグイン）
   - HTTP-01 チャレンジは 80 番ポートに来るので、IIS 側で **80 番でリッスンするバインディング**を一時的に追加しておくこと
4. CSR: **RSA** を選択（デフォルト）
5. Store: **Windows Certificate Store**（IIS が読み取れる場所）
6. Installation: **IIS** を選び、対象サイトを選択（次節 §10 で作成する `NewReleaseChecker.Relay` サイトを指定）
7. Acceptance: ToS を確認して `yes`、メールアドレスを入力（更新失敗時の通知用）

完了すると:
- Let's Encrypt から証明書が発行される
- Windows 証明書ストアに保存される
- IIS の対応サイトの HTTPS バインディングに自動で適用される
- **タスクスケジューラに自動更新タスクが登録される（renew は毎日チェック、期限 30 日以内で実行）**

### 9.4 取得後の確認
- IIS マネージャー → 対象サイト → バインディング → 443 番に証明書が紐付いていることを確認
- ブラウザで `https://kaz.server-on.net:49443/healthz` にアクセスし、SSL 警告が出ずに 200 OK が返れば成功（中継サーバーが稼働している前提。先に §10・§11 を完了させてから確認）

### 9.5 トラブルシュート
- HTTP-01 チャレンジ失敗 → 外部 80 番ポートの転送が有効か、ファイアウォール/セキュリティソフトがブロックしていないかを確認
- バインディング失敗 → 対象 IIS サイトが先に作成されているか、サイト名が正しいかを確認
- 更新失敗 → タスクスケジューラ「win-acme renew (acme-v02.api.letsencrypt.org)」のログを確認

---

## 10. IIS セットアップ手順

新規 Windows サーバーへの IIS 構築から、中継アプリのデプロイまでの一連の手順。

### 10.1 IIS の有効化
**Windows 10/11 の場合**:
1. コントロールパネル → 「プログラムと機能」 → 「Windows の機能の有効化または無効化」
2. 以下をチェック:
   - **インターネット インフォメーション サービス**
     - Web 管理ツール → IIS 管理コンソール
     - World Wide Web サービス → アプリケーション開発機能（全部チェックでOK）
     - World Wide Web サービス → 共通 HTTP 機能（全部チェックでOK）
3. OK で適用、再起動

**Windows Server の場合**:
1. サーバーマネージャー → 「役割と機能の追加」
2. **Web サーバー (IIS)** にチェック、ウィザードに従って完了

### 10.2 ASP.NET Core Hosting Bundle のインストール
IIS で ASP.NET Core アプリを動かすために必須。
1. https://dotnet.microsoft.com/download/dotnet/9.0 から **「ASP.NET Core Runtime → Hosting Bundle」** をダウンロード
2. インストーラを実行（IIS が起動していると自動で `AspNetCoreModuleV2` がインストールされる）
3. インストール後、**IIS を再起動**: 管理者コマンドプロンプトで `iisreset`

### 10.3 アプリプール作成
1. IIS マネージャー起動
2. 左ペインでサーバー名 → **アプリケーション プール** → 右クリック「アプリケーション プールの追加」
3. 設定:
   - **名前**: `NewReleaseCheckerRelayPool`
   - **.NET CLR バージョン**: **「マネージド コードなし」** (in-process ホスティングではこれが必須)
   - **マネージド パイプライン モード**: 統合
4. 作成後、プールを右クリック → 「詳細設定」:
   - **アイドル タイムアウト (分)**: `0`（アイドルで停止しないように）
   - **プロセス モデル → ID**: ApplicationPoolIdentity（デフォルトのまま）

### 10.4 サイト作成
1. 左ペイン → **サイト** → 右クリック「Web サイトの追加」
2. 設定:
   - **サイト名**: `NewReleaseChecker.Relay`
   - **アプリケーション プール**: `NewReleaseCheckerRelayPool`
   - **物理パス**: `C:\inetpub\NewReleaseChecker.Relay\`（任意。後で publish 先として使う）
   - **バインディング**:
     - **種類**: `https`
     - **ホスト名**: `kaz.server-on.net`
     - **ポート**: `443`
     - **SSL 証明書**: ここでは「未選択のまま」OK（§9 の win-acme が後で自動設定）
   - **HTTP も追加**: 後で 80 番のバインディングを追加する（Let's Encrypt の HTTP-01 チャレンジ用）。サイト → 「バインディング」 → 「追加」 → HTTP / ホスト名 `kaz.server-on.net` / ポート `80`
3. 物理パスのフォルダを作成し、IIS_IUSRS グループに読み取り権限を付与（デフォルトで付くはず）

### 10.5 ファイアウォール
- Windows ファイアウォールで **TCP 443、TCP 80** の受信を許可（IIS インストール時に通常自動で許可される）

### 10.6 公開・配置
ローカル開発機で:
```
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

生成された `./publish` の中身を、サーバーの `C:\inetpub\NewReleaseChecker.Relay\` にコピー。

**含まれるべきファイル**:
- `NewReleaseChecker.Relay.dll`, `NewReleaseChecker.Relay.exe`
- `appsettings.json`, `appsettings.Production.json`
- `web.config`（publish で自動生成）
- 各種依存 DLL

**配置先で別途用意**:
- `appsettings.Secrets.json`（**Git・publish 出力どちらにも含まれないので手動配置**）

### 10.7 アクセス権
- アプリプールの ID（`IIS AppPool\NewReleaseCheckerRelayPool`）に対し、サイト物理パスへの**読み取り権限**を付与
- ログを書き出すフォルダ（後述）には**書き込み権限**を追加

### 10.8 stdout ログの有効化（任意・推奨）
`web.config` の `<aspNetCore ... />` 要素を編集:
```xml
<aspNetCore processPath="dotnet"
            arguments=".\NewReleaseChecker.Relay.dll"
            stdoutLogEnabled="true"
            stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess" />
```
- `logs` フォルダをサイト物理パス配下に作成
- アプリプール ID にそのフォルダへの**書き込み権限**を付与

### 10.9 楽天アプリ登録 → Secrets 配置
1. §8 の手順で楽天アプリを登録し、`applicationId` と `accessKey` を取得
2. §5 の手順で共有シークレットを生成
3. `appsettings.Secrets.json` をサーバーの `C:\inetpub\NewReleaseChecker.Relay\` に配置（中身は §5 のテンプレート）

### 10.10 起動確認
1. IIS マネージャーでサイトを「開始」（または `iisreset`）
2. サーバー上のブラウザで `https://localhost/healthz` を開き、200 OK と `{"status":"ok"}` が返ることを確認（証明書警告は出る、サーバー名検証の都合）
3. 外部から `https://kaz.server-on.net:49443/healthz` を開き、SSL 警告なしで 200 OK が返れば全て完了

### 10.11 ポートフォワーディング確認
ルーター設定で「外部 49443 → 内部 443」が有効になっていることを確認。

---

## 11. ルーター・ネットワーク要件

| 項目 | 設定 |
|---|---|
| 外部ポート 49443 → 内部 443 | TCP、サーバー機の内部 IP へ転送 |
| 外部ポート 80 → 内部 80 | TCP（**Let's Encrypt 取得時・更新時のみ必要**。常時開放しても可、セキュリティを気にするなら更新時のみ有効化） |
| DDNS | MyDNS.JP（`kaz.server-on.net`）。自動更新が動いていること |
| サーバー機の内部 IP | DHCP 予約等で固定推奨 |

> ⚠️ **証明書の自動更新と 80 番ポートの関係**: §9.3 で win-acme はタスクスケジューラに自動更新を登録するが、**HTTP-01 チャレンジは更新時にも 80 番ポートの到達性を要する**。80 番を「更新時のみ手動開放」にすると、自動更新タスクが期限前に走っても 80 番が閉じていて**サイレントに失敗 → 証明書失効でサーバー停止**となる。対策は次のいずれか: (a) 80 番を常時フォワーディングして自動更新を機能させる（推奨）／(b) 自動更新に頼らず、期限（30 日前〜）に手動で 80 番を開けて `wacs.exe --renew` を実行する運用にする。**「80 番は更新時のみ手動開放」かつ「自動更新任せ」の組み合わせは避ける**。なお外部 HTTPS は 49443・内部 443 のため、HTTP-01 の代替に TLS-ALPN-01（443）を使う手も外部 443 が開いておらず不可。代替するなら MyDNS.JP の DNS-01 を使う。

---

## 12. デプロイの実運用

### 初回
1. §8 楽天アプリ登録
2. §10.1〜10.5 IIS セットアップ
3. §10.6 publish & 配置
4. §10.7〜10.9 権限・ログ・Secrets 配置
5. §9 Let's Encrypt 証明書取得
6. §10.10 起動確認

### 更新時（コード変更時）
1. ローカルで `dotnet publish`
2. サーバーにファイルコピー（`appsettings.Secrets.json` は触らない）
3. IIS マネージャーでサイト/アプリプールを「リサイクル」（または `iisreset`）

### 証明書更新
- win-acme が自動でやってくれる。タスクスケジューラに登録されたタスクが期限 30 日前から毎日チェック
- 通知メールが届いたら手動で `wacs.exe --renew` を実行することもできる

---

## 13. 楽天 API 仕様の検証事項（実装時）

実装直前に楽天ウェブサービス公式ドキュメントで最終確認すべき項目（★は 2026-06 確認済で当面の値を確定済）:
1. ★**楽天 Kobo 電子書籍検索 API**: `https://openapi.rakuten.co.jp/services/api/Kobo/EbookSearch/20170426`（パス・バージョン据え置き）。配置直前にバージョン廃止告知が無いか最終確認のみ
2. ★**楽天 Kobo ジャンル検索 API**: `https://openapi.rakuten.co.jp/services/api/Kobo/GenreSearch/20131010`（**電子書籍検索の `20170426` とはバージョンが異なる**＝公式「version:2013-10-10」。旧 Android 実装 `RakutenKoboApiClient.cs` の `GenreSearch/20170426` は誤りなので流用しないこと）
3. ★**`applicationId` / `accessKey` の渡し方**: `applicationId` はクエリ必須、`accessKey` はヘッダ・クエリどちらでも可（両方必須）。本サーバーは**クエリで統一**
4. **`Referer` / `Origin` の照合方式**: 完全一致か前方一致か、トレイリングスラッシュの扱い（「許可されたWebサイト」に登録した `kaz.server-on.net` と一致させる。§8）
5. **`format=json`, `formatVersion=2` の指定有無**: 楽天 API は formatVersion で応答形式が変わるため、Android 側と合わせる
6. ★**レート制限の実際の閾値**: コミュニティ実測で**間隔 1.5 秒未満は 429**。中継側上限（1秒2req）は粗い保険にすぎず上流 429 は防げない。上流 429 は透過し、主たるペース制御は Android 側 `SiteRateLimiter` を 1.5 秒以上に

---

## 14. 未決事項
| # | 項目 | 扱い |
|---|---|---|
| TBD-R-001 | サーバー監視 | 個人用のため未対応 |
| TBD-R-002 | ログローテーション | IIS の stdout ログは日次ロール程度で十分。問題が起きたら検討 |
| TBD-R-003 | 複数クライアント | 共有シークレットを複数発行する設計に拡張する場合は別途 |
