# ニコニコAPI nvapi移行プラン

- 日付: 2026-04-25
- ブランチ: `app-moviewer`
- 起因: RSS取得時に `XmlException: 'id' is an unexpected token` が発生（[NicoUtil.cs:66-71](../_Apps/Nico.Core/NicoUtil.cs#L66-L71) `GetXmlChannelAsync` が HTML を受け取ったため）
- 方針: ニコニコ動画の RSS / 旧XML系APIを nvapi (公式SPA利用のJSON API) に全面置換

---

## 1. 事前調査結果

### 1.1 エンドポイント生死判定（2026-04-25時点）

| # | 現行エンドポイント | 状態 | 現行の利用箇所 |
|---|---|---|---|
| a | `www.nicovideo.jp/ranking/genre/{g}?rss=2.0` | ❌ **死亡** (HTML/SPA) | [NicoUtil.cs:100](../_Apps/Nico.Core/NicoUtil.cs#L100) |
| b | `www.nicovideo.jp/mylist/{id}?rss=2.0` | ⚠️ 稼働中 | [NicoUtil.cs:111](../_Apps/Nico.Core/NicoUtil.cs#L111), [NicoMylistModel.cs:17](../_Apps/Nico.Controls/NicoMylistModel.cs#L17) |
| c | `www.nicovideo.jp/user/{id}/video?rss=2.0` | ⚠️ 稼働中 | [NicoUtil.cs:128](../_Apps/Nico.Core/NicoUtil.cs#L128) |
| d | `ext.nicovideo.jp/api/getthumbinfo/{id}` | ✅ **稼働中** (http→https 301、XML 200) | [NicoUtil.cs:40](../_Apps/Nico.Core/NicoUtil.cs#L40) |
| e | `seiga.nicovideo.jp/api/user/info?id={id}` | ⚠️ 稼働中 (XML) | [NicoUserModel.cs:49](../_Apps/Nico.Controls/NicoUserModel.cs#L49) |
| f | `snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search` | ✅ 稼働中 (JSON) | [NicoUtil.cs:150](../_Apps/Nico.Core/NicoUtil.cs#L150) |

実害:
- **a**: ユーザ報告の例外原因
- **d**: 動作はしているが、description が短く tags や channel/owner 構造が古い。watch v3_guest に置換することで全文descriptionと channel優先owner判定が改善する（**緊急ではない**）

### 1.2 置換先nvapi（全て動作確認済み）

**認証方式は2系統に分岐**（実機 curl 検証済み・2026-04-25）:

| ホスト | 認証方式 | 検証 |
|---|---|---|
| `nvapi.nicovideo.jp` | **HTTPヘッダ必須**: `X-Frontend-Id: 6`, `X-Frontend-Version: 0` | ヘッダ無し→400 / ヘッダ有り→200 |
| `www.nicovideo.jp/api/watch/v3_guest` | **クエリパラメータで完結**: `?_frontendId=6&_frontendVersion=0` | クエリ無し→400(ヘッダ有無問わず) / クエリ有り→200(ヘッダ有無問わず) |

→ **watch/v3_guest はヘッダ付加不要**。既存 `GetNicoVideoUrl` の URL（`_frontendId/_frontendVersion` 含む）をそのまま既存 `WebUtil.GetJsonAsync(url)` に渡せばよい。新規ヘッダ付与ヘルパ `GetNvapiJsonAsync` は **nvapi.nicovideo.jp 用途専用**。

| # | 置換先 URL | 認証 |
|---|---|---|
| a | `nvapi.nicovideo.jp/v1/ranking/genre/{genre}?term={t}&tag={tag}&pageSize=100` | ヘッダ |
| b | `nvapi.nicovideo.jp/v2/mylists/{id}?sortKey=&sortOrder=&pageSize=100&page=1` | ヘッダ |
| c | `nvapi.nicovideo.jp/v2/users/{id}/videos?sortKey=&sortOrder=&pageSize=100&page=1` | ヘッダ |
| d | `www.nicovideo.jp/api/watch/v3_guest/{id}?_frontendId=6&_frontendVersion=0&actionTrackId=&skips=harmful&noSideEffect=false&t=` ([NicoUtil.cs:55-60](../_Apps/Nico.Core/NicoUtil.cs#L55-L60) `GetNicoVideoUrl` **再利用可**) | クエリ |
| e | `nvapi.nicovideo.jp/v1/users/{id}` | ヘッダ |

### 1.3 レスポンス構造（**2026-04-25 実機 curl 検証済み**）

**essential形式** (a/b/c のitem)
```
type, id, contentType, title, registeredAt(ISO8601),
count.{view,comment,mylist,like}, thumbnail.{url,middleUrl,largeUrl,listingUrl,nHdUrl,shortUrl},
duration(秒), shortDescription, isChannelVideo,
owner.{ownerType("user"|"channel"), type, visibility, id, name, iconUrl}
```
→ タグ無し、descriptionは短縮版
- **重要**: `owner.id` は **ownerType="channel" の場合すでに `"ch2649997"` 形式で prefix 込み** で返る（user の場合は `"1594318"` の素の数値）。`"ch" + id` を追加するとダブり (`"chch2649997"`) になる

**watch v3_guest形式** (d)
```
[共通]           data.video.{id,title,description(全文HTML),count,duration,thumbnail,registeredAt}
[共通]           data.tag.items[*].name              (タグ配列)
[ユーザー動画]   data.channel = null                 / data.owner   = {id(int), nickname, iconUrl, channel(=null), ...}
[チャンネル動画] data.channel = {id("ch..."形式), name, ...} / data.owner   = null
```
→ 全情報
- **重要1 (実機 curl 検証 2026-04-25, 動画 4本確認)**: 投稿者情報の格納先は動画種別で**完全に分かれる**。チャンネル動画は `data.channel` が non-null・`data.owner` が null。ユーザー動画は逆 (`data.channel = null`, `data.owner = {id, nickname, ...}`)。**判定キー: `data.channel != null` → チャンネル動画**。旧 [NicoVideoModel.cs:65-67](../_Apps/Nico.Controls/NicoVideoModel.cs#L65-L67) のコメントアウト旧コードと同仕様
- **重要2**: `data.owner.id` は **数値 (int)**。`DynamicUtil.S` は `$"{value}"` で string 化するので OK
- **重要3**: `data.channel.id` はチャンネル動画ですでに **`"ch2649997"` 形式で prefix 込み** (essential の channel 表現と同じ)。`"ch" + id` を追加してはいけない
- **重要4**: `actionTrackId` と `t` クエリは **空文字だと 400 (INVALID_PARAMETER)** が返る。既存 [NicoUtil.cs:55-60](../_Apps/Nico.Core/NicoUtil.cs#L55-L60) `GetNicoVideoUrl` は `MOVIEWER_{session}` と `t={session}` を埋めているため OK
- **重要5 (DynamicUtil.O の null 安全限界)**: `data.channel`/`data.owner`/`owner.channel` 等は **明示的に null** で返るフィールド。`DynamicUtil.S("a.b.c")` のドット記法は **「未定義キー」は null 返却で安全だが「定義済かつ値 null」は再帰先で NRE** を投げる。本プランではドット記法ではなく **`IsDefined` + null チェック + 段階的アクセス** で防御する（詳細は §3.3 注を参照）

**mylist v2 形式** (b の data.mylist)
```
data.mylist.{id, name, description, decoratedDescriptionHtml,
             defaultSortKey, defaultSortOrder, items[*],
             totalItemCount, hasNext, isPublic,
             owner, hasInvisibleItems, followerCount, isFollowing}
```
- **`createdAt` / `updatedAt` 等のタイムスタンプは存在しない** → `MylistDate` は `items[0].addedAt` で代替（プラン §3.4 参照）
- 各 item: `{itemId, watchId, description, decoratedDescriptionHtml, addedAt(ISO8601), status, video(=essential相当)}`

**user videos v2 形式** (c の data)
```
data.{totalCount, items[*]}
data.items[i].{series, essential(=essential相当)}
```
- ranking/mylist と異なり items 直下ではなく `items[i].essential` でラップされている

**user info v1 形式** (e の data.user)
```
data.user.{id(int), nickname, icons.{small,large}, description, ...,
           userChannel, sns[], coverImage, ...}
```

### 1.4 値マッピング (要コンボファイル変更)

#### rank_period ([nico-combo-setting.xml:32-38](../_Apps/lib/nico-combo-setting.xml#L32-L38))
| 現行 value | 新 value |
|---|---|
| hourly | `hour` |
| daily | `24h` |
| weekly | `week` |
| monthly | `month` |
| total | `total` |

#### oyder_by_mylist ([nico-combo-setting.xml:71-86](../_Apps/lib/nico-combo-setting.xml#L71-L86))
現行: 数値コード（`0,1,6,7,...`） → nvapi `sortKey,sortOrder`形式へ全書換
| 現行 key | 新 display |
|---|---|
| regdate- | `addedAt,desc` |
| regdate+ | `addedAt,asc` |
| stadate- | `registeredAt,desc` |
| stadate+ | `registeredAt,asc` |
| viewcnt-/+ | `viewCount,desc/asc` |
| commcnt-/+ | `commentCount,desc/asc` |
| listcnt-/+ | `mylistCount,desc/asc` |
| likecnt-/+ | `likeCount,desc/asc` |
| lengsec-/+ | `duration,desc/asc` |

**解釈**: `regdate-` = マイリスト追加日時(addedAt)、`stadate-` = 動画投稿日時(registeredAt)。**要ユーザ確認**（リスク項目）。

#### oyder_by_user
現行 display 値（`registeredAt,desc` 等）はそのまま nvapi で使える文字列値。コード側は `display.Split(',')` で `sortKey`/`sortOrder` の2クエリパラメータに分割して渡す（既存維持）。

**重複削除**: 現行XMLは `regdate-`/`stadate-` がともに `registeredAt,desc` を、`regdate+`/`stadate+` がともに `registeredAt,asc` を返す重複定義になっている（user投稿動画には mylist の addedAt 相当が無く、両者とも投稿日時に集約されるため）。UI 上で別項目を選んでも結果が同じになり混乱の元のため、**`regdate-`/`regdate+` を `oyder_by_user` から削除**する。

```xml
<combo group="oyder_by_user">
    <!-- regdate-/regdate+ は削除（registeredAt に集約） -->
    <item value="stadate-"   display="registeredAt,desc" />
    <item value="stadate+"   display="registeredAt,asc" />
    <item value="viewcnt-"   display="viewCount,desc" />
    ...
</combo>
```

**コード側の対応**（[NicoUtil.GetVideosByNicouser](../_Apps/Nico.Core/NicoUtil.cs#L120-L124)）: UI から `regdate-/+` で呼ばれた場合は `stadate-/+` に正規化してからルックアップ:

```csharp
public static IAsyncEnumerable<NicoVideoModel> GetVideosByNicouser(string userid, string order)
{
    // user 動画では regdate(登録日時) と stadate(投稿日時) は同義 (= registeredAt)。
    // oyder_by_user XMLから regdate を削除済みのため、ここで stadate に正規化してマッピング不在を回避。
    var normalized = order switch
    {
        "regdate-" => "stadate-",
        "regdate+" => "stadate+",
        _ => order,
    };
    var orderbyuser = ComboUtil.GetNicoDisplay("oyder_by_user", normalized).Split(',');
    return GetVideosByNicouser(userid, orderbyuser[0], orderbyuser[1]);
}
```

### 1.5 pageSize制限

- ranking: **100のみ** (10,20,30,50は400)
- mylist/user: 最大100 (200以上は400)
- → 全エンドポイントで **pageSize=100 固定**

### 1.6 件数方針

- **ranking**: 100件固定（仕様上Top-Nなので追加取得不要）
- **mylist / user videos**: **全件取得**。ただし100件超は **遅延追加ロード** で UX 維持（初期100件を即時表示、残りをバックグラウンドで順次追加）
- **search (word/tag, snapshot API)**: 現行どおり limit=50 の1コール

---

## 2. 実装方針

- nvapi にすべて置換。RSS/getthumbinfo系コードパスは削除
- **戻り値は全 `IAsyncEnumerable<NicoVideoModel>` に統一**（mylist/userの遅延ロード対応のため。`GetVideoBySearchType` のswitchで型を揃える必要）
- **再利用**:
  - [NicoUtil.cs:55-60](../_Apps/Nico.Core/NicoUtil.cs#L55-L60) `GetNicoVideoUrl`（watch/v3_guest URL構築。`actionTrackId`/`t` のセッション値が必要なため自前生成は不可。空だと 400）
  - [NicoUserModel.cs:12-28](../_Apps/Nico.Controls/NicoUserModel.cs#L12-L28) `SetUserInfo` のアイコンURL自動生成ロジック（id が `"ch"` 始まりかどうかで分岐。**essential の owner.id は channel の場合すでに `"ch..."` 形式で来るため、追加の prefix 操作は不要、そのまま渡せばよい**）
  - 既存 [NicoVideoModel.cs:27](../_Apps/Nico.Controls/NicoVideoModel.cs#L27) `NicoVideoModel(dynamic item)` (snapshot search用) はフィールド名が異なるため **存続**
- **追加**: `WebUtil` にカスタムヘッダ付きGET（最小拡張）
- **キャンセル処理**: 現状同様 TryCatch 握り潰し（View切替等で中断する仕組みは今回入れない）
- `WebUtil.GetXmlAsync` は **削除しない**（ユーザ指示）

---

## 3. 変更ファイル詳細

### 3.1 [TBird.Web/_ROOT/WebUtil.cs](../TBird.Web/_ROOT/WebUtil.cs)

#### 3.1.1 既存 `GetJsonAsync(url)` の null 吸収統一（バグ修正含む）

**現行** [WebUtil.cs:128-131](../TBird.Web/_ROOT/WebUtil.cs#L128-L131):
```csharp
public static async Task<dynamic> GetJsonAsync(string url)
{
    return DynamicJson.Parse(await GetStringAsync(url).TryCatch().ConfigureAwait(false));
}
```

**問題**: `SendStringAsync` は HTTP 4xx/5xx 時に `null` を返すが、`DynamicJson.Parse(null)` は `NullReferenceException` を投げる。Polly リトライで吸収しきれないケースで NRE が漏れる潜在バグ。

**修正後**:
```csharp
public static async Task<dynamic> GetJsonAsync(string url)
{
    var body = await GetStringAsync(url).TryCatch().ConfigureAwait(false);
    return string.IsNullOrEmpty(body) ? null : DynamicJson.Parse(body);
}
```

これにより `GetVideo`（watch/v3_guest, ヘッダ無し）/ `SearchApiV2`（snapshot search）も同じ null セマンティクスを得る。

#### 3.1.2 ヘッダ付き版の追加（nvapi 用）

```csharp
public static async Task<string> GetStringAsync(string url, IDictionary<string, string> headers)
{
    var req = new HttpRequestMessage(HttpMethod.Get, url);
    foreach (var kv in headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
    return await SendStringAsync(req).ConfigureAwait(false);
}

public static async Task<dynamic> GetJsonAsync(string url, IDictionary<string, string> headers)
{
    var body = await GetStringAsync(url, headers).TryCatch().ConfigureAwait(false);
    return string.IsNullOrEmpty(body) ? null : DynamicJson.Parse(body);
}
```

#### 3.1.3 呼び出し側の null/例外ハンドリング規約

`TryCatch<T>` は失敗時に `ArgumentNullException` を**再投**する仕様（[TaskExtension.cs:31-41](../TBird.Core/Extensions/TaskExtension.cs#L31-L41)）。HTTP 4xx/5xx は `null` 戻りで吸収できるが、**ネットワーク例外時は呼び出し側で try/catch が必須**。

呼び出し規約:
- `GetVideosByMylist` 等の `IAsyncEnumerable` メソッド内: `if (json == null) yield break;` で空シーケンス
- ViewModel 側の `await foreach` は `try { } catch { }` で全例外を握り潰し（既存と同等）
- `NicoMylistModel.GetNicoMylistData` の戻り値 null は コンストラクタ側で吸収

**削除候補**: `GetXmlAsync` (移行後はNico以外からも未参照となる)。ただし今回は残置、別PRで削除判断。

#### 3.1.4 既存 `GetJsonAsync(url)` 仕様変更が **`SearchApiV2` に与える副作用**

§3.1.1 の null 吸収化により、`WebUtil.GetJsonAsync(url)` は HTTP 4xx/5xx 時に **`null` を返す**ようになる。これは [NicoUtil.cs:152](../_Apps/Nico.Core/NicoUtil.cs#L152) `SearchApiV2(...)` の戻り値も null になり得ることを意味する。
旧仕様では `DynamicJson.Parse(null)` の段階で NRE が出ていた（同じく失敗していた）が、エラー経路が変わるため呼出側 [NicoUtil.cs:155-161](../_Apps/Nico.Core/NicoUtil.cs#L155-L161) `SearchApiV2(dynamic json)` の冒頭に **null ガードを追加必須**:

```csharp
private static IEnumerable<NicoVideoModel> SearchApiV2(dynamic json)
{
    if (json == null) yield break;          // ← 新規追加
    foreach (var item in json.data)
    {
        yield return new NicoVideoModel(item);
    }
}
```

`IAsyncEnumerable` 化（プラン §3.2 step 9）の際もこのガードを残す。

### 3.2 [_Apps/Nico.Core/NicoUtil.cs](../_Apps/Nico.Core/NicoUtil.cs)

追加:
```csharp
private static readonly Dictionary<string, string> NvapiHeaders = new()
{
    ["X-Frontend-Id"] = "6",
    ["X-Frontend-Version"] = "0",
};

public static Task<dynamic> GetNvapiJsonAsync(string url)
    => WebUtil.GetJsonAsync(url, NvapiHeaders);
```

書き換え（戻り値は全て `IAsyncEnumerable<NicoVideoModel>`）:

- `GetVideo(videoid)`: `Task<NicoVideoModel>` のまま。`getthumbinfo` XML → `WebUtil.GetJsonAsync(GetNicoVideoUrl(videoid))`（**watch/v3_guest はクエリパラメータで認証完結のため、既存ヘッダ無し版 `GetJsonAsync(url)` を使用。`GetNvapiJsonAsync` は使わない**）。
  ```csharp
  public static async Task<NicoVideoModel> GetVideo(string videoid)
  {
      try
      {
          var json = await WebUtil.GetJsonAsync(GetNicoVideoUrl(videoid));
          if (json != null && (int)json.meta.status == 200)
          {
              return NicoVideoModel.FromWatchData(json.data);
          }
      }
      catch (Exception ex)
      {
          MessageService.Exception(ex);
      }
      return new NicoVideoModel { ContentId = videoid, Status = VideoStatus.Delete };
  }
  ```
  **ポイント**:
  - `WebUtil.GetJsonAsync(url)` は **3.1 で null 吸収版に統一する** ため、ネットワーク失敗時に NRE は出ないが、`(int)json.meta.status` キャストやプロパティアクセスで例外が出る可能性があるため try/catch は必須
  - 失敗時は `Status=Delete` のモデルを返却（OnLoaded 側のフォールバック動作を維持）

- `GetVideosByRanking(genre, tag, term)` — **1ページ100件のみ**:
  ```csharp
  public static async IAsyncEnumerable<NicoVideoModel> GetVideosByRanking(string genre, string tag, string term)
  {
      // 呼出側 (NicoRankingViewModel) からは慣習的に "all" がハードコードで渡されるが、
      // nvapi では `tag=all` は「all という文字列タグでフィルタ」のセマンティクスを取り得るため
      // 無タグ呼出 (= ジャンル全体ランキング) と等価ではない。"all" を空扱いに正規化する。
      var effectiveTag = (string.IsNullOrEmpty(tag) || tag == "all") ? null : tag;
      var url = $"https://nvapi.nicovideo.jp/v1/ranking/genre/{genre}?term={term}&pageSize=100"
          + (string.IsNullOrEmpty(effectiveTag) ? "" : $"&tag={Uri.EscapeDataString(effectiveTag)}");
      var json = await GetNvapiJsonAsync(url);
      if (json == null) yield break;
      foreach (var item in json.data.items)
          yield return NicoVideoModel.FromEssential(item);
  }
  ```

- `GetVideosByMylist(mylistid, orderby)` — **全件ページング**（403/404は空シーケンス）。**addedAt を保持するため `FromMylistItem` を使う**:
  ```csharp
  public static async IAsyncEnumerable<NicoVideoModel> GetVideosByMylist(string mylistid, string orderby)
  {
      var parts = orderby.Split(',');
      var sortKey = parts[0]; var sortOrder = parts[1];
      int page = 1;
      while (true)
      {
          var url = $"https://nvapi.nicovideo.jp/v2/mylists/{mylistid}?sortKey={sortKey}&sortOrder={sortOrder}&pageSize=100&page={page}";
          var json = await GetNvapiJsonAsync(url);
          if (json == null) yield break;          // 403/404/network error
          var ml = json.data.mylist;
          foreach (var it in ml.items)
          {
              yield return NicoVideoModel.FromMylistItem(it);  // ← FromEssential ではなく FromMylistItem
          }
          // hasNext を信頼する (実機検証: ml.hasNext が次ページ有無を返す)
          // フォールバックとして totalItemCount でも判定可だが hasNext 一本で十分
          bool hasNext = false;
          try { hasNext = (bool)ml.hasNext; } catch { hasNext = false; }
          if (!hasNext) yield break;
          page++;
      }
  }
  ```
  - 実機 curl 検証 (2026-04-25): `data.mylist.totalItemCount` (int) と `data.mylist.hasNext` (bool) の両方が存在。`hasNext` 一本でループ終了判定可能

- `GetVideosByNicouser(userid, order)` / 内部 `GetVideosByNicouser(userid, key, order)` — **全件ページング**。`oyder_by_user` の display は `key,order` の `,` 区切り文字列なので、呼び出し側の `Split(',')` は維持。**XML から `regdate-/regdate+` を削除する関係で、外側の overload で `regdate→stadate` 正規化を実装する** — 詳細は 1.4 節参照。レスポンス構造は `json.data.totalCount` (int) と `json.data.items[i].essential` (mylist と異なり `essential` ラッパーあり)。各 essential を `FromEssential` に渡す。終端判定は `items.Count < 100` でよい (user videos には hasNext フィールドが無いため)。null 時は `yield break`

- `GetVideosByWord(word, order)` / `GetVideosByTag(word, order)` — 現行 snapshot search API のまま。`IAsyncEnumerable` に包む（1コール・最大50件を `yield return` で返すだけ。snapshot search の項目は `FromEssential` ではなく **既存の `new NicoVideoModel((dynamic)item)` (snapshot search 用コンストラクタ) を継続使用**）

- `GetVideoBySearchType(word, type, order)`: switch各枝 `IAsyncEnumerable<NicoVideoModel>` を返す

削除:
- `GetXmlChannelAsync` (行66-71)
- `GetVideosFromXmlUrl` (行73-80)

### 3.3 [_Apps/Nico.Controls/NicoVideoModel.cs](../_Apps/Nico.Controls/NicoVideoModel.cs)

追加プロパティ:
```csharp
// Mylist 経由で取得した場合の追加日時。お気に入り巡回 (PatrolFavorites) で
// addedAt 順ソートと整合させるため。それ以外の経路では null。
public DateTime? MylistAddedAt { get; set; }
```

追加（**3つのファクトリメソッド**。既存の `NicoVideoModel(dynamic item)` (snapshot search 用) と同一シグネチャ衝突を避けるため、すべて static factory に統一）:

```csharp
// essential (ranking / user videos / mylist の video 部) 共通抽出
// 注: owner.id は ownerType="channel" の場合すでに "ch2649997" 形式で prefix 込みで返るため、
//     "ch" + id を追加してはいけない（"chch2649997" になる）
public static NicoVideoModel FromEssential(dynamic essential)
{
    var m = new NicoVideoModel();
    try
    {
        m.ContentId = DynamicUtil.S(essential, "id");
        m.Title = DynamicUtil.S(essential, "title");
        m.Description = DynamicUtil.S(essential, "shortDescription");
        m.ThumbnailUrl = DynamicUtil.S(essential, "thumbnail.url");
        m.ViewCount = DynamicUtil.L(essential, "count.view");
        m.CommentCount = DynamicUtil.L(essential, "count.comment");
        m.MylistCount = DynamicUtil.L(essential, "count.mylist");
        m.StartTime = DateTimeOffset.Parse(DynamicUtil.S(essential, "registeredAt")).DateTime;
        m.Duration = TimeSpan.FromSeconds(DynamicUtil.L(essential, "duration"));

        // owner.id は ownerType="channel" のとき既に "ch..." 形式 → そのまま渡す
        // **重要**: 公式アカウントの ranking entry 等で `owner` 自体が null の可能性あり
        // (実機サンプル数 4 では確認できていないが、構造的に起こりうる)。
        // DynamicUtil.S(essential, "owner.id") は owner=null だと NRE → 全体 try/catch で
        // Status=Delete に倒れる。これは過剰削除なので、IsDefined+null チェックで明示的に
        // 「owner 不在 → 投稿者情報空のまま続行」させる
        if (essential.IsDefined("owner") && essential.owner != null)
        {
            var ownerId = DynamicUtil.S(essential.owner, "id");
            var ownerName = DynamicUtil.S(essential.owner, "name");
            if (!string.IsNullOrEmpty(ownerId))
            {
                m.UserInfo.SetUserInfo(ownerId, ownerName);
            }
        }

        m.RefreshStatus();
        m._beforedisplay = true; // tag等はOnLoadedで watch v3_guest 経由に補完
    }
    catch (Exception ex)
    {
        MessageService.Exception(ex);
        m.Status = VideoStatus.Delete;
    }
    return m;
}

// mylist v2 の items[i] 用 (essential を内包しつつ addedAt も保持)
// PatrolFavorites の break 判定が addedAt 順と整合するよう MylistAddedAt を設定する
public static NicoVideoModel FromMylistItem(dynamic item)
{
    // item.video は essential 相当
    var m = FromEssential(item.video);
    if (m.Status == VideoStatus.Delete) return m;
    try
    {
        var addedAt = DynamicUtil.S(item, "addedAt");
        if (!string.IsNullOrEmpty(addedAt))
        {
            m.MylistAddedAt = DateTimeOffset.Parse(addedAt).DateTime;
        }
    }
    catch (Exception ex)
    {
        MessageService.Exception(ex);
        // addedAt 解析失敗は致命傷ではない（巡回が StartTime fallback になるだけ）
    }
    return m;
}

// watch v3_guest (個別動画詳細用: 旧 NicoVideoModel(XElement) の置換)
// FromEssential と同様に try/catch で囲み、解析失敗時は Status=Delete を返す（OnLoaded 経由の例外伝播を回避）
//
// **重要 (実機 curl 検証 2026-04-25, 動画 4本確認)**:
//   - チャンネル動画: data.channel = {id: "ch...", name, ...}, data.owner = null
//   - ユーザー動画:   data.channel = null,                     data.owner = {id: int, nickname, ...}
//   - **判定キー: data.channel != null → チャンネル動画** (旧 NicoVideoModel.cs:65-67 のコメントアウト判定と同仕様)
//   - data.channel.id はチャンネル動画ですでに "ch..." 形式 (essential と同じ prefix 込み)
//   - data.owner.id は数値 (int)。DynamicUtil.S が string 化するので OK
public static NicoVideoModel FromWatchData(dynamic data)
{
    var m = new NicoVideoModel();
    try
    {
        m.ContentId = DynamicUtil.S(data, "video.id");
        m.Title = DynamicUtil.S(data, "video.title");
        m.Description = DynamicUtil.S(data, "video.description");
        m.ThumbnailUrl = DynamicUtil.S(data, "video.thumbnail.url");
        m.ViewCount = DynamicUtil.L(data, "video.count.view");
        m.CommentCount = DynamicUtil.L(data, "video.count.comment");
        m.MylistCount = DynamicUtil.L(data, "video.count.mylist");
        m.StartTime = DateTimeOffset.Parse(DynamicUtil.S(data, "video.registeredAt")).DateTime;
        m.Duration = TimeSpan.FromSeconds(DynamicUtil.L(data, "video.duration"));

        if (data.IsDefined("tag") && data.tag != null)
        {
            foreach (var t in data.tag.items)
                m.Tags.Add(DynamicUtil.S(t, "name"));
        }

        // **判定**: data.channel が非 null → チャンネル動画 (id は既に "ch..." 形式)
        // **DynamicUtil.O の null 安全限界の対処** (§1.3 重要5 / 本節注 参照):
        //   data.channel はユーザー動画で「定義済かつ値 null」のため、`DynamicUtil.S(data, "channel.id")`
        //   のドット記法を使うと再帰中に NRE を投げる。よって IsDefined + 明示 null チェック + 段階的アクセスで防御。
        if (data.IsDefined("channel") && data.channel != null)
        {
            m.UserInfo.SetUserInfo(
                DynamicUtil.S(data.channel, "id"),
                DynamicUtil.S(data.channel, "name"));
        }
        else
        {
            // ユーザー動画: data.owner を参照 (こちらも IsDefined + null チェック)
            if (data.IsDefined("owner") && data.owner != null)
            {
                m.UserInfo.SetUserInfo(
                    DynamicUtil.S(data.owner, "id"),
                    DynamicUtil.S(data.owner, "nickname"));
            }
        }

        m.RefreshStatus();
        m._beforedisplay = false;
    }
    catch (Exception ex)
    {
        MessageService.Exception(ex);
        m.Status = VideoStatus.Delete;
    }
    return m;
}
```

**注 (DynamicUtil.O の null 安全性 — 重要)**: `DynamicUtil.S(data, "a.b.c")` のドット記法は [DynamicUtil.cs:8-20](../TBird.Core/IO/DynamicUtil.cs#L8-L20) の `O()` 再帰実装に対応しているが、**完全な null 安全ではない**。

`O()` の null ガードは **`IsDefined` のみ** で、再帰呼出の冒頭 `value.IsDefined(...)` を null に対して呼ぶと NullReferenceException を投げる。挙動は次の通り:

| 中間ノードの状態 | 結果 |
|---|---|
| **未定義**（キーそのものが無い） | null 返却 ✓ |
| **定義済かつ値 null** | 次回再帰の `IsDefined` で **NRE** ✗ |
| 通常の値 | 正常進行 ✓ |

nvapi は `data.channel`/`data.owner`/`owner.channel` 等を **明示的に null** で返すフィールドが多いため、深い path をドット記法で辿る用途には**注意が必要**。

**本プランの対処方針**:
- `FromEssential`/`FromMylistItem`/`FromWatchData` は全体を try/catch で囲み、NRE を `Status=Delete` 化で吸収（既定の安全網）
- `essential.thumbnail.url` のように **中間ノードが必ず非 null** であるレスポンス (essential の thumbnail/count 等) は従来通りドット記法を使ってよい
- nvapi で「明示的 null」が観測されているフィールド (`data.channel`, `data.owner`, `data.owner.channel` 等) を経由する場合は、ドット記法ではなく **`IsDefined` + null チェック + 段階的アクセス** を行う（`FromWatchData` の判定参照）
- `DynamicUtil.O` 自体に null ガードを足す改修は **本プランの対象外**（`DynamicUtil.S/I/L/D/T<>` 全経路の挙動変化と影響範囲評価が別タスクで必要）。本プランはあくまで try/catch + 手動チェックで防御する

削除:
- `NicoVideoModel(XElement xml)` (行74-94)
- `NicoVideoModel(XElement item, string, string, string)` (行96-125)
- `ToDuration(XElement)` (行137-145)
- `GetData` (行147-153)
- `ToCounter` (行155-162)
- `ToRankingDatetime` (行164-174)

残す:
- `NicoVideoModel(dynamic item)` (snapshot search用 - 行27-51)
- `ToDuration(string)` (行127-135)

### 3.4 [_Apps/Nico.Controls/NicoMylistModel.cs](../_Apps/Nico.Controls/NicoMylistModel.cs)

書き換え:
```csharp
public static async Task<dynamic> GetNicoMylistData(string id)
{
    var cleanId = NicoUtil.Url2Id(id);
    // 最新 addedAt を MylistDate に使うため、addedAt降順で1件取得
    return await NicoUtil.GetNvapiJsonAsync(
        $"https://nvapi.nicovideo.jp/v2/mylists/{cleanId}?sortKey=addedAt&sortOrder=desc&pageSize=1&page=1");
}

public NicoMylistModel(string id, dynamic json)
{
    // GetJsonAsync が null を返す可能性 (403/404) — null時は最低限のフィールドだけ埋める。
    // **重要**: json 非 null でも data または mylist が null/未定義のケース (権限不足時に
    // meta だけ返るレスポンス等) をガード。DynamicUtil.S(ml, "name") は ml=null だと
    // 内部の O() で NRE を投げるので、ml の存在を try で確認してから進める
    dynamic ml = null;
    try
    {
        if (json != null && json.IsDefined("data") && json.data != null
            && json.data.IsDefined("mylist") && json.data.mylist != null)
        {
            ml = json.data.mylist;
        }
    }
    catch { ml = null; }

    if (ml == null)
    {
        MylistId = id;
        MylistTitle = "";
        MylistDate = DateTime.MinValue;
        MylistDescription = "";
        UserInfo = new NicoUserModel();
        return;
    }

    MylistId = id;
    // **null防御**: GetMylistTitle 内 `Aggregate(value, (s, c) => s.Replace(...))` は value=null だと NRE。
    //   nvapi の `name` は通常 string だが、レスポンスでフィールド欠如→ null の可能性に備え `?? ""` で吸収
    MylistTitle = GetMylistTitle(DynamicUtil.S(ml, "name") ?? "");
    MylistDescription = DynamicUtil.S(ml, "description");

    // RSS lastBuildDate 相当: 最新追加アイテムの addedAt を採用
    // (実機 curl 検証 2026-04-25 で ml に createdAt/updatedAt フィールドは存在しないことを確認)
    // items が空の場合は MinValue
    string addedAt = null;
    if (ml.IsDefined("items"))
    {
        foreach (var it in ml.items)
        {
            addedAt = DynamicUtil.S(it, "addedAt");
            break;  // 先頭1件のみ参照
        }
    }
    MylistDate = string.IsNullOrEmpty(addedAt)
        ? DateTime.MinValue
        : DateTimeOffset.Parse(addedAt).DateTime;

    UserInfo = new NicoUserModel();
    // owner は essential の owner と同形 (ownerType, id, name, iconUrl)。
    // 通常 mylist owner は user (ownerType="user", id=数値文字列) だが、
    // 念のため id をそのまま渡す (owner.id がチャンネル形式 "ch..." でも SetUserInfo が正しく分岐する)
    UserInfo.SetUserInfo(
        DynamicUtil.S(ml, "owner.id"),
        DynamicUtil.S(ml, "owner.name"));

    UserInfo.AddOnPropertyChanged(this, (sender, e) =>
    {
        ThumbnailUrl = UserInfo.ThumbnailUrl;
    }, nameof(UserInfo.ThumbnailUrl), true);
}
```

呼び出し元更新:
- [NicoSearchHistoryViewModel.cs:94](../_Apps/Nico.Controls/NicoSearchHistoryViewModel.cs#L94) `GetNicoMylistXml` → `GetNicoMylistData`
- **重要 (例外ハンドリング)**: `GetNvapiJsonAsync` 内部の `WebUtil.GetJsonAsync(url, headers)` は HTTP 4xx/5xx は null 吸収するが **ネットワーク例外時は `TryCatch<string>` が `ArgumentNullException` を再投する** (§3.1.3)。つまり `await NicoMylistModel.GetNicoMylistData(Word)` は null 戻り or 例外スローの両方が起こり得る。
- 戻り値 null はコンストラクタ側で吸収可能だが、**例外は `Loaded` ハンドラに伝播してしまう**ため、呼び出し側 `GetDisplay` を try/catch で囲み null フォールバックさせる必要がある:
  ```csharp
  case NicoSearchType.Mylist:
      dynamic json = null;
      try { json = await NicoMylistModel.GetNicoMylistData(Word); } catch { /* 既存と同等の握り潰し */ }
      return new NicoMylistViewModel(new NicoMylistModel(Word, json));  // null でもコンストラクタが空モデル化
  ```
- 同じく `case NicoSearchType.User: NicoUserModel.GetUserInfo(Word)` 経路も `GetNickname` 内 try/catch で吸収するため例外は出ないが、念のため呼出 try/catch を共通化しておくと安全

**備考**: [nico-combo-setting.xml:7-10](../_Apps/lib/nico-combo-setting.xml#L7-L10) の `mylist_title_removes` はRSS `<title>` の `‐ニコニコ動画` 除去用。nvapiの `name` は純粋タイトルなので **空振りするが無害**。放置（別PR検討）。

### 3.5 [_Apps/Nico.Controls/NicoUserModel.cs](../_Apps/Nico.Controls/NicoUserModel.cs)

**置換範囲を明示**: [NicoUserModel.cs:39-58](../_Apps/Nico.Controls/NicoUserModel.cs#L39-L58) の `GetNickname` の **`try { ... }` ブロック内部（旧 `seiga.nicovideo.jp/api/user/info` の URL構築・XML解析）のみ** を nvapi 呼び出しに置換する。**外側の `Locker` ロック取得・`_nicknames` キャッシュ取り出し・`catch` 処理はそのまま保持**。`_nicknames` フィールド・`_nicknamelock` フィールドの定義（行70-71）も変更しない。

置換後の try ブロック:
```csharp
try
{
    var json = await NicoUtil.GetNvapiJsonAsync(
        $"https://nvapi.nicovideo.jp/v1/users/{userid}");
    if (json == null) return userid;  // GetJsonAsync の null 吸収

    // ドット記法で json.data.user の null も安全に吸収。
    // 実機 curl 検証 2026-04-25: data.user.id は int、data.user.nickname は string
    var nickname = DynamicUtil.S(json, "data.user.nickname");

    // **毒キャッシュ防止 (旧バグ修正)**: nickname が null/空の場合は _nicknames に保存しない。
    //   保存してしまうと次回以降ずっと null/空が返り続け、別呼び出しで nickname 取得ができなくなる。
    //   旧 seiga 版は **nickname タグ欠如時に null をそのまま `_nicknames[userid]` に保存していた潜在バグ** あり
    //   ([NicoUserModel.cs:50-52] の `_nicknames[userid] = (string)xml...FirstOrDefault();`)。
    //   旧コードは「例外時のみ」キャッシュ更新を回避していたが、null nickname の場合は防げていなかった。
    //   nvapi 移行に合わせてこのバグも修正する。
    if (string.IsNullOrEmpty(nickname)) return userid;
    return _nicknames[userid] = nickname;
}
catch
{
    return userid;
}
```

XMLコメント（行60-67）は削除（旧API仕様書きのため不要）。

### 3.6 [_Apps/lib/nico-combo-setting.xml](../_Apps/lib/nico-combo-setting.xml)

- `rank_period` の value を `hour/24h/week/month/total` に改訂
- `oyder_by_mylist` の display を `sortKey,sortOrder` 文字列形式に全書換
- `oyder_by_user` の `regdate-`/`regdate+` を **削除**（display重複: `registeredAt,desc/asc` が stadate と被るため。詳細は 1.4 節）

**ユーザー設定値の互換性**: `NicoSetting.Instance.NicoRankingPeriod` には旧 value (`hourly`/`daily`/`weekly`/`monthly`/`total`) が保存されている可能性がある。新 XML には該当 value が存在しないため、[ComboboxViewModel.cs:31](../TBird.Wpf/_ROOT/ComboboxViewModel.cs#L31) `GetItemNotNull` のフォールバック (先頭項目 = `hour`) が効いてクラッシュは無いが、**過去の選択（例: "日間"）が初回起動時に "時間" に化ける UX 劣化**がある。リグレッションではないが既知事象として §5 リスクに記載。`oyder_by_user` の `regdate-` 削除も同様の保存値ミスマッチを生むが、こちらはコード側 normalization (§1.4) が吸収する。

### 3.7 呼び出し側の変更（`await foreach` 化）

`IAsyncEnumerable` 化に伴い、4ファイルの呼び出しパターンを書き換え。

#### 連続発火時の競合対策（NicoSearch / NicoFavorite）

**問題**: `IAsyncEnumerable` 化により、ユーザがSearchボタン連打すると、前回の `await foreach` がまだ稼働中に2回目が `Sources.Clear()` → 前回の残ループが新Sourcesに古い結果を混入させる競合が発生する。NicoRanking は既に `using (await LockAsync())` でシリアライズ済みのため対象外、PatrolFavorites も timer 直列起動のため対象外。**対策が必要なのは NicoSearch / NicoFavorite のみ**。

**3案比較**:

| 案 | 概要 | UX | 実装コスト | HTTPコール削減 |
|---|---|---|---|---|
| A. LockAsync 追加 | NicoRankingと同様 `using (await LockAsync())` を OnSearch に巻く | 長尺マイリスト読込中はSearch不能になる × | 小（1行追加） | × (旧ループも完走) |
| B. 世代カウンタ | `int _gen` を増分し、ループ中 `_gen != myGen` なら return | ボタン即時応答 ○、UI更新は最新のみ | 中（各ViewModel数行）  | × (旧HTTPは完走) |
| C. CancellationToken | NicoUtilメソッドに `[EnumeratorCancellation]` 引数追加、`_cts` 切替 | 完全キャンセル ◎ | 大（NicoUtil全メソッド署名変更 + 各呼出側） | ○ (旧HTTP即停止) |

**推奨: B（世代カウンタ）**。理由:
- 確認事項#5「キャンセル処理は今回入れない」と整合（HTTPは握り潰し）
- NicoUtil 側の署名変更不要 → 影響範囲が呼出側のみに局所化
- 長尺マイリスト中でも次のSearchが即時開始可能
- 旧HTTPコールは数百ms内に終わるため浪費は許容範囲

**実装パターン**（NicoSearchViewModel / NicoFavoriteViewModel 共通）:
```csharp
private int _searchGen;  // Interlocked/Volatile アクセス専用

public ICommand OnSearch => _OnSearch = _OnSearch ?? RelayCommand.Create<NicoSearchType>(async t =>
{
    var myGen = Interlocked.Increment(ref _searchGen);
    Sources.Clear();
    try
    {
        await foreach (var item in NicoUtil.GetVideoBySearchType(Word, t, Orderby.SelectedItem.Value))
        {
            if (Volatile.Read(ref _searchGen) != myGen) return;  // 後発呼出に追い越されたら離脱
            Sources.Add(item);
        }
    }
    catch { }
    if (Volatile.Read(ref _searchGen) == myGen) NicoModel.AddSearch(Word, t);  // 履歴登録も最新世代のみ
});
```

**注意点**:
- **`Interlocked.Increment` + `Volatile.Read` でメモリ可視性を保証**。`await foreach` の継続が UI スレッドに戻る保証は SynchronizationContext 設定次第（テストや将来の構成変更で破綻し得る）。1行のコストでバグを未然に防ぐ
- `Sources.Clear()` は世代増分後に呼ぶ（旧ループの最後の `Add` が Clear 前に滑り込んでも、次の Clear で消える）
- NicoFavoriteViewModel の OnSearch も同パターンを適用
- **AddSearch 挙動の変化 (UX)**: 旧コードは検索完了後に必ず `NicoModel.AddSearch(Word, t)` を呼んでいたが、本パターンでは**最新世代のみ履歴登録**となる。連打時の中間検索が履歴に残らない。意図的変更として認識する（履歴肥大化を防ぐ副次効果あり）。NicoFavoriteViewModel は元々 AddSearch を呼んでいないため影響なし

#### [_Apps/Nico.Workspaces/NicoRankingViewModel.cs:67-77](../_Apps/Nico.Workspaces/NicoRankingViewModel.cs#L67-L77)
```csharp
private async Task Reload()
{
    try
    {
        Sources.Clear();
        await foreach (var item in NicoUtil.GetVideosByRanking(Genre.SelectedItem.Value, "all", Period.SelectedItem.Value))
            Sources.Add(item);
    }
    catch { }
}
```

#### [_Apps/Nico.Workspaces/NicoSearchViewModel.cs:63-72](../_Apps/Nico.Workspaces/NicoSearchViewModel.cs#L63-L72)
上記「連続発火時の競合対策」の世代カウンタパターンを適用。

#### [_Apps/Nico.Workspaces/NicoFavoriteViewModel.cs:50-57](../_Apps/Nico.Workspaces/NicoFavoriteViewModel.cs#L50-L57)
NicoSearch と同パターンの世代カウンタで `await foreach` 化。コマンド引数が `NicoSearchType` ではなく `NicoSearchHistoryViewModel` である点に注意:
```csharp
private int _searchGen;  // Interlocked/Volatile アクセス専用 (NicoSearch と同様、§3.7 注 / 確定事項 #30 参照)

public ICommand OnSearch => _OnSearch = _OnSearch ?? RelayCommand.Create<NicoSearchHistoryViewModel>(async vm =>
{
    var myGen = Interlocked.Increment(ref _searchGen);
    Sources.Clear();
    try
    {
        await foreach (var item in NicoUtil.GetVideoBySearchType(vm.Word, vm.Type, Orderby.SelectedItem.Value))
        {
            if (Volatile.Read(ref _searchGen) != myGen) return;
            Sources.Add(item);
        }
    }
    catch { }
});
```

#### [_Apps/Core.Windows/MainViewModel.cs:76-86](../_Apps/Core.Windows/MainViewModel.cs#L76-L86)
現行: `enumerable.Where(x => m.Date < x.StartTime).ToArray()` — `IAsyncEnumerable` には同期 `Where` が無い。
**System.Linq.Async パッケージは追加せず、`await foreach` 内で手書きフィルタする**:

```csharp
private async Task PatrolFavorites()
{
    foreach (var m in NicoModel.Favorites)
    {
        // 旧コードは Where(...).ToArray() で初期 m.Date を捕捉してフィルタしていた。
        // 新コードは IAsyncEnumerable を逐次評価するため、ループ内で m.Date を更新すると
        // 次イテレーションのフィルタ判定値が変わってしまう。
        // regdate- は降順なので、初回更新後は以降のアイテムが必ず脱落 → 最新1件しか追加されないバグ。
        // → ループ前に initialDate を捕捉して固定値で判定する。
        var initialDate = m.Date;
        await foreach (var video in NicoUtil.GetVideoBySearchType(m.Word, m.Type, "regdate-"))
        {
            // **比較フィールドの整合 (重要)**:
            //   nvapi で `regdate-` を投げると種別ごとに並び順とフィールドが異なる。
            //     - User/Word/Tag: registeredAt 降順 (= video.StartTime と同義)
            //     - Mylist:        addedAt 降順 (= video.MylistAddedAt)
            //   StartTime 一本で比較すると Mylist 種別で「最近マイリスト追加された古い動画」が
            //   先頭に来た場合 break が早すぎて新規追加を取りこぼす。
            //   → MylistAddedAt が設定されていればそちらを優先 (FromMylistItem が設定する)
            var compareDate = video.MylistAddedAt ?? video.StartTime;
            if (compareDate <= initialDate) break;  // 降順前提なので以降は全て古い → break で打ち切り
            VideoUtil.AddTemporary(MenuMode.Niconico, video.ContentId, false);
            m.Date = Arr(m.Date, compareDate).Max();
        }
        VideoUtil.Save();
    }
}
```

注意:
- `initialDate` で固定判定 + 降順前提の `break` 打ち切りで、無駄なページング HTTP コールも抑制される（旧 RSS 1コール固定とは違い、nvapi はページングするため）
- `m.Date` の更新も `compareDate` ベース (Mylist 種別なら addedAt、それ以外は StartTime) で揃える。次回巡回時の `initialDate` と意味が一致する

### 3.8 BindableCollection のスレッド扱い（現行と同等）

[BindableCollection.cs:109-119](../TBird.Wpf/Collections/BindableCollection.cs#L109-L119) の `Add` は `lock(LockObject)` で内部リストを保護するのみで、UI スレッドへのマーシャリングは行わない。

しかし**現行コード**（[NicoRankingViewModel.cs:69-76](../_Apps/Nico.Workspaces/NicoRankingViewModel.cs#L69-L76) 等）は `Task.ContinueWith` のデフォルトスケジューラ（=スレッドプール）から `Sources.Add` を呼んでおり、既にバックグラウンドスレッドからの Add で稼働中。`BindableContextCollection` 経由で WPF 側はディスパッチされる構成になっている。

→ `await foreach` 化後も同じ経路を辿るため**挙動変化なし**。新規対策は不要。

---

## 4. 実装順序

**原則**: combo XML 改訂 → 該当 API 切替 の順 (combo の旧値が新 API に投げられて 400 になるウィンドウを作らないため)。**ただし combo XML 改訂と API 切替は必ず同一コミットで適用する** (XML だけ先 commit するとユーザー設定値が新 XML に存在しない過渡期に旧 API へ不正値が投げられる)。step 4↔5 / step 9↔10 が該当。

1. **WebUtil**:
   - 既存 `GetJsonAsync(url)` に null 吸収を追加（NRE バグ修正）
   - ヘッダ付き `GetStringAsync(url, headers)` / `GetJsonAsync(url, headers)` を追加
   - **同時に `SearchApiV2(dynamic json)` 冒頭に `if (json == null) yield break;` を追加** (§3.1.4 副作用対応)
2. **NicoUtil** に `GetNvapiJsonAsync` ヘルパ追加
3. **NicoVideoModel** に `FromEssential` / `FromMylistItem` / `FromWatchData` ファクトリ追加 + `MylistAddedAt` プロパティ追加（**全て try/catch で囲む**。旧XML版は一時残置、snapshot search 用 `NicoVideoModel(dynamic)` は残す）
4. **nico-combo-setting.xml** の `rank_period` 改訂 (`hour/24h/week/month/total`) — **⚠️ step 5 と同一コミット必須**
5. **NicoUtil.GetVideosByRanking** → `IAsyncEnumerable` 化 + nvapi ranking（**ユーザ報告の本丸**、100件固定）
   - **重要**: step 4 と step 5 は **必ず同一コミットで適用**。step 4 だけ先に commit すると、新 XML 値 (`hour` 等) を持たないユーザー設定が `GetItemNotNull` のフォールバック (`hour`) に化け、旧 RSS API (`?term=hour&rss=2.0`) に新値が投げられて 400 を返し、過渡期にランキング機能が完全に動かなくなる
6. **NicoUtil.GetVideo** → watch/v3_guest（**try/catch で囲み、`json.meta.status==200` チェックで FromWatchData 呼び出し**。失敗時 `Status=Delete`。getthumbinfo 自体は生きているが description全文/tags/`data.channel` ベースのチャンネル判定改善のため置換）
7. **nico-combo-setting.xml** の `oyder_by_user` から `regdate-`/`regdate+` を削除
8. **NicoUtil.GetVideosByNicouser** → `IAsyncEnumerable` 化 + nvapi users/videos（全件ページング、`oyder_by_user.Split(',')` 維持。**外側 overload で `regdate→stadate` 正規化を追加**。終端判定は `items.Count < 100`）
9. **nico-combo-setting.xml** の `oyder_by_mylist` 改訂 (`addedAt,desc` 等の sortKey,sortOrder 形式に全書換) — **⚠️ step 10 と同一コミット必須**
10. **NicoUtil.GetVideosByMylist** → `IAsyncEnumerable` 化 + nvapi mylists（全件ページング、**`FromMylistItem` 使用で addedAt 保持**、終端判定は `data.mylist.hasNext`）
    - **重要**: step 9 と step 10 は **必ず同一コミットで適用**。step 9 だけ先に commit すると、旧 RSS API (`mylist?rss=2.0&sort=addedAt,desc`) に新フォーマットが投げられて 400 を返し、過渡期にマイリスト検索が完全に動かなくなる。レビュー単位を分割する場合でも cherry-pick 順序を 10→9 に逆転させてはいけない
11. **NicoUtil.GetVideosByWord / GetVideosByTag / GetVideoBySearchType** → `IAsyncEnumerable` 化（内容は現状維持、snapshot search コンストラクタ継続使用、§3.1.4 の null ガードを忘れずに）
12. **呼び出し側4ファイル**を `await foreach` に書き換え:
    - NicoRanking: 既存 LockAsync 内で `await foreach`
    - NicoSearch: 世代カウンタパターン（プラン3.7参照）
    - NicoFavorite: 世代カウンタパターン（プラン3.7参照、引数は `NicoSearchHistoryViewModel`）
    - **MainViewModel.PatrolFavorites: `initialDate` 捕捉 + `MylistAddedAt ?? StartTime` で比較 + 降順 break 打ち切り**（プラン3.7の重要修正）
13. **NicoMylistModel** → nvapi mylists（XElement → dynamic、MylistDate は最新 `items[0].addedAt` で代替）+ NicoSearchHistoryViewModel の呼び出し更新
14. **NicoUserModel.GetNickname** → nvapi users（**毒キャッシュ防止ガード入り**、§3.5 参照）
15. 旧XMLコンストラクタ・ヘルパ・`GetXmlChannelAsync` を削除
16. ビルド確認 (`dotnet build /c/Work/Github/TBird.Library/_Apps/App.sln --no-restore`) と手動動作確認

---

## 5. リスク・未決事項

- **nvapi は非公式API**（公式SPAが利用）。予告なく仕様変更されるリスクは RSS廃止と同じ
- **大規模マイリスト/ユーザの全件ロード**: 2000件なら20コール≈数秒。遅延追加ロードで UX は損なわないが、途中で View 切替された場合の中断処理は今回入れない（TryCatch握り潰し、既存同様）
- **tags欠落（一覧表示時）**: essential にタグ無し。既存の `OnLoaded → GetVideo` 遅延補完で埋まる設計を維持できるので、現状より劣化しない（むしろ getthumbinfo 復旧で改善）
- **DynamicJson の子オブジェクト null**: `essential.thumbnail` 等が null の場合は `FromEssential` の try/catch 全体で `Status = VideoStatus.Delete` になる（既存の XElement 版の落ち方と同等）。**追加の防御コードは不要**
- **BindableCollection のスレッド扱い**: `await foreach` の continuation が UI スレッドに戻らない場合の Add 挙動を実装時確認。必要なら dispatcher 経由化
- **自動ユニットテスト無し**: リグレッションは手動確認のみ
- **watch/v3_guest のレート制限懸念**: ランキング100件表示時、`_beforedisplay=true` の各動画が `OnLoaded` で個別に `watch/v3_guest` を叩くため最大100並列コールが発生。getthumbinfo 時代から同じ挙動だが、watch/v3_guest 側のレート制限が厳しい場合は429等が出る可能性。実害発生時のみ対策（既存の Polly リトライ [WebUtil.cs:46-48](../TBird.Web/_ROOT/WebUtil.cs#L46-L48) で吸収される想定）
- **PatrolFavorites 初回パトロールのページング過大**: User 種別お気に入りで `m.Date` が古い日付のまま残っていると、`compareDate <= initialDate` の break が長く成立せず、最終ページまで到達するケースがある (例: 2700 件投稿ユーザーで最大 28 HTTP コール ≈ 数秒)。初回到達後は `m.Date` が更新され次回以降は最新数件で収束するため、一過性のコスト。旧 RSS は1コール固定だったため移行直後の初回タイマー実行で一時的な負荷増になる
- **combo XML 設定値ミグレーション**: `rank_period` の value 変更 (`hourly`→`hour` 等) により、ユーザー設定 `NicoSetting.NicoRankingPeriod` の保存値が新 XML に存在しないケースが発生。`GetItemNotNull` のフォールバックで先頭項目 (`hour`) になるためクラッシュは無いが、過去選択 (例: "日間") が初回起動で "時間" に化ける UX 劣化あり。`oyder_by_user` の `regdate-/+` 削除はコード側 normalization で吸収される (§1.4)
- **PatrolFavorites 初回 Mylist 再発見 (軽微)**: Mylist 種別の `m.Date` は旧コード ([MainViewModel.cs:83](../_Apps/Core.Windows/MainViewModel.cs#L83) `m.Date = Arr(m.Date, video.StartTime).Max();`) では **動画の `StartTime` (= registeredAt = 投稿日時) の最大値** を保持していた (RSS `<pubDate>` 由来)。新コードは Mylist 種別のみ `addedAt` (= マイリスト追加日時) ベースに変わる。**意味的軸が異なる**ため、移行直後の初回パトロールで `compareDate(addedAt) > initialDate(maxStartTime)` が成立して**過去にすでに `AddTemporary` されたアイテムを再投入**する可能性がある (例: 古い video が最近マイリスト追加された場合、addedAt > 過去の max(StartTime) になりやすい)。
  - **実コード調査結果 (2026-04-25)**: [VideoHistoryModel.cs:57-68](../_Apps/Core/VideoHistoryModel.cs#L57-L68) の `AddModel` は **冪等**（既存アイテムは `Date = DateTime.Now` で更新するのみ、新規 Add しない）
  - **影響範囲**: 件数増加・データ破壊なし。ただし**既存アイテムの `Date` が `DateTime.Now` で上書きされる副作用**あり → Temporary タブが Date 降順ソートの場合、再発見されたアイテムが先頭に浮上する一時的な UX 違和感
  - **対応方針**: workaround 不要。ユーザへの案内も不要。一過性のソート順乱れは数日以内で自然解消（次回以降のパトロールでは `m.Date` が `addedAt` 軸に揃うため再発見が起きない）
- **チャンネル所有マイリストの owner 構造未検証**: 実機 curl 検証 (2026-04-25) は通常のユーザー所有マイリストでのみ実施。チャンネル公式まとめ等で `data.mylist.owner.id` が `"ch..."` 形式で来るかは未確認。`NicoUserModel.SetUserInfo` の `id.StartsWith("ch")` 分岐で吸収できる構造ではあるが、動作確認時の補助観察項目として明記。万一 owner 構造がチャンネル時に異なる (例: `owner` ではなく `channel` フィールドに格納) 場合は `NicoMylistModel` コンストラクタで分岐追加が必要

---

## 6. ユーザ確認済み事項（確定）

1. **mylistソート解釈**: `regdate = addedAt`, `stadate = registeredAt` で確定
2. **`WebUtil.GetXmlAsync` の削除**: **削除しない**（残置）
3. **`mylist_title_removes`**: 機能空振りのまま放置
4. **件数制約**:
   - ranking: 100件固定
   - mylist/user: **全件取得、遅延追加ロード**（初期100件 + 非同期追加）
   - 戻り値を全 `IAsyncEnumerable<NicoVideoModel>` に統一（検索系 Word/Tag も）
5. **キャンセル処理**: HTTPレベルのキャンセル（CancellationToken）は今回入れない（TryCatch握り潰し、既存同様）。ただし `IAsyncEnumerable` 化に伴う UI 上の競合（NicoSearch/NicoFavorite の連打）には**世代カウンタ方式**で対応（プラン3.7参照）。NicoRanking は既存 `LockAsync` で対処済み
6. **MylistDate**: nvapi に lastBuildDate/createdAt 相当が無いので、**最新 addedAt（items を addedAt降順 pageSize=1 で1件取得）** で代替
7. **`WebUtil.GetJsonAsync(url, headers)`**: HTTPエラー時の null を内部で吸収し、呼び出し側は `json == null` で握り潰す
8. **NicoVideoModel コンストラクタ衝突**: `dynamic` シグネチャ衝突を避けるため、essential/watch-v3-guest は両方ファクトリメソッド (`FromEssential`/`FromWatchData`) に統一。snapshot search 用 `NicoVideoModel(dynamic item)` は既存維持
9. **`IAsyncEnumerable` の Where**: `System.Linq.Async` パッケージは追加せず、`MainViewModel.PatrolFavorites` 内で `await foreach` + 手書きフィルタで対応
10. **getthumbinfo の状態認識**: 「死亡」ではなく「稼働中だが essential/watch-v3-guest に置換することで品質改善」が正確。緊急優先ではなくランキング修正を最優先とする
11. **watch/v3_guest の認証**: 実機 curl 検証（2026-04-25）の結果、**クエリパラメータ `_frontendId/_frontendVersion` だけで認証完結**。ヘッダ `X-Frontend-Id`/`X-Frontend-Version` は不要（あっても害なし）。`GetVideo` は既存ヘッダ無し版 `WebUtil.GetJsonAsync(url)` で呼び出す。`GetNvapiJsonAsync` は nvapi.nicovideo.jp 用途専用
12. **DynamicJson のキャスト方針**: `(IEnumerable<object>)` `(object[])` `(dynamic)` のキャストは付けず、`foreach (var x in dyn.array)` および `DynamicUtil.S(dyn, "key")` のまま使う。既存の [NicoUtil.cs:155-161](../_Apps/Nico.Core/NicoUtil.cs#L155-L161) `SearchApiV2` 実装と同じイディオム
13. **NicoSearch/NicoFavorite 連続発火対策**: 世代カウンタ方式（`Interlocked.Increment(ref _searchGen)` の増分・ループ内 `Volatile.Read` チェック・離脱）で実装。HTTPのキャンセルはせず UI 更新のみ抑止。**仕様変更 (ユーザ確定 2026-04-25, B 案採用)**: NicoSearch の `NicoModel.AddSearch` 呼出は「最新世代のみ登録」に変更。連打時の中間検索は履歴に残らない（履歴肥大化抑制の副次効果あり）。実装は §3.7 のコードそのまま — AddSearch 呼出を世代チェック内 (`if (Volatile.Read(ref _searchGen) == myGen)`) に維持する
14. **PatrolFavorites のフィルタ仕様**: `IAsyncEnumerable` 逐次評価で `m.Date` 更新が次イテレーションのフィルタ判定に影響するバグを回避するため、ループ前に `var initialDate = m.Date;` で固定値を捕捉。`regdate-` 降順前提で `video.StartTime <= initialDate` 時に `break` で打ち切り（無駄なページングコール抑制）
15. **既存 `WebUtil.GetJsonAsync(url)` の NRE バグ**: `DynamicJson.Parse(null)` の NullReferenceException を回避するため、ヘッダ付き版と同じ null 吸収パターン（`string.IsNullOrEmpty` 判定）に統一する
16. **`FromWatchData` の例外耐性**: `FromEssential` と同様に try/catch で囲み、解析失敗時は `Status=Delete` 化（OnLoaded 経由の例外伝播を回避）
17. **`oyder_by_user` の重複削除**: user投稿動画は addedAt と registeredAt が同義のため、`regdate-`/`regdate+` を XML から削除。コード側は `GetVideosByNicouser` の外側 overload で `regdate→stadate` への正規化処理を追加（後方互換: 既存設定ファイルに `regdate-` が保存されていてもクラッシュしない）
18. **BindableCollection スレッド扱い**: 現行コード（`Task.ContinueWith` のスレッドプール経由）で既にバックグラウンド Add が稼働中。`BindableContextCollection` 経由で WPF 側はディスパッチされる構成のため、`await foreach` 化後も挙動変化なし（追加対策不要）
19. **`SearchApiV2(dynamic json)` の null 副作用**: §3.1.1 の `WebUtil.GetJsonAsync(url)` null 吸収化に伴い、[NicoUtil.cs:155-161](../_Apps/Nico.Core/NicoUtil.cs#L155-L161) `SearchApiV2(dynamic json)` の冒頭に `if (json == null) yield break;` を追加必須。詳細は §3.1.4
20. **PatrolFavorites の比較フィールド整合**: nvapi で `regdate-` を投げると種別ごとに並び順が異なる (User/Word/Tag は registeredAt 降順、Mylist は addedAt 降順)。`StartTime` 一本で break すると Mylist 種別で取りこぼす。`NicoVideoModel.MylistAddedAt` (DateTime?) を新設し、`FromMylistItem` で設定、PatrolFavorites は `video.MylistAddedAt ?? video.StartTime` で比較 (§3.3, §3.7)
21. **NicoUserModel.GetNickname の毒キャッシュ防止**: nickname が null/空文字の場合は `_nicknames` に保存せず `userid` を返す。保存してしまうと次回以降の取得が永続的に空になる (§3.5)
22. **owner.id の prefix 仕様 (実機 curl 検証 2026-04-25, 動画 4本確認)**:
    - **essential**: `owner.id` は ownerType="channel" の場合**すでに `"ch2649997"` 形式で prefix 込み**、user の場合は `"1594318"` 素の数値。`"ch" + id` を追加してはいけない (`"chch..."` バグ)
    - **watch v3_guest**: 投稿者情報は動画種別で格納先が**完全に分かれる**。チャンネル動画は `data.channel = {id("ch..." 形式), name, ...}` で `data.owner = null`。ユーザー動画は逆 (`data.channel = null`, `data.owner = {id(int), nickname, ...}`)。**判定キー: `data.channel != null` → チャンネル動画**。旧 [NicoVideoModel.cs:65-67](../_Apps/Nico.Controls/NicoVideoModel.cs#L65-L67) のコメントアウト旧コードと同仕様。データ構造詳細は §1.3
    - **mylist v2 owner**: essential と同形 (ownerType, id, name, iconUrl)。channel 形式の owner も essential と同じく id に prefix 込み
23. **watch v3_guest の `actionTrackId`/`t` クエリ**: 空文字だと **400 INVALID_PARAMETER** が返る。既存 `GetNicoVideoUrl` ([NicoUtil.cs:55-60](../_Apps/Nico.Core/NicoUtil.cs#L55-L60)) は `MOVIEWER_{session}` と `t={session}` を埋めているため OK。プラン §1.3 のテーブルも空でない値で記載すること
24. **mylist v2 ループ終了判定**: `data.mylist.hasNext` (bool) が true/false で次ページ有無を返す。`totalItemCount` を別途カウントするより `hasNext` 一本でシンプルに判定可 (実機 curl 2026-04-25 確認)
25. **user videos v2 のラッパー**: `data.items[i]` は mylist と異なり `series` と `essential` の2フィールドを持つ。動画情報は `items[i].essential`。終端判定は `hasNext` フィールドが無いため `items.Count < pageSize` で代用
26. **実装順序**: combo XML 改訂は対応 API 切替の **前** に行う (§4 順序参照)。順序を逆にすると combo の旧値が新 API に投げられて 400 になる過渡期が発生する (1コミット粒度なら無視可だが、レビュー単位を切る場合に注意)
27. **NicoSearchHistoryViewModel.GetDisplay の例外ハンドリング**: `await NicoMylistModel.GetNicoMylistData(Word)` はネットワーク例外時 `ArgumentNullException` を再投する (`TryCatch<string>` の仕様)。null 戻りはコンストラクタが吸収するが**例外は Loaded ハンドラに伝播してアプリが落ちうる**ため、呼出側を try/catch で包み、catch 時は json=null を渡して空 NicoMylistModel を構築する (§3.4 呼出元更新セクション)
28. **NicoMylistModel コンストラクタの多段 null 防御**: json 非 null でも `data` または `mylist` が null/未定義のケース (権限不足時に meta だけ来る等) をガード。`json.IsDefined("data") && json.data != null && json.data.IsDefined("mylist") && json.data.mylist != null` を try で囲んで判定し、いずれか false なら早期 fallback ブランチへ (§3.4)
29. **FromEssential の owner null 防御**: 公式アカウント等で `essential.owner` 自体が null になる可能性に備え、`IsDefined+null チェック` で「owner 不在 → 投稿者情報空のまま続行」させる。全体 try/catch だけだと過剰削除 (Status=Delete) になるため、**owner 周りのみ早期スキップ**して動画情報自体は保存する (§3.3)
30. **`_searchGen` のメモリ可視性**: `++_searchGen` ではなく `Interlocked.Increment(ref _searchGen)`、ループ内チェックは `Volatile.Read(ref _searchGen)` を使用。await foreach の継続が UI スレッドに戻る保証は SynchronizationContext 設定次第のため、明示的なメモリバリアを置く (§3.7)
31. **PatrolFavorites 初回 Mylist 再発見 (軽微)**: 旧 `m.Date` は **`StartTime` の最大値** ([MainViewModel.cs:83](../_Apps/Core.Windows/MainViewModel.cs#L83) で確認)、新 `compareDate` は Mylist 種別のみ `addedAt` ベース。**意味的軸の不一致**で、移行直後の初回パトロールが過去アイテムを再 `AddTemporary` する可能性あり (§5)。**実コード調査済 (2026-04-25)**: [VideoHistoryModel.cs:57-68](../_Apps/Core/VideoHistoryModel.cs#L57-L68) の `AddModel` は冪等（既存なら Date 更新のみ、新規 Add しない）。件数増加・データ破壊なし、Date 上書きによる Temporary タブのソート順一時乱れのみ。workaround 不要
32. **combo XML 改訂と API 切替の同一コミット制約**: 
    - `rank_period` XML (step 4) と `GetVideosByRanking` API (step 5) は同一コミット必須。XML だけ先行 commit すると旧 RSS API に新値 (`term=hour`) が投げられて 400
    - `oyder_by_mylist` XML (step 9) と `GetVideosByMylist` API (step 10) は同一コミット必須。XML だけ先行すると旧 RSS に `sort=addedAt,desc` という不正値が飛んで Mylist 機能完全停止
    - cherry-pick 時も順序逆転禁止 (§4)
33. **NicoFavoriteViewModel の世代カウンタも Interlocked/Volatile 必須**: NicoSearch と同形でメモリ可視性保証。`++_searchGen` ではなく `Interlocked.Increment(ref _searchGen)`、ループ内チェックは `Volatile.Read(ref _searchGen)` を使用 (確定事項 #30 と同主旨、§3.7 のサンプルコード参照)
34. **NicoUserModel.GetNickname の毒キャッシュは旧バグ修正**: 旧 [NicoUserModel.cs:50-52](../_Apps/Nico.Controls/NicoUserModel.cs#L50-L52) は `_nicknames[userid] = (string)xml...FirstOrDefault();` で nickname タグ欠如時に **null をキャッシュに保存していた潜在バグ** あり (catch 経路では更新を回避していたが、null nickname の場合は防げず)。nvapi 移行に合わせて null/空 nickname は `_nicknames` に保存しないガードを追加 (確定事項 #21 を補強)
35. **NicoMylistModel `name` の null 防御**: nvapi `name` フィールド欠如時の `Aggregate(null, ...)` NRE を防ぐため `DynamicUtil.S(ml, "name") ?? ""` で吸収 (§3.4)。`mylist_title_removes` は本来空振りで無害だが、`name` が null だと `Aggregate` の seed が null になり Replace で例外
