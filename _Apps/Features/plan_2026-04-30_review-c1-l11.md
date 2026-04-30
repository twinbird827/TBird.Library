# レビュー指摘 修正プラン C1-L11 + N1-N4（2026-04-30）

## 改訂履歴

- **v10 (2026-04-30 / プラン妥当性レビュー第 6 回フィードバックを反映)**:
  1. **重要 1 / PR-6 (L-9) 修正 4-4 のリナンバリング対象に PR-7 Overlay Button を追加**: 推奨マージ順 `PR-7 → PR-6` のため、PR-6 着手時点で base には B-4 修正 e ([PR-7 line 2052-2056](#)) で投入された「自動 OFF + フッタ非表示時の既読ボタン単独 Overlay」が `Grid.Row="2"` で存在している。L-9 修正 4-4 の ReaderPage.xaml リナンバリング項目（旧 v9 では「ヘッダ Grid・ActivityIndicator・ScrollView・ReaderWebView・フッター Grid」の 5 個のみ）に **Overlay Button `"2"` → `"3"`** を追加。これに合わせて検証チェックリストの「ReaderPage の 5 個に注意」を **「6 個（Overlay Button 含む）に注意」** に訂正。漏れると Overlay がエラーバナー Row（新 Row 0）と衝突して表示崩れを起こすため必須対応。
  2. **軽微 1 / PR-7 B-4 修正 e の Grid.Row が PR-6 マージ後に変動する旨を明示**: 修正 e のサンプル XAML は `Grid.Row="2"` を前提にしているが、PR-6 (L-9) マージ後は `Grid.Row="3"` に変わる。実装着手時の base が PR-6 マージ前/後のどちらかで Grid.Row 値を切替える必要がある旨を B-4 修正 e セクションの注意点に追記。
  3. **軽微 2 / 検証項目の補強**: PR-6 (L-9) の検証チェックリストに「ReaderPage で `IsManualReadButtonOverlayVisible=true` 状態（自動 OFF + フッタ非表示）にしたときに、Overlay Button がエラーバナー Row と重ならず左下に正しく表示されること」を追加。Overlay Button のリナンバリング漏れを実機検証で必ず捕捉する。
- **v9 (2026-04-30 / プラン妥当性レビュー第 5 回フィードバックを反映)**:
  1. **重要 1 / B-4 救済 UI を「将来導入予定への先回り対応」として明示（dead code 前提）**: 実コード grep で `ToggleHeaderFooterCommand` の XAML/コードビハインド binding が **0 件**であることを確認した。現状では `IsFooterVisible=false` 経路が存在せず、B-4 修正 e の「自動 OFF + フッタ非表示」救済 Overlay は現時点で到達不能なシナリオへの対応となる。本プランでは設計を維持しつつ、(a) 救済 Overlay の存在意義は「将来 ToggleHeaderFooter binding が導入された際に既読化経路が失われない保険」であることを B-4 セクション冒頭で明示、(b) 事前確認チェックリストに **P-8** として `ToggleHeaderFooter` の binding 状況を追加、(c) もし将来 binding が削除/置換された場合は本 Overlay も同時に削除する旨を注記。
  2. **重要 2 / SeedSettingsAsync 3-way 衝突解決手順を明記**: PR-1 / PR-4 (L-1) / PR-7 (B-4) の 3 PR が同一 `defaults` 辞書を編集するため、推奨マージ順での rebase 衝突手順を「PR 分割」セクション直後に新規節として追加。各 PR がどの行を触るか / 衝突解決時に保持すべき項目 / ハードコード混入を防ぐ目視確認ポイントを明記。
  3. **重要 3 / 推奨マージ順の根拠を強化（現状順序据え置き）**: 「PR-3 を PR-2 の直後にマージすれば PR-2 H-4 由来の race window を構造的に解消できるが、PR-3 が PR-7 (N-1〜N-4) より先行すると N 系バグ修正のユーザ到達が 1 PR 分遅れる」という比較を明示。実害は重複 prefetch 1 回程度で軽微 vs N 系バグはユーザが日々遭遇する事実バグのため、現状順序を据え置く判断根拠を補強。
  4. **軽微 1 / NarouApiService.cs:43 の記述訂正**: 旧記述「[NarouApiService.cs:43](../Services/Narou/NarouApiService.cs#L43) は `word=` のみを使用」は、実コード上は `{wordParam}={encoded}` の変数経由（runtime で "Both" hardcoded のため結果的に `word=`）。「runtime 上は `word=` で動作するが line 43 のリテラル記述は変数経由」と表現を訂正。L-3 で switch 削除後に line 43 がリテラル `word=` になり、その状態に対して N-1 が `&title=1&wname=1` を追加する流れ。
  5. **軽微 2 / C-1 修正の `ctx` null annotation ガード**: 旧サンプル `var ctx = ApplicationContext;` は `Context?` 型推論となり、後続の `SchedulePeriodicCheck(Context context, ...)` 呼び出しで `<Nullable>enable</Nullable>` 環境では `CS8604` 警告が出る可能性がある。`var ctx = ApplicationContext ?? throw new InvalidOperationException("ApplicationContext is null in OnCreate");` への変更でガード。OnCreate 段階で `ApplicationContext` が null なケースは異常系のため throw で問題なし。
  6. **軽微 3 / EpisodeCacheRepository.DeleteByNovelIdSync の `internal` 明示**: 事前確認 P-4 と H-1 修正 1 の説明に「アクセス修飾子は `internal`（同一アセンブリ `LanobeReader` から呼び出し可能）」を追記。`NovelRepository.DeleteBySiteAndNovelIdAsync` は同一アセンブリ内のため `internal` のままで再利用可能であることを明示。
  7. **軽微 4 / N-2 デフォルト UX リスクへの注記追加**: 既定 `auto_mark_read_enabled=1` + 巻き戻し挙動は、設定変更しないユーザにとって「過去話を確認しただけで read_at が NULL に戻り復元不可」という攻撃的な挙動になり得る点を N-2 セクションに明示。本プラン段階では設計変更しない（ユーザ承認済み仕様のため）が、リリース後のフィードバック次第で「既定 0（OFF）化」を後続 PR で検討する余地を記載。
- **v8 (2026-04-30 / プラン妥当性レビュー第 4 回フィードバックを反映)**:
  1. **N-2 SQL を 2 文分割案に格上げ（重要 1 対応）**: 旧 v7 のサンプル SQL は 5 個の `?` placeholder を使う 1 つの一括 UPDATE で、v7 で「先例 0 件なら 2 文分割案に切り替え」と注記していた。**実コード調査で先例 0 件が確定**（EpisodeRepository での ExecuteAsync は最大 3 個まで）したため、**2 文分割案を本実装、1 文案を補足**に格下げ。コード可読性・既存パターンとの整合性も向上。
  2. **PR マージ順における race window の明示化（重要 2 対応）**: 旧推奨順 `PR-1 → PR-2 → PR-4 → PR-7 → PR-3 → PR-6 → PR-5` だと PR-2 (H-4) と PR-3 (M-2) の間に PR-4 と PR-7 が挟まり、その期間中は H-4 由来の race window が解消されない。本問題を PR-2 description に必須記載項目として追加し、実装者・レビュアーへの注意喚起を強化。**マージ順の変更は行わない**（ユーザ実害解消優先という旧プラン v4 の判断は据え置き、実頻度が低い race を一時的に容認する代わりに明示性を高める）。
  3. **B-4 修正 c の `[ObservableProperty]` 統一（重要 3 対応）**: 旧 v7 では修正 c で `private bool _autoMarkReadEnabled = true;` を提示し、修正 c2 で「`[ObservableProperty]` に格上げ」と注記する自己矛盾構造だった。修正 c のサンプルコード自体を `[ObservableProperty]` 形に書き換え、修正 c2 は OnPropertyChanged 連動と算出プロパティ追加のみに限定する形に整理。
  4. **行番号の細部訂正（軽微 1, 3 対応）**: SeedSettingsAsync の seed 追加位置 `117` 行目 → `116` 行目（"novel_sort_key" の実位置）。ReaderViewModel ToggleHeaderFooter 行番号 `252-257` → `253-256` に訂正。
  5. **H-2 の Worker タイムアウト懸念を追記（軽微 4 対応）**: WorkManager の Worker 実行時間制限（通常 10 分）に近づくケース（長編 + 大量登録ユーザ）で強制 cancel される可能性を H-2 セクションに明示。後続 PR での throttling 導入を 1 案として記録。
  6. **「実装着手前の事前確認チェックリスト」セクションを冒頭に追加**: v7 で散在していた事前確認項目（SeedSettingsAsync の挿入動作、`Result.InvokeRetry` binding 名、SQL placeholder 数、UNIQUE 制約 DDL、DeleteByNovelIdSync シグネチャ）を冒頭の「事前確認チェックリスト」に集約。実装者がプラン全体を読まずとも着手前のブロッカー確認を漏らさず実施できる構造に。
- **v7 (2026-04-30 / プラン妥当性レビュー第 3 回フィードバックを反映)**:
  1. **C-1 (Activity 参照リーク回避)**: 旧 v6 の `MainActivity.OnCreate` サンプルでは `_ = Task.Run(async () => { ... UpdateCheckScheduler.SchedulePeriodicCheck(this, ...) ... });` で `this`（Activity）を closure キャプチャしていた。最大 ~3 秒のリトライ + DB 初期化中に Activity ライフサイクルを跨ぐ可能性があり、Activity 破棄後に WorkManager 呼び出しが走るとリーク/例外の懸念があった。**`var ctx = ApplicationContext;` を Task 開始前に取得し、closure には Activity ではなく ApplicationContext を渡す**よう修正。WorkManager は Application Context を要求するため意味的にも妥当。
  2. **PR-7 B-4 (自動 OFF + フッタ非表示の詰み回避)**: 旧 v6 では「自動既読化 OFF + ToggleHeaderFooter でフッタ非表示」の組み合わせで既読化経路が完全に失われる UX 詰みを「現状の表示制御を変更しない」で見送っていた。**自動 OFF 時はフッタ非表示の対象から「既読」ボタンだけ除外する**仕様に変更。`ReaderViewModel` に `IsManualReadButtonVisible` 算出プロパティを追加し、`ToggleHeaderFooter` で `IsFooterVisible=false` でも自動 OFF のときはフッタ Grid 自体を表示し続ける（または既読ボタン単独の Visibility を独立制御）。検証項目にもケースを追加。
  3. **C-3 (Result.InvokeRetry の事前確認)**: AndroidX.Work の C# binding における `Result.InvokeRetry()` メソッドの実在を実装着手前に Grep で確認する手順を追加。既存コードは `Result.InvokeFailure()` のみ使用しているため、`InvokeRetry` の binding 名が異なる可能性がある（`Result.Retry()` 等）。
  4. **SeedSettingsAsync 既存ユーザ挙動の事前確認**: プラン v6/A-3 は「`last_scheduled_hours` シードを `"6"` にすれば既存ユーザの初回起動も no-op」と主張したが、`SeedSettingsAsync` が**既存 DB に対しても新キーを挿入する**前提が明示されていなかった。実装着手前に `SeedSettingsAsync` の foreach ロジック（`INSERT OR IGNORE` 相当か否か）を確認する手順を C-1 セクションに追加。
  5. **PR-7 N-1 (要件書同期マージブロック)**: PR-7 (L-3 + N-1) で `searchTarget="Title"`/`"Author"` の単独検索が消える仕様変更を入れるが、要件書 §3.2 F-001 への反映は PR-5 (L-4) 任せ。両 PR の同期が遅れると仕様食い違いが発生するため、**PR-7 のマージ条件として「PR-5 で F-001 修正がコミット済み」または「PR-7 内で要件書 §3.2 F-001 を同 PR 修正対象に含める」**のいずれかを明示。
  6. **PR-3 M-2 (旧 Enqueue 完全削除の強調)**: 旧 v6 では「旧 `public void Enqueue(...)` は削除」をセクション末尾に書いていたため、レビュー漏れの可能性があった。修正 1 サンプルコード直下に **太字 + 注記**で「旧 `Enqueue` メソッドは**完全削除**（非推奨マークではなく削除）」と再掲。
  7. **PR-3 M-3 と PR-6 L-9 のコンテンツクリア二重実装防止**: 旧 v6 では PR-3 M-3 と PR-6 L-9 の両方に `EpisodeContent / EpisodeTitle / EpisodeHtml = string.Empty` のコードブロックがあり、実装時に両 PR で同じ行を二重に書いてしまうリスクがあった。L-9 セクションの該当ブロックに「**M-3 で既に投入済みの 3 行はそのまま残し、新規追加は不要**。L-9 は DisplayAlert → SetError 置換のみ実施」を明記。
  8. **PR-2 L-2 (UI 範囲外保存値クランプの周知)**: 旧 v6 の注意点は「100ms 等が 500ms に引き上げられる」のみ言及していたが、**5000ms 等が 2000ms に引き下げられる**ケースも同時に発生する。後者はサイト負荷増加とは逆方向の挙動変化のため、ユーザが「設定したはずの値より速くリクエストされる」と気付く可能性。注意点に上振れケースも追記。
  9. **PR-7 N-2 SetReadStateUpToAsync (positional placeholder の事前確認)**: 5 個の `?` placeholder を持つ SQL を `_db.ExecuteAsync(sql, params...)` で実行する設計だが、SQLite-net (sqlite-net-pcl) で 4 個以上の positional placeholder を使う先例が `EpisodeRepository` 内に存在することを実装着手前に Grep で確認する手順を追加。
- **v6 (2026-04-30 / プラン妥当性レビュー第 2 回フィードバックを反映)**:
  1. **A-1 (PR-7 N-2 + B-4 統合)**: 旧 v5 ではコミット 2 (N-2) → コミット 3 (B-4) の 2 段階で `MarkAsReadAsync` を書き換える流れだったが、コミット 2 で書いた `MarkAsReadAsync` をコミット 3 で再リファクタする差分混在が起きていた。**N-2 と B-4 を 1 コミットに統合**し、`ApplyMarkAsReadAsync` (private ヘルパー) + `MarkAsReadAsync` (手動) + `MarkAsReadFromAutoAsync` (自動・ガード付き) の 3 メソッド構造を最終形として一括投入する。N-2 セクションの「修正 2 — `MarkAsReadAsync`」コードブロックは廃止（B-4 修正 c に集約）。コミット構成は 5 → 4 に減少。
  2. **A-2 (BackgroundJobQueue race window)**: M-2 の `EnqueueAsync` で `HashSet.Add` と `Queue.Enqueue` を**同一 lock 内で完結**させる。旧実装は Add 後に lock を抜けてから Enqueue していたため、`StopWorker → SyncEnqueuedIdsFromQueues` が割り込むと「HashSet にも Queue にもない job」が発生し重複 prefetch を起こす race があった。`SyncEnqueuedIdsFromQueues` も同 lock 内で Queue 列挙を行うよう変更。
  3. **A-3 (last_scheduled_hours マイグレーション副作用)**: シード値を `"0"` → `"6"` (= `DEFAULT_UPDATE_INTERVAL_HOURS`) に同期。旧 v5 では既存ユーザの初回起動時に「DB の `update_interval_hours` (=6) ≠ `last_scheduled_hours` (=0)」となり**毎起動 1 回限りの周期リセット副作用**が必ず発生していた。同期させることで既定設定のユーザは no-op となり、設定変更ユーザのみ再登録される。`MainActivity` の `GetIntValueAsync` 呼び出しの defaultValue も `0` → `DEFAULT_UPDATE_INTERVAL_HOURS` に変更。
  4. **A-4 (PR-4 と PR-7 の AUTO_MARK_READ_ENABLED 依存)**: 旧 v5 の PR-4 L-1 サンプルコードに `[SettingsKeys.AUTO_MARK_READ_ENABLED] = ...` 行が含まれていたが、本キーは PR-7 (B-4) で定義される定数のため、推奨マージ順 `PR-4 → PR-7` では PR-4 着手時にビルドエラーになる。**PR-4 L-1 の defaults 辞書から AUTO_MARK_READ_ENABLED 行を除去**し、PR-7 修正 b で本辞書に 1 行追加する形に整理。
  5. **B-2 (async void OnAppearing 例外ガード)**: M-5 で `EpisodeListPage.OnAppearing` を `async void` 化する際、その例外は `TaskScheduler.UnobservedTaskException` (L-5) では拾えない（async void の例外は SynchronizationContext 直接ポスト）。`InitializeAsync` 自体は try/catch 完備で実害は無いが、念のため OnAppearing 内に try/catch を入れてプロセスクラッシュ耐性を強化する旨を「想定外で詰まった場合」に追記。
- **v5 (2026-04-30 / 妥当性検証フィードバックを反映)**:
  1. C-1 修正コードの `SetIntValueAsync` を実在する `SetValueAsync(key, hours.ToString())` に置換（A-1）。
  2. PR-1 のスコープに `SeedSettingsAsync` への `LAST_SCHEDULED_HOURS = "0"` 追加を含めるよう変更。PR-4 L-1 とは独立に PR-1 単体で完結させる（A-2）。
  3. H-2 の通知文言記述を「N話更新→N+M話更新のように増えるだけ」から「差分のみが表示される（重複通知にはならない）」に訂正（B-1）。実コード [UpdateCheckWorker.cs:50](../Platforms/Android/UpdateCheckWorker.cs#L50) と [UpdateCheckService.cs:80](../Services/UpdateCheckService.cs#L80) の `newEpisodes.Count` 引き渡しに整合。
  4. PR-6 概要に「PR-1〜PR-5/PR-7 全マージ後の rebase が前提」を明記（B-2）。
  5. H-1 に `EpisodeCacheRepository.DeleteByNovelIdSync` のシグネチャ事前確認手順を追加（B-3）。
  6. PR-7 N-2 に設定キー `AUTO_MARK_READ_ENABLED` の追加と、それに伴う UI（SettingsPage トグル + ReaderPage フッター手動「既読」ボタン）と自動経路ガード（`MarkAsReadFromAutoCommand` 分離）を**一式で実装**（B-4）。MarkAsReadCommand の発火経路を grep した結果、現コードでは [ReaderPage.xaml.cs:32](../Views/ReaderPage.xaml.cs#L32) と [ReaderPage.xaml.cs:46](../Views/ReaderPage.xaml.cs#L46) の自動 2 経路のみで手動 UI が無いため、設定 OFF 時のフォールバック手動ボタンを同時投入する必要があると判断。
- **v4 (2026-04-30 / 妥当性レビューを反映)**:
  1. C-1: Update ポリシーの「毎起動でリセット」副作用を回避するため、`SettingsKeys.LAST_SCHEDULED_HOURS` で差分判定するロジックを追加。
  2. H-1: `novelInserted = false` の位置が rollback スコープ全体（Insert〜全 await 完了）をカバーすることをコメントで明示。
  3. H-1: `idx_novels_site_novel` UNIQUE 制約の所在を実装着手前に grep で確認する手順を追加。
  4. N-2: 発火条件は手動タップ・自動・スクロール終端いずれも許可（ユーザ確認済み）。誤タップ時の `read_at` 復元不可を要件書 §6.3 に反映する項目（PR-5 / L-4 (f)）として追加。
  5. PR-7: L-3 と N-1 を **1 コミットに統合**（旧 v3 では 2 コミット分離）。個別 cherry-pick / revert 事故を構造的に防止。
  6. PR-2 / PR-3 の `EpisodeListViewModel.cs` 衝突に対する rebase 必要性を明記。
  7. **L-2 を PR-4 から PR-2 に格上げ移動**。事実バグ（Clamp 範囲ズレ）のためユーザ実害最小化を優先。
  8. なろう小説 API 公式仕様（https://dev.syosetu.com/man/api/ 2026-04-30 確認）の裏取り根拠を N-1 / N-3 に追記。

## Context

2026-04-30 に実施した `_Apps` フォルダ全 50 ファイルの徹底レビューで抽出した指摘 23 件 + ユーザ報告 4 件を整理する。
内訳: Critical 3 / High 4 / Medium 5 / Low 11 / ユーザ報告 4 (N-1〜N-4)。
現在ブランチ `app-novelviewer`（master ベース）。

レビュー観点:
- 起動・初期化（DI / Resources / Worker）
- データ整合性（DB トランザクション境界 / 設定値反映）
- バックグラウンド動作（Wi-Fi gating / キャンセル時のハンドル）
- UI / UX（フィルタ表示・エラー表示・無効化）
- 設計上の DRY 違反 / dead code / 過剰権限
- **ユーザ報告（N-1〜N-4）: 検索精度 / 既読の連動 / なろうジャンル / カクヨムランキング・ジャンル**

---

## 実装着手前の事前確認チェックリスト（v8 で追加）

各 PR 着手前に以下を grep / コード確認で検証する。1 つでも結果が想定と異なる場合は、該当 PR のスコープ・サンプルコードを実コードに合わせて調整してから着手すること。

| # | 対象 PR | 確認項目 | 想定結果 / 不一致時の対応 |
|---|---|---|---|
| P-1 | PR-1 (C-1) | `SeedSettingsAsync` が **既存 DB に新キーを追加するが既存キーは上書きしない**（INSERT OR IGNORE 相当）か | `grep -n -A 20 "SeedSettingsAsync" _Apps/Services/Database/DatabaseService.cs` → `FindAsync<AppSetting>(key) ... if (existing is null) await InsertAsync(...)` パターンであること。INSERT OR REPLACE 系なら C-1 のシード値設計（"6"）が成り立たないため、別タスクで「既存キー保持型」に修正を先行 |
| P-2 | PR-1 (C-3) | AndroidX.Work C# binding に `Result.InvokeRetry()` が実在するか | `grep -n "Result\.\(Invoke\)\?Retry" _Apps/` で先例確認。既存 binding は `Result.InvokeFailure()` `Result.InvokeSuccess()` を使用しているため、同じ流儀で `InvokeRetry()` の存在を期待。不一致時は (1) `Result.InvokeRetry()` → (2) `AndroidX.Work.ListenableWorker.Result.InvokeRetry()` → (3) `Result.Retry()` → (4) `AndroidX.Work.ListenableWorker.Result.Retry()` の順で試し、`CS0117` エラーで切り分け |
| P-3 | PR-2 (H-1) | `idx_novels_site_novel` UNIQUE INDEX が既存 DDL に存在するか | `grep -n "idx_novels_site_novel\|UNIQUE.*site_type\|site_type.*novel_id" _Apps/Services/Database/DatabaseService.cs` → 該当 DDL 文がヒットすること。**実コード [DatabaseService.cs:51-53](../Services/Database/DatabaseService.cs#L51-L53) で確認済み（v8 時点）**。万一将来削除されていたら、補償削除に頼る前に UNIQUE インデックス追加を先行 |
| P-4 | PR-2 (H-1) | `EpisodeCacheRepository.DeleteByNovelIdSync` が `(SQLiteConnection conn, int novelId)` の同期 API か / アクセス修飾子 | `grep -n "DeleteByNovelIdSync" _Apps/Services/Database/EpisodeCacheRepository.cs` → 該当シグネチャの存在。**v9 確認済み: 実コードは `internal void DeleteByNovelIdSync(SQLiteConnection conn, int novelId)`**（[EpisodeCacheRepository.cs:41](../Services/Database/EpisodeCacheRepository.cs#L41)）。アクセス修飾子は `internal` だが、`NovelRepository` は同一アセンブリ `LanobeReader` 内のため `_cacheRepo.DeleteByNovelIdSync(conn, n.Id)` 呼び出しは可能。シグネチャが異なる場合（async しか無い等）は `RunInTransactionAsync` の lambda を調整するか、cache の DELETE を生 SQL で直書き |
| P-5 | PR-7 (N-2) | SQLite-net で **5 個以上の positional placeholder (`?`) を使う先例**が repository 群にあるか | `grep -nE "ExecuteAsync\(.*\?.*\?.*\?.*\?" _Apps/Services/Database/*.cs` で集計。**実コード調査で先例 0 件確定（v8 時点）**。よって本プラン N-2 セクションは **2 文分割案を本実装**として採用済み。1 文案は補足扱い。 |
| P-6 | PR-7 (N-2) | `EpisodeRepository` で `read_at` に空文字列を書き込む経路があるか | `grep -n "read_at" _Apps/Services/Database/EpisodeRepository.cs` → MarkAsReadAsync (line 125-132) のみで NULL or ISO-8601 文字列のみ書き込み（実コード確認済み）。空文字列経路があれば SQL の比較で `read_at IS NULL OR read_at = ''` を併用する必要あり |
| P-7 | PR-3 (M-3) / PR-6 (L-9) | `ReaderViewModel.IsFooterVisible` が `[ObservableProperty]` で生成された変更通知付きプロパティか | [ReaderViewModel.cs:51-52](../ViewModels/ReaderViewModel.cs#L51-L52) で `[ObservableProperty] private bool _isFooterVisible = true;` を確認済み（v8 時点）。手書きプロパティの場合は B-4 修正 c2 の `OnIsFooterVisibleChanged` partial 連動を `ToggleHeaderFooter` 内の手動通知に切替 |
| P-8 | PR-7 (B-4) | `ToggleHeaderFooter` コマンドが XAML/コードビハインドから binding されているか（= `IsFooterVisible=false` 経路の到達可能性） | `grep -n "ToggleHeaderFooter\(Command\)\?" _Apps/` の結果が **ReaderViewModel.cs:253 の定義のみ**（XAML/コードビハインドから 0 件）であることを v9 で確認済み。**現状は dead code**であり `IsFooterVisible=false` 経路は到達不能。B-4 修正 e の `IsManualReadButtonOverlayVisible` Overlay は将来 binding が導入された際の救済 UI として先回りで投入する。**もし将来 ToggleHeaderFooter binding が削除/置換された場合は、本 Overlay も同時に削除**する（dead code を増やさない方針）。 |

**v8 注記**: P-3 / P-5 / P-6 / P-7 は v8 改訂時点で実コード確認済み。残る P-1 / P-2 / P-4 は実装 PR ブランチ作成時に最終確認を行う。
**v9 注記**: P-8 は v9 改訂時点で実コード確認済み（dead code 状態）。実装着手時点で binding が追加されていないか改めて grep で確認すること（追加されていれば B-4 修正 e の Overlay は「実到達可能シナリオへの対応」となり立場が強化される）。

---

## PR 分割

| PR | ブランチ名 | 含む項目 | 想定差分 |
|---|---|---|---|
| PR-1 | `feature/fix-c1-c3-startup` | C-1, C-2, C-3 | 4 ファイル / ~80 行 |
| PR-2 | `feature/fix-h1-h4-bugs` | H-1, H-2, H-3, H-4, **L-2（事実バグなので格上げ同梱）** | 5 ファイル / ~140 行 |
| PR-4 | `feature/fix-l1-l10-quality` | L-1, L-5, L-6, L-7, L-8, L-10（**L-2 は PR-2 へ格上げ移動** / **L-3 は PR-7 へ移動** / L-4・L-9・L-11 は別 PR or 取り下げ）| 5 ファイル / ~70 行 |
| PR-3 | `feature/fix-m1-m5-ux` | M-1, M-2, M-3, M-4, M-5 | 7 ファイル / ~110 行（M-2 で BackgroundJobQueue.cs / PrefetchService.cs / UpdateCheckService.cs を変更）|
| PR-7 | `feature/fix-l3-n1-n4-search-bugs` | **L-3 + N-1, N-2, N-3, N-4 + B-4**（L-3 と N-1 は **1 コミットに統合**。N-2 と B-4 も **1 コミットに統合**。B-4 = 自動既読化トグル + 手動既読ボタン UI）| 9 ファイル / ~210 行 / 4 コミット |
| PR-6 | `feature/refactor-error-ui-unification` | L-9（エラー UI 完全統一）| 12 ファイル / ~150 行 |
| PR-5 | `feature/docs-requirements-catchup` | L-4（要件書キャッチアップ + plan ファイル削除）| 要件書 ~300 行追記 / 16 ファイル削除 |

**重要事実バグ優先表（マージ後即座にユーザ実害が消える順）:**

| 項目 | 影響 | 含まれる PR |
|---|---|---|
| **L-2** | リクエストディレイ Clamp が UI 範囲(500-2000)と乖離(100-5000)。設定値外の値が通る | PR-2（事実バグ格上げ） |
| **N-3** | なろうジャンルブラウズが「すべて」で 0 件、他ジャンルも `genre=` vs `biggenre=` 混同で動かない | PR-7 |
| **N-4** | カクヨムランキング/ジャンルブラウズが広告混入・順序崩れ | PR-7 |
| **N-1** | なろう検索結果に無関係作品が大量混入 | PR-7（同 PR 内 L-3 修正後）|
| **N-2** | 既読化が読了点までの一括にならない | PR-7 |
| **C-1** | 設定 UI で update_interval_hours を変更しても永久 6h 固定 | PR-1 |
| **H-1** | Register 失敗時に Novel が DB に孤立、再登録不能 | PR-2 |

**推奨順序: PR-1 → PR-2 → PR-4 → PR-7 → PR-3 → PR-6 → PR-5**

**順序の根拠:**
- **L-3 を PR-7 に統合する理由（重要）**: L-3（`searchTarget` 削除）と N-1（URL に `&title=1&wname=1` 追加）はどちらも `NarouApiService.SearchAsync` を触る。仮に PR を分けると、片方を revert したときにもう一方がビルド破綻 (CS0535) または dead parameter 残留を引き起こす。**両者を同一 PR にまとめれば revert/再適用が必ず一緒に行われる**ため事故が構造的に防止できる。
  - **コミットも 1 つに統合する**（旧プラン v3 では 2 コミット分離だったが、レビュアーが個別 cherry-pick / revert する誤操作で部分適用される脆さを残す。1 コミット統合なら revert は確実に両方一緒）。
  - コミット 1: `fix: narrow Narou search scope (remove dead searchTarget, add title/wname flags)` — `INovelService.cs` / `NarouApiService.cs` / `KakuyomuApiService.cs` / `SearchViewModel.cs` から `searchTarget` を削除し、同時に `NarouApiService.SearchAsync` の URL に `&title=1&wname=1` を追加。L-3 と N-1 を 1 ファイル変更単位で見ると同じ箇所への複合修正のため、コミット分離のメリットは小さい。
  - PR description に L-3 と N-1 のそれぞれの背景を見出し付きで記述してレビュアビリティを担保する（コミット分離の代替）。
- **PR-4 を PR-7 の前にする理由**: PR-4 は dead code 削除と定数差し替えが中心で安全度が高い。L-2（Clamp 範囲ズレ）は事実バグだが本プラン v4 で **PR-2 に格上げ済み**のため、PR-4 はクリーンアップに専念する内容になる。PR-4 と PR-7 はファイル衝突がない（PR-7 から L-3 が PR-4 へ移っていないため）。
- **N-1〜N-4 を PR-3 (M-1〜M-5) より前に置く理由**: N-3, N-4 はジャンル/ランキング機能が実質壊れている High 相当のバグ。M-1〜M-5 は UX 改善（M-1 silently fallback / M-2 メモリ蓄積 / M-3 表示残留 等）で実害は相対的に小さい。**ユーザ実害の解消を優先**。
- **PR-3 (M-2) のスコープが拡大した点**: 当初 M-2 は `Enqueue` の同期 drop だけだったが、本プラン v3 では race window を完全抑止するため `EnqueueAsync` への async 化に変更。`BackgroundJobQueue.cs` / `PrefetchService.cs` / `UpdateCheckService.cs` の 3 ファイルに変更が及ぶため PR-3 のサイズが ~80 → ~110 行に増える。それでも単一 PR で完結する小さなリファクタリング。
- **PR-3 (M-3) と PR-6 (L-9) の依存**: M-3 が `ReaderViewModel.LoadEpisodeAsync` のオフライン分岐に「コンテンツクリア + DisplayAlert」を入れ、L-9 でその DisplayAlert を `SetError` バナーに置換する直列構造。詳細は M-3 / L-9 セクション内の「PR-6 (L-9) との関係」節参照。
- **PR-5 を最後にする理由**: PR-1〜PR-4, PR-6, PR-7 でコード変更（特に ReaderViewModel のエラー UI、F-006 既読仕様、F-001 検索仕様）が確定してから要件書に転記する方が、書き直しが減る。

- **「PR-3 を PR-2 直後に繰り上げる案」を採らない根拠（v9 で追加）**: PR-2 マージ時点で導入される H-4 由来の race window（`SyncEnqueuedIdsFromQueues` が旧 `Enqueue` の lock 構造のまま動作する期間）は、PR-3 (M-2) で `EnqueueAsync` の lock 統合が入るまで残存する。マージ順を `PR-1 → PR-2 → PR-3 → PR-4 → PR-7 → PR-6 → PR-5` に変更すれば本 race を **PR-2 マージ直後に解消**できる。しかしこの順序は **N-1〜N-4（ユーザが日々遭遇する事実バグ：検索精度・なろうジャンル・カクヨムランキング）の到達を 1 PR 分遅らせる**トレードオフを伴う。
  - **race window の実害**: Wi-Fi 切断中に新規 `Enqueue` が走った場合、HashSet にも Queue にもない job が発生する可能性 → 後続再 Enqueue 時に重複 prefetch 1 回発生。UX への影響は無視できる範囲（同 episode の二重キャッシュ書き込みは `EpisodeCacheRepository.InsertAsync` 側で `episode_id` UNIQUE 制約により後勝ちの上書き、または insert 失敗で握り潰し）。
  - **N 系バグの実害**: N-3「なろうジャンルが完全に動かない」、N-4「カクヨムランキングに広告/順序崩れが混入」、N-1「検索結果に無関係作品が混入」、N-2「既読化が読了点までの一括にならない」のいずれもユーザが画面を開くたびに遭遇する事実バグ。
  - **比較結論**: race window はバックグラウンド + Wi-Fi 切断中の限定条件下で重複 prefetch 1 回 vs N 系はフォアグラウンドで毎回発火。**後者の早期解消を優先**する判断は妥当で、現状順序を据え置く。
  - **代替の構造的軽減**: PR-2 description の「⚠ 必須記載事項」（v8 で追加済み）でレビュアーへ race の存在を周知し、PR-3 マージ時点で完全解消することを明示。これにより「気付かないまま運用される race」ではなく「期間限定で許容される race」として管理する。

- **PR-2 と PR-3 の同一ファイル衝突（rebase 必要性）**: PR-2 の H-3 が `EpisodeListViewModel.RefreshReadStatusAsync` を改修し、PR-3 の M-5 が同じ ViewModel に `_initTask` フィールドと `EnsureInitializedAsync` を追加する。推奨順序では PR-2 → PR-4 → PR-7 → PR-3 と間に 2 PR が挟まるため、PR-3 着手時には**確実に rebase が必要**。PR-3 のブランチ `feature/fix-m1-m5-ux` を作成する直前に `git fetch && git rebase origin/app-novelviewer` を実行し、H-3 の最新コードに対して M-5 の変更を載せ直すこと。`async Task RefreshReadStatusAsync` が呼ばれる経路（`OnAppearing` 内）と `EnsureInitializedAsync` の追加箇所は別関数なのでロジック衝突は無く、機械的な rebase で解決できる想定。

### `DatabaseService.SeedSettingsAsync` の 3-way 衝突解決手順（v9 で追加）

PR-1 / PR-4 (L-1) / PR-7 (B-4) の **3 PR が同一の `defaults` 辞書を編集**する。推奨マージ順 `PR-1 → PR-2 → PR-4 → PR-7 → ...` で進めると、PR-4 着手時に PR-1 の差分との rebase、PR-7 着手時に PR-4 の差分との rebase で `defaults` 辞書 ([DatabaseService.cs:103-127](../Services/Database/DatabaseService.cs#L103-L127)) のマージ衝突が発生する。

各 PR が触る行と衝突解決時の **保持必須項目**:

| PR | 触る行 | 追加/変更内容 | 衝突解決時に必須で保持する項目 |
|---|---|---|---|
| PR-1 (C-1) | line 116 直後 | `["last_scheduled_hours"] = "6"` の **1 行追加（リテラル形式）** | この 1 行が消えていないこと。値は **必ず "6"**（C-1 の差分判定が機能するため。"0" だと既存ユーザ全員に毎起動の reschedule が発生する v6 で議論済みの副作用） |
| PR-4 (L-1) | line 105-117 全体 | **既存リテラル 10 行 + PR-1 で追加した 1 行を `SettingsKeys.*` 定数経由に書き換え** | (a) リテラル `"3"` `"6"` 等が一切残っていないこと、(b) `LAST_SCHEDULED_HOURS` 行が `SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString()` 形式で含まれていること、(c) `NOVEL_SORT_KEY` の値が `SettingsKeys.DEFAULT_NOVEL_SORT_KEY`（新規定数）経由 |
| PR-7 (B-4) | L-1 後 defaults 辞書末尾 | `[SettingsKeys.AUTO_MARK_READ_ENABLED] = SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED.ToString()` の 1 行を **`LAST_SCHEDULED_HOURS` 行の直後に追加** | この 1 行が `LAST_SCHEDULED_HOURS` の直後に置かれていること（位置を末尾に揺らさない理由は v6 A-4 で議論済み） |

**衝突解決時の目視確認チェックリスト（PR-4 / PR-7 マージ完了後に必ず実施）:**

- [ ] `defaults` 辞書の **全エントリが `SettingsKeys.*` 定数経由**で書かれている（リテラル文字列が混入していないこと）
- [ ] `defaults.Count` が想定値（PR-1 後=11、PR-4 後=11、PR-7 後=12）と一致している
- [ ] 各エントリの値が `SettingsKeys.DEFAULT_*` 定数の `.ToString()` または `string` 直参照形式で書かれている
- [ ] `LAST_SCHEDULED_HOURS` の値が `SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString()` で `UPDATE_INTERVAL_HOURS` と必ず同期している（"6" と "0" 等で不一致になっていないこと）
- [ ] PR-1 で追加した seed が誤って消されていないこと（rebase コンフリクト解決時の事故防止）

**実装上の補強案（採用は実装者判断）:** PR-1 のスコープに L-1 を統合する選択肢もある（PR-1 サイズが ~80 → ~100 行に増える代わりに PR-7 B-4 段階で defaults 辞書編集が単純な末尾追加だけになり、3-way 衝突が 2-way に縮小）。本プランでは PR 粒度（C 系の 3 件は起動・更新通知という機能軸、L 系は cleanup 軸）を保つため統合しない方針だが、もし PR-4 マージ時に予想以上の衝突解決コストが発生した場合は、PR-7 着手前に「先に PR-7 のスコープから L-1 影響部分を一時的に切り出して PR-4 にマージしてしまう」形での吸収もあり得る。

ベースは全て `app-novelviewer`。CLAUDE.md ルール「ファイルが base にあるなら base からブランチ」に従う。本プランで触るファイル群（`MainActivity.cs`, `UpdateCheckScheduler.cs`, `UpdateCheckWorker.cs`, `MauiProgram.cs`, `App.xaml.cs`, 各 ViewModel / Repository / Service / View）は全て `_Apps` 配下のアプリ固有資産で、`app-novelviewer` 上に存在する（master には `_Apps` 自体が存在しない）。実装前に `git ls-tree app-novelviewer -- <path>` で念のため確認すること。

---

# PR-1: 起動・更新通知の信頼性（C1-C3）

## C-1: `update_interval_hours` 設定が WorkManager に反映されない

**問題:** [MainActivity.cs:23](../Platforms/Android/MainActivity.cs#L23) は `UpdateCheckScheduler.SchedulePeriodicCheck(this)` を呼ぶだけで、設定値を渡していない。
- [UpdateCheckScheduler.cs:9](../Platforms/Android/UpdateCheckScheduler.cs#L9) のシグネチャは `int intervalHours = 6` 既定値持ち。
- [UpdateCheckScheduler.cs:24](../Platforms/Android/UpdateCheckScheduler.cs#L24) は `ExistingPeriodicWorkPolicy.Keep` で再登録時も上書きしない。
- 結果: 設定 UI で「チェック間隔: N時間」を変更しても永久に 6h 固定。`SettingsViewModel.OnUpdateIntervalHoursChanged` で DB 保存はされるが Worker 側で読み取らない。

**修正方針:**
1. `MainActivity.OnCreate` で DB 初期化を保証してから設定値を取得し、Scheduler に渡す。
2. Scheduler は `ExistingPeriodicWorkPolicy.Update` に変更（既存があれば request の最新値で上書き、無ければ新規登録）。
3. **毎 OnCreate で無条件再登録すると次回発火時刻がリセットされ、6h 周期 + 毎日起動するユーザでは Worker が一度も発火しない事故が起きる**。これを防ぐため、`SettingsKeys` に新規定数 `LAST_SCHEDULED_HOURS`（既定 0）を追加し、**「DB の `update_interval_hours` ≠ 前回 schedule 時の保存値」のときだけ `Update` で再登録**する差分判定を入れる。同値なら no-op。これで設定変更が確実に反映され、かつ毎起動でのリセットも防げる。
4. 設定 UI 変更時の即時 reschedule は今回スコープ外（次回起動で反映、最大 1 起動分の遅延）。理由: 上記差分判定により次回起動で確実に反映されるため、即時化のための WorkManager I/O コストを払う必要が薄い。
5. `IPlatformApplication.Current` は `MauiAppCompatActivity.OnCreate(base)` 完了時点で通常は確立済み（`MainApplication.OnCreate` で `MauiProgram.CreateMauiApp` が走った後に Activity が起動するため）。ただし MAUI 9 + Android のスレッドモデル依存で稀に未確立となる事例が C-3 と同根で観測されることを踏まえ、**防衛コードとしてのみ最大 ~3 秒（30 回 × 100ms）のリトライループ**を入れる。null のまま諦めた場合でも、`UpdateCheckScheduler` の既定値 6h で**フォールバック登録**を行い、次回起動以降の反映に賭ける（永久に Worker 未スケジュールを避ける）。

   **3 秒という値の根拠（透明性のための注記）:** 本プラン作成時点で実機計測の観測実績はなく、3 秒は「100ms × 30 回」として直感的に選んだ防衛上限値。通常はループ初回（最初の 100ms）で抜けることを想定しており、3 秒に到達するのは異常系のみ。将来 LogHelper.Warn 経路でリトライ回数が頻繁にログ出力されるようなら、(a) 計測値に基づき上限を上下する、(b) リトライ回数をメトリクスに残して経時変化を観察する、のいずれかで根拠を強化する。本 PR では計測機構までは入れない。

**修正 1 — `MainActivity.cs:15-27` の OnCreate:**

```csharp
protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);

    NotificationHelper.CreateNotificationChannels(this);

    // ★ Activity ではなく ApplicationContext を closure にキャプチャ（v7 で変更）。
    //   理由: 後続の Task.Run は最大 ~3 秒のリトライ + DB 初期化を含み、Activity ライフサイクルを
    //   跨ぐ可能性がある。Activity 破棄後に WorkManager 呼び出しが走るとリーク/例外を起こすため、
    //   寿命がプロセス全体である ApplicationContext を使う。WorkManager.GetInstance も
    //   Application Context を要求するため意味的にも妥当。
    //
    //   v9 修正: `Activity.ApplicationContext` の戻り値型は C# binding 上 `Context?`（nullable）。
    //   後続の `SchedulePeriodicCheck(Context context, ...)` は non-nullable を要求するため、
    //   `<Nullable>enable</Nullable>` 環境では CS8604 警告を回避する必要がある。OnCreate 段階で
    //   ApplicationContext が null なケースは Android プロセス異常に該当するため、null なら
    //   即座に throw して fail-fast する（後続の防御的フォールバックでも null は救えない）。
    var ctx = ApplicationContext
        ?? throw new InvalidOperationException("ApplicationContext is null in MainActivity.OnCreate");

    // 設定値で WorkManager の周期を決定（fire-and-forget で OK; 次回起動で反映）
    _ = Task.Run(async () =>
    {
        try
        {
            // DI 確立を最大 ~3秒待つ（MauiAppCompatActivity 初期化と本コード実行のレース対策）
            IServiceProvider? services = null;
            for (int i = 0; i < 30; i++)
            {
                services = IPlatformApplication.Current?.Services;
                if (services is not null) break;
                await Task.Delay(100).ConfigureAwait(false);
            }

            if (services is null)
            {
                // DI 未確立: 既定値でフォールバック登録（永久未スケジュール回避）
                LogHelper.Warn(nameof(MainActivity),
                    "DI not ready in OnCreate; scheduling with default interval");
                UpdateCheckScheduler.SchedulePeriodicCheck(ctx);
                return;
            }

            var dbService = services.GetService<DatabaseService>();
            var settingsRepo = services.GetService<AppSettingsRepository>();
            if (dbService is null || settingsRepo is null)
            {
                UpdateCheckScheduler.SchedulePeriodicCheck(ctx); // 同上
                return;
            }

            await dbService.EnsureInitializedAsync().ConfigureAwait(false);
            var hours = await settingsRepo.GetIntValueAsync(
                SettingsKeys.UPDATE_INTERVAL_HOURS,
                SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS).ConfigureAwait(false);

            // 差分判定: DB 値 != 前回 schedule 値 のときのみ再登録（毎起動でのリセット防止）。
            // SeedSettingsAsync で last_scheduled_hours は update_interval_hours と同じ既定値 (=6)
            // でシードされるため、既定設定のままのユーザは初回起動でも no-op となる。
            // GetIntValueAsync の defaultValue は念のため DEFAULT_UPDATE_INTERVAL_HOURS を渡し、
            // 万一シードが走らなかった場合でも既定動作と整合させる。
            var lastScheduled = await settingsRepo.GetIntValueAsync(
                SettingsKeys.LAST_SCHEDULED_HOURS,
                SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS).ConfigureAwait(false);
            if (hours != lastScheduled)
            {
                UpdateCheckScheduler.SchedulePeriodicCheck(ctx, hours);
                // AppSettingsRepository には SetIntValueAsync が無いため SetValueAsync で文字列化保存
                await settingsRepo.SetValueAsync(
                    SettingsKeys.LAST_SCHEDULED_HOURS, hours.ToString()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Warn(nameof(MainActivity), $"Schedule worker failed: {ex.Message}");
            // 例外時もフォールバック（last_scheduled は更新しない: 次回も再試行）
            try { UpdateCheckScheduler.SchedulePeriodicCheck(ctx); } catch { /* 諦める */ }
        }
    });

    HandleIntent(Intent);
}
```

`using LanobeReader.Helpers;` `using LanobeReader.Services.Database;` を追加。

**`UpdateCheckScheduler.SchedulePeriodicCheck` のシグネチャ確認:** 既存シグネチャは `public static void SchedulePeriodicCheck(Context context, int intervalHours = 6)` で `Context` 型を受け取る。`ApplicationContext` プロパティは `Activity` の上位型から派生し、戻り値型は `Context` 互換のため、`this`（`MauiAppCompatActivity` extends `Context`）を `ApplicationContext`（`Context`）に置き換えてもシグネチャは変わらず、ビルドエラーは出ない。

**`SettingsKeys.cs` に定数追加:**

```csharp
public const string LAST_SCHEDULED_HOURS = "last_scheduled_hours";
```

**`AppSettingsRepository` の API 確認結果:** [AppSettingsRepository.cs:49-64](../Services/Database/AppSettingsRepository.cs#L49-L64) には `SetValueAsync(string, string)` のみで `SetIntValueAsync` は存在しない。本プランのサンプルコード上段は既に `SetValueAsync(..., hours.ToString())` で書いてある。`AppSettingsRepository` に新規メソッドを足さずに既存 API で完結させる方針。

**事前確認 (v7 で追加・実装着手前に必須):** `SeedSettingsAsync` が**既存 DB に対しても新キーを挿入する**前提を検証する。プラン v6/A-3 のシード値 `"6"` ロジックは「既存ユーザの初回起動 no-op」を保証するためにこの前提が成り立つ必要がある。

```bash
# defaults 辞書が処理される foreach の中身を確認:
grep -n -A 20 "SeedSettingsAsync" _Apps/Services/Database/DatabaseService.cs
```

期待される実装パターン: `foreach (var kv in defaults) { var existing = await _connection.FindAsync<AppSetting>(kv.Key); if (existing is null) await _connection.InsertAsync(new AppSetting { Key = kv.Key, Value = kv.Value }); }` 相当（INSERT OR IGNORE 相当の "missing key だけ挿入" 動作）。**もし `INSERT OR REPLACE` 相当の上書き動作になっている場合**は本プランの前提が崩れるため、defaults 辞書側で `last_scheduled_hours` を入れる前に PR-1 の seed ロジックを「既存キー保持型」に修正する別タスクが先行で必要になる。

**`SeedSettingsAsync` への seed 追加（A-2 対応・PR-1 のスコープに含める）:** [DatabaseService.cs:103-127](../Services/Database/DatabaseService.cs#L103-L127) の `defaults` 辞書に **PR-1 内で** `["last_scheduled_hours"] = "6"` を 1 行追加する。理由は PR-1 単体でセルフコンテインさせるため。defaults 辞書全面リライト（リテラル → `SettingsKeys.*` 定数）は引き続き L-1 (PR-4) のスコープだが、本キーの seed 行追加だけは PR-1 で完結させる。

```csharp
// DatabaseService.cs:116 行目（"novel_sort_key" の直後）に 1 行追加
["last_scheduled_hours"] = "6",  // 値は update_interval_hours の既定値と同期
```

**シード値を `"6"` にする理由（v6 で変更）:** 旧プランは `"0"` を入れていたが、その場合既存ユーザの初回起動時に「DB の `update_interval_hours`（既定 6）≠ `last_scheduled_hours`（=0）」となって**必ず `Update` で再登録が発生し、既存の WorkManager 周期がリセットされる**副作用があった。`update_interval_hours` の既定値 (`SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS = 6`) と同期させることで、既定設定のままのユーザは初回起動でも差分判定 no-op となり、再登録は発生しない。

なお、ユーザが既に `update_interval_hours` を 12 等に変更している場合は、初回起動時に `12 != 6` で 1 度だけ再登録が走る → これは「設定変更が反映される」という本修正の目的そのものなので意図通りの挙動。

PR-4 L-1 の全面リライト時にも `[SettingsKeys.LAST_SCHEDULED_HOURS] = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString()` の形で含める（L-1 セクションに既に明記済み）。

**修正 2 — `UpdateCheckScheduler.cs:23`:**

```csharp
WorkManager.GetInstance(context)!.EnqueueUniquePeriodicWork(
    UpdateCheckWorker.WORK_TAG,
    ExistingPeriodicWorkPolicy.Update!,   // Keep → Update に変更
    workRequest);
```

**注意点:** `Update` ポリシーは「既存の有無に関わらず request の値で上書き or 新規登録」。本プランでは `LAST_SCHEDULED_HOURS` による差分判定を `MainActivity` 側に置き、`Update` ポリシーが実際に呼ばれるのは「初回 or 設定値変更時」だけになる。よって毎起動でのリセットは発生しない。WorkManager 2.7+ の仕様。

---

## C-2: `Resources/Fonts` `Resources/Splash` `Resources/Raw` が空

**問題:**
- [LanobeReader.csproj:23](../LanobeReader.csproj#L23) は `<MauiFont Include="Resources\Fonts\*" />` だが実体ゼロ。
- [MauiProgram.cs:22-23](../MauiProgram.cs#L22-L23) は `OpenSans-Regular.ttf` / `OpenSans-Semibold.ttf` を `AddFont` 登録するが、ファイル自体がリポジトリに存在しない。
- これらのフォント名は XAML から参照されていない (grep で `OpenSans` 不使用を確認)。
- 起動時に FontRegistrar が "Cannot find font asset" を WARN で吐き続ける。即クラッシュはしないが Splash 画像なしで Maui.SplashTheme が真っ黒になり得る。

**修正方針:** XAML で実使用していないので **`AddFont` 呼び出しを削除**するのが最小コスト。OpenSans を実際に採用したい場合は別 PR で MAUI テンプレートからファイルを取得して配置する。

**修正 — `MauiProgram.cs:18-24`（AddFont 呼び出しは 22-23 行）:**

```csharp
builder
    .UseMauiApp<App>();
    // 旧: .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", ...); fonts.AddFont("OpenSans-Semibold.ttf", ...); });
    // OpenSans*.ttf は Resources/Fonts に存在せず、XAML からも参照していないため削除。
    // 縦書き WebView は Reader 側 CSS で font-family:serif を直指定しているため影響なし。
```

Splash / Raw については現状 `MauiAsset` / `MauiSplashScreen` 設定が無いので、空でもビルド・起動は通る（`SplashTheme` は OS デフォルトに fallback）。今回は触らない。Splash を実装するなら別 PR で MAUI テンプレートの `splash.svg` を `Resources/Splash/` に配置。

---

## C-3: `UpdateCheckWorker` の DI 解決を防衛的にする

**問題:** [UpdateCheckWorker.cs:23-32](../Platforms/Android/UpdateCheckWorker.cs#L23-L32) は `IPlatformApplication.Current?.Services` を参照する。
- 通常 WorkManager は `MainApplication.OnCreate` → `MauiProgram.CreateMauiApp` 経由でプロセスが立ち上がってから DoWork を呼ぶため `IPlatformApplication.Current` は非 null になる **はず**。
- しかし MAUI 9 + AndroidX.Work 2.10 では Worker のスレッドモデル次第で初期化順が前後し、`IPlatformApplication.Current is null` で `Result.InvokeFailure()` を返す事象が観測されることがある。
- レビュー時点でクラッシュは確認していないが、起動経路の脆さは残る。

**修正方針:** null だった場合に WARN ログを出して Retry を返す（Failure ではなく）。Retry は WorkManager の指数バックオフで再試行され、次回はプロセスが暖まっている可能性が高い。

**事前確認 (v7 で追加・実装着手前に必須):** `Result.InvokeRetry()` が AndroidX.Work の C# binding に実在するメソッド名かを確認する。既存コードは `Result.InvokeFailure()` のみ使用しているため、`InvokeRetry` の binding 名が異なる可能性（`Result.Retry()`、`Result.Retry` プロパティ等）がある。

```bash
# binding の DLL/型定義から候補メソッド名を確認:
grep -rn "Result\.\(Invoke\)\?Retry" _Apps/ 2>/dev/null
# 既存コードを参照して同じ binding 流儀を踏襲:
grep -n "Result\.Invoke\(Failure\|Success\|Retry\)" _Apps/Platforms/Android/UpdateCheckWorker.cs
```

`Result.InvokeRetry()` が見つからない場合の代替候補（実装時に試す順番）: (1) `Result.InvokeRetry()` → (2) `AndroidX.Work.ListenableWorker.Result.InvokeRetry()` → (3) `Result.Retry()` → (4) `AndroidX.Work.ListenableWorker.Result.Retry()`。`dotnet build` の `CS0117` (メンバー無し) エラーで切り分け可能。

**修正 — `UpdateCheckWorker.cs:23-32`:**

```csharp
var services = IPlatformApplication.Current?.Services;
if (services is null)
{
    // MainApplication 初期化完了前に Worker が起動した可能性。
    // Retry を返して WorkManager のバックオフに任せる。
    LogHelper.Warn(nameof(UpdateCheckWorker), "IPlatformApplication.Current is null, retry later");
    return Result.InvokeRetry();
}

var dbService = services.GetService<DatabaseService>();
var novelRepo = services.GetService<NovelRepository>();
var episodeRepo = services.GetService<EpisodeRepository>();
var updateCheckService = services.GetService<UpdateCheckService>();

if (dbService is null || novelRepo is null || episodeRepo is null || updateCheckService is null)
{
    LogHelper.Error(nameof(UpdateCheckWorker), "Failed to resolve services");
    return Result.InvokeFailure();
}
```

`Result.InvokeRetry()` は AndroidX.Work の `ListenableWorker.Result.Retry()` に対応。最大 5 回までバックオフされ、5 回失敗で諦める（次回の周期 = 6h 後にまた走る）。

---

# PR-2: 機能バグ修正（H1-H4）

## H-1: `SearchViewModel.RegisterAsync` の部分失敗で Novel が孤立

**問題:** [SearchViewModel.cs:268-302](../ViewModels/SearchViewModel.cs#L268-L302) の登録フロー:

```
1. _novelRepo.InsertAsync(novel)      ← DB
2. service.FetchEpisodeListAsync(...) ← ネットワーク (例外候補)
3. _episodeRepo.InsertAllAsync(...)   ← DB
```

手順 2 で `HttpRequestException` / `TaskCanceledException` が出ると Novel だけ DB に残り、Episodes 0件の壊れた状態。再検索しても `IsRegistered=true` で再登録不能、目次も空。

**修正方針:** 失敗時に Novel をロールバック削除する。フローを単一トランザクション化する案もあるが、ネットワーク呼び出しを跨ぐためトランザクションを長時間張るのは avoidable。「失敗時の補償削除」の方が現実的。

**ロールバック識別子の選択:** Insert 直後の `GetBySiteAndNovelIdAsync` 経由で ID を取得する旧設計は、Get-null 時に rollback 識別子が無い corner case を生む。本プランでは **(SiteType, NovelId) のキー** をロールバック識別子として使う。これは UNIQUE 制約で保証されており、Insert 成功時には必ず一意に対応するレコードが存在する。

**事前確認 1（実装着手前に必須）:** `Novel.cs` モデルには UNIQUE 属性の明示がないため、UNIQUE 制約が DDL 側で張られていることを実装着手前に grep で確認する:

```bash
grep -n "idx_novels_site_novel\|UNIQUE.*site_type\|site_type.*novel_id" _Apps/Services/Database/DatabaseService.cs
```

該当する `CREATE UNIQUE INDEX` 文または `CREATE TABLE ... UNIQUE(site_type, novel_id)` 相当の DDL が見つかれば前提成立。**見つからなかった場合は本 H-1 修正を着手する前に UNIQUE インデックスを追加するマイグレーションを別 PR で先行投入する**こと（重複 Insert を許す状態で補償削除に頼ると、既存 Novel まで巻き込んで削除する事故になり得る）。

**事前確認 2（実装着手前に必須）:** 補償削除メソッドのサンプル中で `_cacheRepo.DeleteByNovelIdSync(conn, n.Id)` を再利用する前提のため、`EpisodeCacheRepository.DeleteByNovelIdSync` のシグネチャを実装着手前に確認:

```bash
grep -n "DeleteByNovelIdSync" _Apps/Services/Database/EpisodeCacheRepository.cs
```

**v9 確認済み**: 実コードは `internal void DeleteByNovelIdSync(SQLiteConnection conn, int novelId)`（[EpisodeCacheRepository.cs:41](../Services/Database/EpisodeCacheRepository.cs#L41)）。同期 API かつ `SQLiteConnection` を引数に取る形で、本サンプルのまま再利用可能。アクセス修飾子は `public` ではなく **`internal`** だが、`NovelRepository` は同一アセンブリ `LanobeReader` 内のため呼び出しに制限はない（既存の [NovelRepository.cs:174](../Services/Database/NovelRepository.cs#L174) `DeleteAsync` でも同 internal メソッドを呼んでいる前例あり）。

シグネチャが異なる場合（async しか無い等）は、`RunInTransactionAsync` の lambda を `async` 化するか、cache の DELETE を生 SQL で `conn.Execute("DELETE FROM episode_cache WHERE episode_id IN (SELECT id FROM episodes WHERE novel_id = ?)", n.Id)` 直書きに切り替える。

**修正 1 — `NovelRepository.cs` に補償削除メソッド追加:**

```csharp
/// <summary>
/// (site_type, novel_id) で Novel を補償削除。
/// RegisterAsync の Insert 成功後ネットワーク失敗時に使用。
/// </summary>
public async Task DeleteBySiteAndNovelIdAsync(int siteType, string novelId)
{
    await _dbService.EnsureInitializedAsync().ConfigureAwait(false);
    await _db.RunInTransactionAsync(conn =>
    {
        // 親 → 子の順を保つため id を取得してから既存 DeleteAsync 相当を実行
        var rows = conn.Query<Novel>(
            "SELECT * FROM novels WHERE site_type = ? AND novel_id = ?",
            siteType, novelId);
        foreach (var n in rows)
        {
            _cacheRepo.DeleteByNovelIdSync(conn, n.Id);
            conn.Execute("DELETE FROM episodes WHERE novel_id = ?", n.Id);
            conn.Execute("DELETE FROM novels WHERE id = ?", n.Id);
        }
    }).ConfigureAwait(false);
}
```

**修正 2 — `SearchViewModel.cs:246-316` の `RegisterAsync` 全体:**

`try` 内で「Insert 成功フラグ」を立て、catch で (SiteType, NovelId) キーで補償削除する。

```csharp
[RelayCommand]
private async Task RegisterAsync(SearchResultViewModel result)
{
    if (result.IsRegistered || result.IsRegistering) return;

    result.IsRegistering = true;
    bool novelInserted = false;
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var novel = new Novel
        {
            SiteType = (int)result.SiteType,
            NovelId = result.NovelId,
            Title = result.Title,
            Author = result.Author,
            TotalEpisodes = result.TotalEpisodes,
            IsCompleted = result.IsCompleted,
            RegisteredAt = DateTime.UtcNow.ToString("o"),
            LastUpdatedAt = DateTime.UtcNow.ToString("o"),
        };
        await _novelRepo.InsertAsync(novel);
        novelInserted = true;

        var service = _serviceFactory.GetService(result.SiteType);
        var episodes = await service.FetchEpisodeListAsync(result.NovelId, cts.Token);

        var dbNovel = await _novelRepo.GetBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId)
            ?? throw new InvalidOperationException("登録レコードを取得できませんでした");

        foreach (var ep in episodes) ep.NovelId = dbNovel.Id;
        await _episodeRepo.InsertAllAsync(episodes);

        dbNovel.TotalEpisodes = episodes.Count;
        if (string.IsNullOrEmpty(dbNovel.Author))
        {
            try
            {
                var (_, _, _, fetchedAuthor) = await service.FetchNovelInfoAsync(result.NovelId, cts.Token);
                if (!string.IsNullOrEmpty(fetchedAuthor)) dbNovel.Author = fetchedAuthor;
            }
            catch { /* 作者名取得失敗は無視 */ }
        }
        await _novelRepo.UpdateAsync(dbNovel);

        _ = _prefetch.EnqueueNovelAsync(dbNovel.Id);

        result.IsRegistered = true;
        result.TotalEpisodes = episodes.Count;

        // ★ rollback スコープを「Insert 〜 全 await 完了」までに広げる意図でフラグを最後に下ろす。
        //   位置はメソッド末尾（result.* セット後）で固定。FetchEpisodeList / InsertAll / UpdateAsync /
        //   FetchNovelInfo のいずれの段階で例外が出ても catch で補償削除に到達する。
        novelInserted = false;
    }
    catch (Exception ex)
    {
        LogHelper.Error(nameof(SearchViewModel), $"Register failed: {ex.Message}");
        if (novelInserted)
        {
            try
            {
                await _novelRepo.DeleteBySiteAndNovelIdAsync((int)result.SiteType, result.NovelId);
            }
            catch (Exception rbEx)
            {
                LogHelper.Warn(nameof(SearchViewModel),
                    $"Rollback delete failed for ({result.SiteType}, {result.NovelId}): {rbEx.Message}");
            }
        }
        await Shell.Current.DisplayAlert("エラー", $"登録に失敗しました: {ex.Message}", "OK");
    }
    finally
    {
        result.IsRegistering = false;
    }
}
```

**注意点:**
- `DeleteBySiteAndNovelIdAsync` は `(site_type, novel_id)` キーで検索するため、`GetBySiteAndNovelIdAsync` が null を返す異常系（Insert 成功直後の Get null など）でも確実にロールバックできる。
- 既存の `DeleteAsync(int novelId)` ([NovelRepository.cs:169-178](../Services/Database/NovelRepository.cs#L169-L178)) は使用しない（id を持たない経路でも安全に削除するため）。
- `EpisodeCacheRepository.DeleteByNovelIdSync` は既存メソッドを再利用（NovelRepository.DeleteAsync 内部でも使用中、[L174](../Services/Database/NovelRepository.cs#L174)）。

---

## H-2: `UpdateCheckService` が `HasUnconfirmedUpdate=true` を恒久スキップ

**問題:** [UpdateCheckService.cs:48](../Services/UpdateCheckService.cs#L48) は

```csharp
if (novel.HasUnconfirmedUpdate) continue;
```

意図は「ユーザがまだ更新を確認していない小説を、再チェックで通知再送しない」だが、実装としては**取得そのものをスキップ**しているため、その間に出た新々話を取りに行かない。

`HasUnconfirmedUpdate` は次の経路でしか `false` に戻らない:
- [NovelListViewModel.cs:147-152](../ViewModels/NovelListViewModel.cs#L147-L152) NavigateToDetail（目次タップ）
- [ReaderViewModel.cs:277-286](../ViewModels/ReaderViewModel.cs#L277-L286) MarkAsReadAsync で全話既読時

ユーザがアプリを開かない限り、その小説は更新追跡から脱落する。

**修正方針:** スキップ条件を「再通知抑制」と「取得スキップ」で分離する。今回は通知の重複抑制をやめて、「取得はする / 通知は最後に出す」に統一する。フィールド追加は最小限にする。

**修正 — `UpdateCheckService.cs:42-48`:**

```csharp
foreach (var novel in novels)
{
    if (ct.IsCancellationRequested) break;

    // 旧: if (novel.HasUnconfirmedUpdate) continue;
    // 取得自体は常に行う。HasUnconfirmedUpdate は新話追加検出時に再セットすればよい
    // （セット済みからセット済みへの遷移は無害）。

    try
    { /* 既存どおり */ }
    catch (...)
    { /* 既存どおり */ }
}
```

**副作用 1（通知の重複）:** [UpdateCheckWorker.cs:46-54](../Platforms/Android/UpdateCheckWorker.cs#L46-L54) の `ShowUpdateNotification` 側で `notificationId = novel.Id` のため、同じ ID で上書き表示される（Android の通知仕様）→ 重複通知にはならない。content text には [UpdateCheckService.cs:80](../Services/UpdateCheckService.cs#L80) の `newEpisodes.Count`（差分のみ）が渡されるため、「N話更新」→（次回チェックでさらに M 話追加なら）「M話更新」と上書き表示される。累積（N+M話）ではなく**差分のみが表示される**。再チェック後の差分が無ければ updates list に積まれず通知も飛ばないため、意図に沿う挙動。

**副作用 2（ネットワーク負荷）:** スキップ条件を外すと **HasUnconfirmedUpdate=true な小説も毎回 fetch** される。負荷見積もり（最悪ケース、登録 200 件すべて未確認 + 全件に新話あり）:

- `FetchNovelInfoAsync` × 200 件: 800ms ゲート × 200 = 約 2.6 分
- `FetchEpisodeListAsync` × 200 件: **なろう側はページネーションあり**（[NarouApiService.cs:85-141](../Services/Narou/NarouApiService.cs#L85-L141) の while ループで `?p=2`, `?p=3`, ... を `.c-pager__item--next` 不在まで取得）。長編 1 作品で複数ページのリクエストが発生するため、リクエスト数は「200 × 平均ページ数」になる。カクヨム側は 1 リクエストで TOC 完結（[KakuyomuApiService.cs:89-98](../Services/Kakuyomu/KakuyomuApiService.cs#L89-L98)）。
- カクヨムの場合は `FetchNovelInfoAsync` 内でも追加の HTTP 呼び出しはなく、同 1 リクエストでメタ取得まで済む
- **粗い試算**: なろう作品のページサイズが ~100 話/ページ前提で、平均 200 話/作品なら平均 2 ページ → なろう 100 件 × 2 ページ + カクヨム 100 件 × 1 ページ = 300 リクエスト ≒ 4 分。500 話超の長大作品が混じる構成では 5〜7 分まで膨らみ得る。
- 実態として全件未確認 + 全件更新ありはまず無く、現実的には 1〜2 分以内に収束する想定。とはいえ最悪ケースの上振れは旧プランの「~5 分」より大きい可能性がある点を実装レビュー時に意識する。

`UpdateCheckWorker` は Wi-Fi 接続時 + 6 時間周期 + バックグラウンドのため UX 影響は限定的だが、サイト側への負荷増は事実。許容範囲と判断（過剰負荷ならば後続で「N時間以内に再確認しない」程度の throttling を別 PR で導入）。

**副作用 3（Worker 実行時間制限・v8 で追加）:** AndroidX.Work の `Worker` クラスは通常 **10 分のタイムアウト**で強制終了される（[公式ドキュメント](https://developer.android.com/reference/androidx/work/Worker)）。本修正でスキップ条件を外すと最悪ケース 5〜7 分まで膨らむ可能性があり、**長編 + 大量登録 + すべて未確認 + すべて新話あり**という積み重なった条件下では 10 分上限に達して途中で強制 cancel される可能性がある。cancel された場合 `UpdateCheckWorker.DoWork` は中断され、残りの小説は次回 6h 後の周期実行で取得される。データ損失は発生しないが、通知が分散することになる。後続 PR で以下のいずれかを検討:
- (a) 「直近 N 分以内にチェック済みの小説はスキップ」する throttling（last_checked_at カラム + skip 条件）
- (b) `Worker` を `CoroutineWorker` 相当の長時間ジョブ用 binding に変更（AndroidX.Work で 10 分超を許容するための専用 API）
- (c) チェック対象を **batch 分割**（1 回の Worker 実行で 50 件まで、残りは次周期）

本 PR スコープでは対処しない（実頻度が低く、cancel 時の挙動も致命的ではないため）。

---

## H-3: `EpisodeListViewModel.RefreshReadStatusAsync` がフィルタ表示を更新しない

**問題:** [EpisodeListViewModel.cs:145-163](../ViewModels/EpisodeListViewModel.cs#L145-L163) は `_allEpisodes` と `Episodes` の `IsRead` を上書きするだけで、`_filteredCache` を再構築しない。
- `ShowUnreadOnly=true` 表示中、リーダーで既読化して戻ると、本来リストから消えるはずの話が「既読印」付きで残る。
- `ShowFavoritesOnly` には影響なし（既読化はお気に入り判定に関係しないため）。

**修正方針:** 既読状態を反映後、未読フィルタが ON の場合だけ `_filteredCache` を再構築・再ページング。

**修正 — `EpisodeListViewModel.cs:145-163`:**

```csharp
public async Task RefreshReadStatusAsync()
{
    if (_allEpisodes.Count == 0) return;

    var freshEpisodes = await _episodeRepo.GetByNovelIdAsync(_novelDbId);
    var readMap = freshEpisodes.ToDictionary(e => e.Id, e => e.IsRead);

    foreach (var ep in _allEpisodes)
    {
        if (readMap.TryGetValue(ep.Id, out var isRead))
            ep.IsRead = isRead;
    }

    if (ShowUnreadOnly)
    {
        // 既読化した話を未読フィルタから外す
        RebuildFilterCache();
        RecalcPaging();
        await LoadPageAsync();
    }
    else
    {
        // フィルタが OFF なら現在表示中のアイテムだけ in-place 更新
        foreach (var vm in Episodes)
        {
            if (readMap.TryGetValue(vm.Id, out var isRead))
                vm.IsRead = isRead;
        }
    }
}
```

**注意点:** `ShowUnreadOnly=true` で `RebuildFilterCache` を呼ぶと現在ページが空になる場合がある（最後の未読を読み終えた等）。`RecalcPaging` は `MaxPage = Math.Max(1, ...)` でガードされており `CurrentPage > MaxPage` で `MaxPage` に引き戻すため安全。

---

## H-4: `BackgroundJobQueue` Cancel 時に `_enqueuedEpisodeIds` がクリアされない

**問題:** [BackgroundJobQueue.cs:122-156](../Services/Background/BackgroundJobQueue.cs#L122-L156) の Worker ループは Wi-Fi 切断時 / `StopWorker` 時に `break` で抜ける。
- [BackgroundJobQueue.cs:191-194](../Services/Background/BackgroundJobQueue.cs#L191-L194) の finally で `_enqueuedEpisodeIds.Remove(job.EpisodeDbId)` するのは「処理に着手したジョブ」のみ。
- キューに残っている job の EpisodeId は HashSet に残ったまま → 同じ episode を再 Enqueue できない。
- 同一エピソードの prefetch 要求が重ねられる経路（手動ダウンロード→再度プリフェッチ）でデッドロック気味の "永久にスキップ" 状態に陥る。

**修正方針:** `StopWorker` 時に「キューに残っている job の HashSet を一括クリア」する。キュー本体（`_highPriority` / `_normalPriority`）は再開時に再消費されるので消さない。

**修正 — `BackgroundJobQueue.cs` に補助メソッド追加 + `StopWorker` を変更:**

```csharp
public void StopWorker()
{
    CancellationTokenSource? oldCts;
    Task? oldTask;
    lock (_startLock)
    {
        oldCts = _workerCts;
        oldTask = _workerTask;
        _workerCts = null;
        _workerTask = null;
    }
    if (oldCts is null) return;
    try { oldCts.Cancel(); }
    catch (ObjectDisposedException) { return; }

    // キューに残っている job の dedup HashSet を再開可能な状態に戻す。
    // キュー本体は消さない（Wi-Fi 復帰時にそのまま再消費される）。
    SyncEnqueuedIdsFromQueues();

    if (oldTask is not null)
    {
        _ = oldTask.ContinueWith(_ => oldCts.Dispose(), TaskScheduler.Default);
    }
    else
    {
        oldCts.Dispose();
    }
}

private void SyncEnqueuedIdsFromQueues()
{
    // ConcurrentQueue.GetEnumerator はスナップショットを返すため列挙中の変更で例外にはならない。
    // M-2 の EnqueueAsync は HashSet.Add と Queue.Enqueue を同一 lock 内で行うため、
    // 本メソッドが lock を取った時点で Queue 列挙の結果は HashSet と整合している
    // （HashSet に居るが Queue に未追加という中間状態が存在しない）。
    lock (_enqueuedEpisodeIds)
    {
        var live = new HashSet<int>();
        foreach (var j in _highPriority) live.Add(j.EpisodeDbId);
        foreach (var j in _normalPriority) live.Add(j.EpisodeDbId);
        _enqueuedEpisodeIds.Clear();
        foreach (var id in live) _enqueuedEpisodeIds.Add(id);
    }
}
```

**補足:** Live キューに残ってる ID だけ HashSet に再構成することで、(a) キューに居る重複 Enqueue 抑止は保たれ、(b) キューに無いものは再 Enqueue 可能になる。M-2 (PR-3) で `Enqueue` を `EnqueueAsync` に async 化する際、HashSet.Add と Queue.Enqueue を同一 lock 内に統合する変更を**併せて行う前提**である（v6 で明示化）。本 H-4 修正単体（PR-2 段階）では旧 `Enqueue` の lock 構造のまま `SyncEnqueuedIdsFromQueues` を追加することになるが、PR-2 の段階では Worker 停止中に新規 `Enqueue` が走る経路は SearchViewModel.RegisterAsync の `_ = _prefetch.EnqueueNovelAsync(...)` 等に限られ、Wi-Fi 切断中の Enqueue は実頻度が低い。race の可能性は残るが実害は重複 prefetch 1 回程度のため、PR-3 (M-2) のマージ後に lock 統合で恒久解消する。

**⚠ PR-2 description 必須記載事項（v8 で追加）:** 推奨マージ順 `PR-1 → PR-2 → PR-4 → PR-7 → PR-3 → PR-6 → PR-5` では PR-2 マージ後 PR-3 マージまでの間に **PR-4 と PR-7 の 2 PR が挟まる**。この期間中、H-4 で導入する `SyncEnqueuedIdsFromQueues` は旧 `Enqueue` の lock 構造のまま動作するため、Wi-Fi 切断中に発生する Enqueue で「HashSet にも Queue にもない job」が生まれる race が残存する。実害は同一 episode の重複 prefetch 1 回程度で UX への影響は無視できる範囲だが、**PR-2 description にこの race の存在と PR-3 (M-2) で恒久解消することを明記**してレビュアーに伝える。マージ順の変更は行わない（ユーザ実害が大きい N-1〜N-4・C-1〜C-3 の解消優先という旧プラン v4 の判断を据え置き）。

---

## L-2: リクエストディレイ Clamp 範囲ズレ（事実バグ格上げ）

**位置付け:** Low 分類だが事実バグ（UI スライダー 500-2000ms に対し、Clamp は 100-5000ms で UI 外の値も通る）であり、設定 UI 経由でなく旧バージョンの DB 値継承などで UI 範囲外の遅延が発生し得る。ユーザ実害最小化のため PR-4 から PR-2 に格上げ。

**問題:** [NetworkPolicyService.cs:121](../Services/Network/NetworkPolicyService.cs#L121) は `Math.Clamp(v, 100, 5000)`、UI スライダー / 定数は 500–2000 ([SettingsKeys.cs:26-27](../Helpers/SettingsKeys.cs#L26-L27))。

**修正 — `NetworkPolicyService.cs:118-122`:**

```csharp
private async Task<int> GetDelayMsAsync()
{
    var v = await _settingsRepo.GetIntValueAsync(
        SettingsKeys.REQUEST_DELAY_MS,
        SettingsKeys.DEFAULT_REQUEST_DELAY_MS).ConfigureAwait(false);
    return Math.Clamp(v, SettingsKeys.MIN_REQUEST_DELAY_MS, SettingsKeys.MAX_REQUEST_DELAY_MS);
}
```

`using LanobeReader.Helpers;` を確認（既にあり）。

**注意点:**
- 既存 DB に 100ms 等の UI 範囲外値（下振れ）が保存されているユーザでは、本修正後に強制的に MIN 側（500ms）へ引き上げられる。これはサイト負荷の観点から望ましい挙動。
- **逆方向（上振れ）の挙動変化にも注意（v7 で追記）**: 5000ms 等の旧 MAX 上限値が保存されているユーザでは、本修正後に強制的に新 MAX (2000ms) へ引き下げられ、**ユーザが設定したはずの値より速くリクエストされる**ことになる。サイト負荷増加方向の挙動変化のため、UX 上は「設定したはずなのに勝手に変わった」と感じられる可能性がある。リリースノートか初回起動時の通知で「設定値が UI 範囲 (500-2000ms) にクランプされた」旨を周知することを検討（ただし本 PR では実装しない）。
- PR-4 の L-1（SeedSettingsAsync ハードコード解消）と独立に適用可能。同 PR-2 内では H-4（BackgroundJobQueue）と同一ファイルを触らないため衝突なし。

---

# PR-3: UX 改善（M1-M5）

## M-1: `RankingPeriod.Quarterly` をカクヨムが silently fallback

**問題:** [SearchViewModel.cs:209-214](../ViewModels/SearchViewModel.cs#L209-L214) で `Quarterly` の場合 `_ => "weekly"` に落ちる。UI は「四半期」表示のまま結果は週間ランキング → ユーザに通知なし。

**修正方針:** Quarterly 時はカクヨム呼び出し自体を skip。`ExecuteSiteQueryAsync` のシグネチャに `string? prefixMessage = null` を追加し、skip 時の注釈を確実にエラーバナーへ流す。

`ExecuteSiteQueryAsync` の冒頭で `HasError = false; ErrorMessage = string.Empty;` ([SearchViewModel.cs:141-142](../ViewModels/SearchViewModel.cs#L141-L142)) が走るため、呼び出し前に HasError をセットしても上書きされる。シグネチャ側で受ける形が最も確実。

**修正 1 — `SearchViewModel.cs:135-177` の `ExecuteSiteQueryAsync`:**

```csharp
private async Task ExecuteSiteQueryAsync(
    string operationName,
    Func<CancellationToken, Task<List<SearchResult>>>? narouFetch,
    Func<CancellationToken, Task<List<SearchResult>>>? kakuyomuFetch,
    string? prefixMessage = null)        // ← 追加
{
    IsLoading = true;
    HasError = false;
    ErrorMessage = string.Empty;
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        var narouTask = narouFetch is not null
            ? RunSiteSearchAsync(() => narouFetch(ct), "なろう")
            : Task.FromResult<(List<SearchResult> hits, string? error)>(([], null));
        var kakuyomuTask = kakuyomuFetch is not null
            ? RunSiteSearchAsync(() => kakuyomuFetch(ct), "カクヨム")
            : Task.FromResult<(List<SearchResult> hits, string? error)>(([], null));

        var siteResults = await Task.WhenAll(narouTask, kakuyomuTask);

        var allHits = siteResults.SelectMany(r => r.hits).ToList();
        var errors = siteResults.Select(r => r.error).Where(e => e is not null).ToList();

        // prefixMessage があれば常にエラーバナーで先頭表示
        var combined = new List<string>();
        if (!string.IsNullOrEmpty(prefixMessage)) combined.Add(prefixMessage);
        combined.AddRange(errors!);
        if (combined.Count > 0)
        {
            HasError = true;
            ErrorMessage = string.Join("\n", combined);
        }

        await ShowResultsAsync(allHits);
    }
    catch (Exception ex)
    {
        LogHelper.Error(nameof(SearchViewModel), $"{operationName} failed: {ex.Message}");
        HasError = true;
        ErrorMessage = "通信エラーが発生しました";
    }
    finally
    {
        IsLoading = false;
    }
}
```

**修正 2 — `SearchViewModel.cs:191-219` の `FetchRankingAsync`:**

```csharp
[RelayCommand]
private Task FetchRankingAsync()
{
    var period = (RankingPeriod)Math.Clamp(RankingPeriodIndex, 0, 3);
    var kakuyomuSupported = period != RankingPeriod.Quarterly;
    // カクヨムが skip される場合だけ通知。文言はなろう側の選択状況で出し分け
    // （SearchNarou=false なら「なろうのみ取得」が嘘になるため）。
    string? prefixMessage = (!kakuyomuSupported && SearchKakuyomu)
        ? (SearchNarou
            ? "カクヨムは四半期ランキング非対応のため、なろうのみ取得します"
            : "カクヨムは四半期ランキング非対応です。取得対象がありません")
        : null;

    return ExecuteSiteQueryAsync(
        "Ranking fetch",
        SearchNarou
            ? ct =>
            {
                int? bg = null;
                if (SelectedNarouBigGenre is not null
                    && int.TryParse(SelectedNarouBigGenre.Id, out var bgv)) bg = bgv;
                return _narou.FetchRankingAsync(period, bg, 30, ct);
            }
            : null,
        (SearchKakuyomu && kakuyomuSupported)
            ? ct =>
            {
                var periodSlug = period switch
                {
                    RankingPeriod.Daily => "daily",
                    RankingPeriod.Weekly => "weekly",
                    RankingPeriod.Monthly => "monthly",
                    _ => "weekly",   // 到達しない（kakuyomuSupported で gate 済み）
                };
                return _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre?.Id ?? "all", periodSlug, ct);
            }
            : null,
        prefixMessage);
}
```

**注意点:** `prefixMessage` は「ユーザに告知すべき制限」を運ぶ専用ルート。サイト fetch そのものの失敗は従来どおり `errors` に集約され、`prefix` の後ろに連結表示される。

---

## M-2: `PrefetchEnabled=false` でもキューに溜まる（完全抑止版）

**問題:** [BackgroundJobQueue.cs:55-66](../Services/Background/BackgroundJobQueue.cs#L55-L66) の `Enqueue` は設定を見ない。Worker 入口だけ `PREFETCH_ENABLED` で early return ([L112-117](../Services/Background/BackgroundJobQueue.cs#L112-L117)) する。
- 設定 OFF のユーザでも `RegisterAsync` ([SearchViewModel.cs:301](../ViewModels/SearchViewModel.cs#L301)) や `DownloadAllAsync` がキューを膨張させる。
- HashSet と内部キューがメモリに残り続ける（プロセス生存期間）。

**修正方針:** `Enqueue` を **`EnqueueAsync` に async 化**し、内部で `_settingsRepo.GetIntValueAsync(PREFETCH_ENABLED, 1)` を await して判定する。`GetIntValueAsync` は内部で `GetValueAsync` を呼び（[AppSettingsRepository.cs:43-47](../Services/Database/AppSettingsRepository.cs#L43-L47)）、`GetValueAsync` 冒頭の `if (!_loaded) await LoadAllAsync()`（[AppSettingsRepository.cs:39](../Services/Database/AppSettingsRepository.cs#L39)）で `LoadAllAsync` 完了を保証する。これにより race window が完全に消える。同期 API 追加は不要。

**呼び出し側の影響範囲:** Enqueue を呼ぶ箇所は以下 3 経路（grep `\.Enqueue\(new PrefetchEpisodeJob` で確認済み）、いずれも既に async コンテキスト内 → `await _queue.EnqueueAsync(...)` への置換のみ:
- [PrefetchService.cs:44](../Services/Background/PrefetchService.cs#L44) `EnqueueNovelAsync` 内
- [PrefetchService.cs:75](../Services/Background/PrefetchService.cs#L75) `EnqueueAllUnreadAsync` 内
- [UpdateCheckService.cs:88](../Services/UpdateCheckService.cs#L88) 新話検出後の prefetch 投入

`SearchViewModel.RegisterAsync:301` の `_ = _prefetch.EnqueueNovelAsync(dbNovel.Id);` は PrefetchService 経由なので変更不要（PrefetchService 側が `await _queue.EnqueueAsync` に変わるだけで API シグネチャは保たれる）。

**修正 1 — `BackgroundJobQueue.cs:55-66` の `Enqueue` を async 化:**

```csharp
public async Task EnqueueAsync(PrefetchEpisodeJob job)
{
    // 設定 OFF なら drop（GetIntValueAsync は内部で LoadAllAsync 完了を保証するため race なし）
    var enabled = await _settingsRepo.GetIntValueAsync(
        SettingsKeys.PREFETCH_ENABLED,
        SettingsKeys.DEFAULT_PREFETCH_ENABLED).ConfigureAwait(false);
    if (enabled == 0) return;

    // HashSet.Add と ConcurrentQueue.Enqueue を同一 lock 内で完結させる（v6 で変更）。
    // 旧実装は Add 後に lock を抜けてから Enqueue していたため、
    // 「HashSet に居るが Queue に未追加」のすき間で StopWorker → SyncEnqueuedIdsFromQueues
    // が割り込むと、HashSet からも消えて Queue にも入らない job が発生し、
    // 後続の再 Enqueue 時に重複 prefetch が起きる race window があった。
    // ConcurrentQueue.Enqueue は thread-safe なので追加 lock のコストは軽微。
    lock (_enqueuedEpisodeIds)
    {
        if (!_enqueuedEpisodeIds.Add(job.EpisodeDbId)) return;
        if (job.Priority > 0) _highPriority.Enqueue(job);
        else _normalPriority.Enqueue(job);
    }

    EnsureWorkerStarted();
}
```

> **⚠️ 重要 (v7 で強調再掲)**: 旧 `public void Enqueue(PrefetchEpisodeJob job)` メソッドは **完全削除**する（非推奨 `[Obsolete]` マークやラッパー実装ではなく、メソッド定義そのものをファイルから削除）。理由: 残しておくと「設定 OFF でも drop しない旧経路」が暗黙に温存され、M-2 の「完全抑止」目的が達成できないため。`grep -n "void Enqueue" _Apps/Services/Background/BackgroundJobQueue.cs` の結果が **0 件**になることを実装後に確認すること。後方互換は不要（呼び出し側 3 経路をすべて同 PR で `EnqueueAsync` に置換するため）。

**修正 2 — `PrefetchService.cs:44, 75` の `_queue.Enqueue(...)` を await 化:**

```csharp
// EnqueueNovelAsync 内 (line 44 付近)
await _queue.EnqueueAsync(new PrefetchEpisodeJob { /* ... */ }).ConfigureAwait(false);

// EnqueueAllUnreadAsync 内 (line 75 付近) も同様
await _queue.EnqueueAsync(new PrefetchEpisodeJob { /* ... */ }).ConfigureAwait(false);
```

**修正 3 — `UpdateCheckService.cs:88` の `_jobQueue.Enqueue(...)` を await 化:**

```csharp
await _jobQueue.EnqueueAsync(new PrefetchEpisodeJob { /* ... */ }).ConfigureAwait(false);
```

`UpdateCheckService` の該当ループは既に async メソッド内のため、`await` 追加のみで OK。

**注意点:**
- Worker 入口の PREFETCH_ENABLED チェック（[BackgroundJobQueue.cs:112-117](../Services/Background/BackgroundJobQueue.cs#L112-L117)）は **削除しない**。設定変更タイミング次第で「Enqueue 時 ON → Worker 実行直前に OFF」のレースが発生し得るため、二重ガードを維持。
- Enqueue 1 件ごとに `GetIntValueAsync` を await するが、`_cache` はメモリ ConcurrentDictionary。**初回の 1 件目のみ** `LoadAllAsync` 経由で DB 初期化と全設定ロードを行うため数百 ms オーダーの遅延が発生し得る。**2 件目以降**は cache hit でマイクロ秒オーダー。`EnqueueAllUnreadAsync` で 200 話を一括投入する場合、初回 1 回 + cache hit 199 回 ≒ 数百 ms の遅延に収まる想定。
- **race window の完全抑止が成立する根拠**: `GetIntValueAsync`（[AppSettingsRepository.cs:43-47](../Services/Database/AppSettingsRepository.cs#L43-L47)）は内部で `GetValueAsync` を呼び、その冒頭（[:39](../Services/Database/AppSettingsRepository.cs#L39)）で `if (!_loaded) await LoadAllAsync()` を実行する。`LoadAllAsync`（[:22-35](../Services/Database/AppSettingsRepository.cs#L22-L35)）は `_loadGate` SemaphoreSlim でガードされ、二重チェックロックパターンで複数同時呼び出しを 1 回に集約する。さらに `_dbService.EnsureInitializedAsync`（[:29](../Services/Database/AppSettingsRepository.cs#L29)）を内部で呼ぶため DB 初期化前でも安全。よって Worker 早期起動時でも、最初の `EnqueueAsync` が DB 初期化 + 設定ロードを完了させてから判定を返す。
- **呼び出し側のパフォーマンス**: PrefetchService.EnqueueNovelAsync が直列に await することになるが、これは元から DB クエリを直列実行する設計（line 34, 37, 38）と同質で、新規ボトルネックは生まない。

---

## M-3: `ReaderViewModel.LoadEpisodeAsync` のオフライン早期 return

**問題:** [ReaderViewModel.cs:170-175](../ViewModels/ReaderViewModel.cs#L170-L175) はオフライン + 未キャッシュ時に `DisplayAlert` だけして return。`EpisodeContent` / `EpisodeTitle` が前話のまま残り、ユーザは「前話が読まれている」と錯覚する。

**PR-6 (L-9) との関係（重要）:** 本 M-3 は「前話の残り表示を消す」ことのみを目的とする最小修正で、**オフライン通知 UI は DisplayAlert のまま**にする。PR-6 (L-9) で全 ViewModel のエラー UI が `SetError` バナーに統一される際、ここの DisplayAlert も同時に SetError に置換される。**M-3 単体ではバナー化しない**（L-9 が ErrorAwareViewModel の導入を伴うため、PR-3 で先行導入すると基底クラス変更が PR-3 と PR-6 に分散して conflict 余地が増える）。

**修正方針:** ReaderPage に留まりつつ `EpisodeContent` / `EpisodeTitle` / `EpisodeHtml` を明示クリアして DisplayAlert を表示する。自動遷移 (`GoToAsync`) は採用しない（バナー視認性とリトライ可能性のため、L-9 の最終形と整合させる）。

**修正 — `ReaderViewModel.cs:168-175`:**

```csharp
else
{
    var connectivity = Connectivity.Current.NetworkAccess;
    if (connectivity != NetworkAccess.Internet)
    {
        // 前話の残り表示を防ぐためコンテンツをクリア（タイトルも）
        EpisodeContent = string.Empty;
        EpisodeTitle = string.Empty;
        EpisodeHtml = string.Empty;

        await Shell.Current.DisplayAlert("オフライン",
            "オフラインのため表示できません。キャッシュもありません。", "OK");
        return;
    }
    /* 既存どおり */
}
```

**注意点:**
- `EpisodeHtml` は縦書き WebView 用なので併せてクリア。
- `IsLoading` は finally で false になる。前話のデータをクリアした空白ページが表示される（ユーザが目次/戻るで自分で抜けられる）。
- PR-6 (L-9) でこの DisplayAlert は `SetError(...)` バナーに置換される。

---

## M-4: `DataTrigger Value="0"` と int の比較を確実化

**問題:** [ReaderPage.xaml:13-21, 51-71](../Views/ReaderPage.xaml#L13-L21) で `DataTrigger Binding="{Binding BackgroundThemeIndex}" Value="0"` を多用（ContentPage.Triggers 11-22, Label.Triggers 51-71）。MAUI では Value 属性は string で受け取り、int 値とは TypeConverter 経由で比較される。動作することが多いが、**EnumValue / int の二重解釈で動かない実機事例**があり、レビュー時点で実機検証していない箇所。

**修正方針:** `<x:Int32>0</x:Int32>` の明示要素構文で Type を強制する（`SettingsPage.xaml` の RadioButton.Value で既に同パターンを使用 [SettingsPage.xaml:69-76](../Views/SettingsPage.xaml#L69-L76)）。

**修正 — `ReaderPage.xaml:13-21`:**

```xml
<DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}">
    <DataTrigger.Value><x:Int32>0</x:Int32></DataTrigger.Value>
    <Setter Property="BackgroundColor" Value="{StaticResource ThemeWhiteBg}" />
</DataTrigger>
<DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}">
    <DataTrigger.Value><x:Int32>1</x:Int32></DataTrigger.Value>
    <Setter Property="BackgroundColor" Value="{StaticResource ThemeDarkBg}" />
</DataTrigger>
<DataTrigger TargetType="ContentPage" Binding="{Binding BackgroundThemeIndex}">
    <DataTrigger.Value><x:Int32>2</x:Int32></DataTrigger.Value>
    <Setter Property="BackgroundColor" Value="{StaticResource ThemeSepiaBg}" />
</DataTrigger>
```

同パターンを `Label.Triggers` の 6 個 ([L51-71](../Views/ReaderPage.xaml#L51-L71)) にも適用。

**注意点・優先度:** **現行コードが実機で動作しているなら本項は見送り可（preventive な改善であり、実害は確認されていない）**。PR-3 のレビュー時に「実機でテーマ切り替えがチラつく / 反映遅延 / DataTrigger 未発火」が観測された場合のみ適用する候補。コミットメッセージに「実機未検証の preventive 修正」と明記すること。

---

## M-5: `EpisodeListPage.OnAppearing` と `InitializeAsync` の競合

**問題:** [EpisodeListPage.xaml.cs:13-20](../Views/EpisodeListPage.xaml.cs#L13-L20) の `OnAppearing` は `_ = vm.RefreshReadStatusAsync()` を fire-and-forget。
- `ApplyQueryAttributes` が `_ = InitializeAsync()` を fire-and-forget しており、両者が並列実行され得る。
- 初回表示時は `_allEpisodes.Count == 0` で `RefreshReadStatusAsync` が早期 return するため事故にはならないが、Initialize 完了直後に OnAppearing が走るタイミングで二重 DB クエリが発生する。

**修正方針:** ViewModel 側に `Task _initTask` を保持し、OnAppearing は完了を待ってから RefreshReadStatusAsync する。

**修正 1 — `EpisodeListViewModel.cs` フィールド + ApplyQueryAttributes / InitializeAsync 改修:**

```csharp
private Task? _initTask;

public void ApplyQueryAttributes(IDictionary<string, object> query)
{
    if (query.TryGetValue("novelId", out var novelIdObj)
        && int.TryParse(novelIdObj?.ToString(), out var novelId))
    {
        _novelDbId = novelId;
        _initTask = InitializeAsync();
    }
}

public Task EnsureInitializedAsync() => _initTask ?? Task.CompletedTask;
```

**修正 2 — `EpisodeListPage.xaml.cs:13-20`:**

```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();
    if (BindingContext is EpisodeListViewModel vm)
    {
        await vm.EnsureInitializedAsync();
        await vm.RefreshReadStatusAsync();
    }
}
```

**注意点:**
- `async void OnAppearing` で例外を吐かないよう、`EnsureInitializedAsync` は内部で握り潰さない（InitializeAsync 自体が try/catch するため）。
- **H-3 との二重実行リスク**: 本修正で `InitializeAsync` 完了直後に `RefreshReadStatusAsync` が必ず走るが、H-3 (PR-2) で `RefreshReadStatusAsync` 内に `RebuildFilterCache` + `RecalcPaging` + `LoadPageAsync` を追加している。`InitializeAsync` 内で同じロジックを既に実行済みのため、初回表示時は `RebuildFilterCache` を 2 回呼ぶことになる。これは観測上の重複であって機能的問題はないが、ログで気付く可能性がある。最適化したい場合は `RefreshReadStatusAsync` に "isInitial" 引数を追加して `_allEpisodes.Count > 0 && readMap が変化した時のみ` 再構築するガードを入れる（本 PR スコープ外）。

---

# PR-4: コード品質 / 整理（L1-L10、L-3 を除く）

**PR 構成（再掲）:**
- **PR-4** (`feature/fix-l1-l10-quality`): L-1, L-5, L-6, L-7, L-8, L-10。dead code 削除と定数差し替え中心のクリーンアップ PR。
- **L-2 は PR-2 に格上げ移動**（事実バグのためユーザ実害最小化を優先）。詳細は PR-2 セクション「L-2: リクエストディレイ Clamp 範囲ズレ」を参照。
- **L-3 は PR-7 に移動**（N-1 と同じ `NarouApiService.SearchAsync` を触るため、1 コミットに統合する）。詳細は PR-7 セクション冒頭を参照。
- L-4 → PR-5（要件書キャッチアップ専用 PR）に分離。
- L-9 → PR-6（エラー UI 統一専用 PR）に分離。
- L-11 → 取り下げ（H-4 で完結）。

以下、個別項目の修正詳細。**本セクションには L-2 と L-3 を含めない**（それぞれ PR-2 / PR-7 へ移動済み）。


## L-1: `SettingsKeys.DEFAULT_*` と `SeedSettingsAsync` の重複ハードコード

**問題:** [DatabaseService.cs:105-117](../Services/Database/DatabaseService.cs#L105-L117) で `"cache_months" => "3"`, `"update_interval_hours" => "6"` 等が直書き。`SettingsKeys.DEFAULT_*` 定数と乖離するリスク。

**修正 1 — `SettingsKeys.cs` に新規定数を追加:**

```csharp
public const string DEFAULT_NOVEL_SORT_KEY = "updated_desc";

// PR-1 (C-1) で追加した「前回 schedule 時の周期値」記録キー。SeedSettingsAsync は "0" を入れる。
// 既に PR-1 で定数 LAST_SCHEDULED_HOURS = "last_scheduled_hours" を追加済みの場合は重複定義しない。
```

**修正 2 — `DatabaseService.cs:103-127`:**

```csharp
private async Task SeedSettingsAsync()
{
    var defaults = new Dictionary<string, string>
    {
        [SettingsKeys.CACHE_MONTHS]          = SettingsKeys.DEFAULT_CACHE_MONTHS.ToString(),
        [SettingsKeys.UPDATE_INTERVAL_HOURS] = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString(),
        [SettingsKeys.FONT_SIZE_SP]          = SettingsKeys.DEFAULT_FONT_SIZE_SP.ToString(),
        [SettingsKeys.BACKGROUND_THEME]      = SettingsKeys.DEFAULT_BACKGROUND_THEME.ToString(),
        [SettingsKeys.LINE_SPACING]          = SettingsKeys.DEFAULT_LINE_SPACING.ToString(),
        [SettingsKeys.EPISODES_PER_PAGE]     = SettingsKeys.DEFAULT_EPISODES_PER_PAGE.ToString(),
        [SettingsKeys.PREFETCH_ENABLED]      = SettingsKeys.DEFAULT_PREFETCH_ENABLED.ToString(),
        [SettingsKeys.REQUEST_DELAY_MS]      = SettingsKeys.DEFAULT_REQUEST_DELAY_MS.ToString(),
        [SettingsKeys.VERTICAL_WRITING]      = SettingsKeys.DEFAULT_VERTICAL_WRITING.ToString(),
        [SettingsKeys.NOVEL_SORT_KEY]        = SettingsKeys.DEFAULT_NOVEL_SORT_KEY,
        [SettingsKeys.LAST_SCHEDULED_HOURS]  = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString(),  // PR-1 で追加。値は UPDATE_INTERVAL_HOURS と同期 (= "6")
    };
    /* foreach 以下は既存どおり */
}
```

`using LanobeReader.Helpers;` を追加。`NOVEL_SORT_KEY` の既定値は string のため新規定数 `DEFAULT_NOVEL_SORT_KEY` を追加し、SeedSettingsAsync からは必ず定数経由で参照する（リテラル "updated_desc" を二重に書かない）。

**`AUTO_MARK_READ_ENABLED` は PR-7 (B-4) で追加する**（v6 で変更）。本 L-1 の段階では当該定数が未定義のため、本 defaults 辞書に含めるとビルドエラーになる。PR-7 着手時に本辞書へ `[SettingsKeys.AUTO_MARK_READ_ENABLED] = SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED.ToString()` の 1 行を追加する。

---

## L-2: `NetworkPolicyService.GetDelayMsAsync` の Clamp と SettingsKeys 不一致

**※ 本項は PR-2 に移動。** 修正詳細は PR-2 セクションの「L-2: リクエストディレイ Clamp 範囲ズレ（事実バグ格上げ）」を参照。理由は事実バグであり、ユーザ実害最小化のため H-1〜H-4 と同じ PR で先行マージしたい。

---

## L-3: `INovelService.SearchAsync(searchTarget)` パラメータが UI から指定されない

**※ 本項は PR-7 に移動。** 修正詳細は PR-7 セクションの「Step 1: searchTarget パラメータ削除（commit 1）」を参照。理由は N-1 と同じ `NarouApiService.SearchAsync` を触るため、両者を同一 PR にまとめて revert/再適用の整合性を保つため。

---

## L-4: `_Apps/Features/` の plan ファイル整理（要件書キャッチアップ込み）

**問題:** 18 ファイルの plan / audit / fixes / todo md。仕様書として残すべきは `requirements_lanovereader.md` のみだが、**作成日 (2026-04-03) 以降の plan で導入された主要機能が要件書に未反映**で、削除すると仕様の一次情報が失われる。

`requirements_lanovereader.md` を grep した結果、以下のキーワードが一切ヒットしない（= 要件書に未記載）:
- `vertical_writing` / `prefetch_enabled` / `request_delay_ms` / `is_favorite` / `favorited_at` / `novel_sort_key`
- `RankingPeriod` / `Quarterly` / `PrefetchService` / `BackgroundJobQueue` / `NetworkPolicyService`

これは plan ファイルだけが一次情報源になっている状態を意味し、削除すると:
1. 機能の意図・受容したトレードオフ（plan_2026-04-07 §0.3, §1.4 等）が失われる
2. なぜ縦書き時に WebView を使うか等の設計判断が辿れなくなる
3. 新規開発者・将来のリファクタが要件書を見ても現状を把握できない

**修正方針（PR-5 として独立化）:** 単純削除でなく **「要件書キャッチアップ → 削除」** の 2 段階。L-4 は PR-4（コード品質）からは外し、**ドキュメント専用の PR-5** に独立させる。理由は (a) 要件書更新は実コードを参照しながらの慎重なレビューが必要、(b) コード変更と混ぜると差分レビューが困難、(c) 完了タイミングが他の PR と独立。

### L-4 タスク手順（PR-5 で実施）

**Step 1 — 要件書未反映の仕様変更を一覧化**

各 plan ファイルを順に読み、以下 4 種を抽出して `requirements_lanovereader.md` への反映要否を判定:
| 種別 | 判定基準 |
|---|---|
| 機能追加 (F-XXX 相当) | 「機能 X を追加」「画面 Y に Z を追加」等。例: 縦書き、お気に入り、ランキング、ジャンルブラウズ、一括 DL、Wi-Fi gating |
| 設定キー追加 | `SettingsKeys` の追加。要件書 §3.2 F-007 設定値テーブルと §5 app_settings 初期レコードに追記 |
| DB スキーマ変更 | カラム追加・インデックス追加・UNIQUE 化。要件書 §5.1 テーブル定義に追記 |
| 設計方針の確定 | 「ハイブリッド方式」「BackgroundJobQueue による Wi-Fi gating」等。要件書 §8 実装上の注意事項に追記 |

抽出元ファイルの優先度（情報量の多い順）:
```
plan_2026-04-07_feature-expansion.md  ← 縦書き / 一覧ソート / お気に入り / 一括DL / ランキング (大)
plan_2026-04-08_pr2-data-performance.md
plan_2026-04-09_pr3a-reader-refactor.md
plan_2026-04-09_pr3b-reader-live-css.md
plan_2026-04-09_pr4-settings-search-refactor.md
plan_2026-04-10_pr5-search-optimization.md
plan_2026-04-10_pr7-reader-theme-mvvm.md
plan_2026-04-12_pr8-bugfix-improvements.md  ← Issue 4-10 の仕様確定
plan_2026-04-15_bugfix-b1-b12.md
plan_2026-04-16_risk-r1-r14.md
plan_2026-04-21_smell-s1-s15.md
plan_2026-04-22_refactor-f1-f8.md
audit_2026-04-08_apps-refactor.md
fixes_2026-04-06.md
todo_2026-04-14_code-review.md
plan_2026-04-10_pr6-code-quality.md  ← コード品質中心、新仕様少なめ
```

**Step 2 — `requirements_lanovereader.md` に追記**

抽出した仕様変更を要件書の該当セクションに反映:

(a) §3.1 機能一覧テーブルへの追加（推定）:
| 機能ID | 機能名 | 優先度 | 概要 |
|---|---|---|---|
| F-009 | 縦書き表示 | Should | 設定で縦書き ON/OFF 切替。WebView でハイブリッド実装 |
| F-010 | お気に入り（作品/話） | Should | 作品・話ごとに ★ トグル。一覧ソートと連動 |
| F-011 | ランキング/ジャンルブラウズ | Should | 期間別ランキング・大ジャンル別作品取得（なろう/カクヨム） |
| F-012 | 一括ダウンロード/先読み | Should | Wi-Fi 接続時のみバックグラウンドで未取得話をプリフェッチ |
| F-013 | 通信ポリシー | Must | サイト別直列化＋ディレイ。設定で調整可 |
| F-014 | 一覧ソート | Should | 更新日時/タイトル/作者/未読数/お気に入り優先 |

各機能の詳細（処理フロー・UI・例外系）は plan_2026-04-07 の該当セクションをベースに、要件書のフォーマットに揃えて転記。

(b) §3.2 F-007 設定管理テーブルへの追加:
| キー | 型 | デフォルト | 反映タイミング |
|---|---|---|---|
| vertical_writing | int | 0 | SCR-004 を次回開いた時 |
| prefetch_enabled | int | 1 | 即時（Enqueue で参照） |
| request_delay_ms | int | 800 | 次回 HTTP リクエスト時 |
| novel_sort_key | string | "updated_desc" | 即時 |

(c) §5.1 novels テーブルへの追加:
| カラム | 型 | NULL | デフォルト | 説明 |
|---|---|---|---|---|
| is_favorite | INTEGER | NG | 0 | お気に入りフラグ |
| favorited_at | TEXT | OK | NULL | お気に入り登録日時 |

§5.1 episodes テーブルへの追加: 同様に `is_favorite`, `favorited_at`。
§5.1 episode_cache に schema_version カラムへの言及を追加 (`app_settings` の `schema_version` キー、現在は v2)。
§5.1 episodes インデックスを「`(novel_id, episode_no)` UNIQUE（v2 で UNIQUE 化）」に修正。

(d) §8 実装上の注意事項への追加:
- 8.X NetworkPolicyService（サイト別 SemaphoreSlim + 設定ディレイで直列化）
- 8.Y BackgroundJobQueue（Wi-Fi 接続時のみ稼働、HashSet で重複抑止、5 失敗で中断）
- 8.Z 縦書きハイブリッド実装方針（横書き=Label、縦書き=ReaderWebView + ReaderHtmlBuilder）

(e) §6.1 パフォーマンス: HttpClient タイムアウトを実コードに合わせて見直し（検索 10s, 本文 10-20s, 更新チェック 30s, 話一覧 15s）。

(f) §6.3 例外・エラーハンドリングへの追加:
- L-9 の対応に伴い「ReaderViewModel の本文取得失敗・オフライン未キャッシュ時はモーダル DisplayAlert ではなく赤バナー（`HasError`/`ErrorMessage`）でインライン表示する。ユーザは戻る/目次ボタンで手動退出する」を明記。
- N-2 の対応に伴い「読了点を巻き戻した結果として `read_at = NULL` に戻されたエピソードのオリジナルの既読日時は復元不可」を明記。アンドゥ機構は要件外。

**Step 3 — 削除対象 plan ファイル**

`_Apps/Features/` 配下の `.md` は **計 18 ファイル**（本プラン作成日時点で確認済み）。内訳:
- 削除対象 16 ファイル（下記）
- 保持 2 ファイル: `requirements_lanovereader.md`（Step 2 で更新済み）と `plan_2026-04-30_review-c1-l11.md`（本プラン、全 PR 完了後に最終削除）

Step 2 完了後に以下を `git rm`:

```
audit_2026-04-08_apps-refactor.md
fixes_2026-04-06.md
plan_2026-04-07_feature-expansion.md
plan_2026-04-08_pr2-data-performance.md
plan_2026-04-09_pr3a-reader-refactor.md
plan_2026-04-09_pr3b-reader-live-css.md
plan_2026-04-09_pr4-settings-search-refactor.md
plan_2026-04-10_pr5-search-optimization.md
plan_2026-04-10_pr6-code-quality.md
plan_2026-04-10_pr7-reader-theme-mvvm.md
plan_2026-04-12_pr8-bugfix-improvements.md
plan_2026-04-15_bugfix-b1-b12.md
plan_2026-04-16_risk-r1-r14.md
plan_2026-04-21_smell-s1-s15.md
plan_2026-04-22_refactor-f1-f8.md
todo_2026-04-14_code-review.md
```

**保持対象:**
```
requirements_lanovereader.md       ← Step 2 で更新済み
plan_2026-04-30_review-c1-l11.md   ← 本プラン（PR-1〜PR-7 全マージ後に削除、本ファイル末尾の「削除予定」セクション参照）
```

### PR-5 のスコープと留意点

- **doc-only PR**。コード変更は含めない（含めるなら別 PR に分離）。
- 1 PR で 16 ファイル削除 + 要件書 ~300 行追記の構成。レビュアーが要件書側だけ集中レビューできるよう、コミットを分ける:
  1. `docs: requirements に F-009..F-014 を追加`
  2. `docs: requirements の §3.2 F-007 設定キー / §5 DB スキーマを実装に合わせて更新`
  3. `docs: requirements の §6 / §8 を実装に合わせて更新`
  4. `docs: 実装済み plan ファイルを除去`
- 要件書追記時は **plan の文言を要件書のフォーマット（処理フロー / 異常系 / 出力 表 / ViewModel プロパティ・コマンド表）に揃える**。雑な転記は避ける。
- 反映漏れ検出のため、最終 commit 後に `_Apps/Helpers/SettingsKeys.cs` と `_Apps/Models/*.cs` の全カラム/キーが要件書に登場することを目視で確認する。

---

## L-5: グローバル例外ハンドラに `TaskScheduler.UnobservedTaskException` 未登録

**問題:** [App.xaml.cs:35-38](../App.xaml.cs#L35-L38) は `AppDomain.UnhandledException` のみ。`Task.Run(InitializeAppAsync)` 等の fire-and-forget 例外を取りこぼす。

**修正 — `App.xaml.cs:35-38` の直後に追加:**

```csharp
TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    LogHelper.Error("App", $"Unobserved task exception: {args.Exception}");
    args.SetObserved(); // プロセス終了を抑止
};
```

---

## L-6: `_episodeIdCache` のメモリ蓄積（Kakuyomu）

**問題:** [KakuyomuApiService.cs:17](../Services/Kakuyomu/KakuyomuApiService.cs#L17) の `ConcurrentDictionary` は Singleton で TTL チェックは get 時のみ。Remove なし。登録小説数 × 5 分以上残存 → 数十～数百件の小説で数 MB の HTML JSON 残留。

**修正方針:** 過剰最適化を避けるため、上限件数 + Get 時の expired sweep を入れる程度。LRU は不要。

**修正 — `KakuyomuApiService.cs:100-113` の `GetEpisodeIdsAsync` 直前に sweep ヘルパー追加:**

```csharp
private const int EpisodeIdCacheMaxEntries = 100;

private void SweepExpiredEpisodeIdCache()
{
    var now = DateTime.UtcNow;
    var expired = _episodeIdCache
        .Where(kv => now - kv.Value.cachedAt >= EpisodeIdCacheTtl)
        .Select(kv => kv.Key)
        .ToList();
    foreach (var k in expired) _episodeIdCache.TryRemove(k, out _);

    // 上限超過時は古い順に削る
    if (_episodeIdCache.Count > EpisodeIdCacheMaxEntries)
    {
        var oldest = _episodeIdCache
            .OrderBy(kv => kv.Value.cachedAt)
            .Take(_episodeIdCache.Count - EpisodeIdCacheMaxEntries)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var k in oldest) _episodeIdCache.TryRemove(k, out _);
    }
}

private async Task<List<string>> GetEpisodeIdsAsync(string novelId, CancellationToken ct)
{
    SweepExpiredEpisodeIdCache();   // ← 追加
    if (_episodeIdCache.TryGetValue(novelId, out var cached)
        && DateTime.UtcNow - cached.cachedAt < EpisodeIdCacheTtl)
    {
        return cached.episodeIds;
    }
    /* 既存どおり */
}
```

---

## L-7: `NarouApiService` で `TimeZoneInfo.FindSystemTimeZoneById` 例外未処理

**問題:** [NarouApiService.cs:250](../Services/Narou/NarouApiService.cs#L250)。.NET 9 + Android では通常成功するが、`TimeZoneNotFoundException` 発生時にランキング取得が落ちる。

**修正 — `NarouApiService.cs:248-264` の `BuildRtype`:**

```csharp
private static string BuildRtype(RankingPeriod period)
{
    DateTime now;
    try
    {
        var jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
    }
    catch (TimeZoneNotFoundException)
    {
        // フォールバック: UTC + 9h（DST なし、JST 固定オフセット）
        now = DateTime.UtcNow.AddHours(9);
    }
    var today = now.Date;
    var dailyTarget = now.Hour < 8 ? today.AddDays(-2) : today.AddDays(-1);

    return period switch { /* 既存どおり */ };
}
```

---

## L-8: `RECEIVE_BOOT_COMPLETED` 権限が不要

**問題:** [AndroidManifest.xml:10](../Platforms/Android/AndroidManifest.xml#L10)。WorkManager は OS 再起動後も自動再開するため不要（明示的な BroadcastReceiver も実装していない）。アプリ自身の宣言として残す必要がない冗長な記載。

**修正 — `AndroidManifest.xml:10` を削除:**

```xml
<!-- 削除: <uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" /> -->
```

**注意点（効果の限界）:** `androidx.work:work-runtime` AAR が同権限を内部で宣言しているため、**APK 最終 manifest では manifest merger により `RECEIVE_BOOT_COMPLETED` は残る**。Google Play Console の権限一覧表示や審査時の指摘も、この削除だけでは解消されない可能性が高い。完全に取り除くには `<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" tools:node="remove" />` のスタブが必要だが、WorkManager の boot 後再開機能を破壊するため不可。本修正の主目的は「アプリ独自宣言として冗長な記載を整理する」コード品質改善であり、Play 審査対策ではない。

---

## L-9: ViewModel ごとのエラー UI 設計を完全統一

**問題:**
- NovelListViewModel: `HasCheckError` フラグだけ（テキストなし、ボタン表示制御のみ）
- SearchViewModel: `HasError` + `ErrorMessage` 赤バナー
- EpisodeListViewModel: ログのみ UI 無
- ReaderViewModel: `DisplayAlert`（モーダル、ナビゲーション中断）

「最低限 EpisodeListViewModel だけ追加」では結局 4 通りのパターンが残るため不統一。**全 ViewModel で同じ抽象 (HasError + ErrorMessage) と同じ表示 (赤バナー) に揃える**。

**修正方針:**

1. **共通基底クラス `ErrorAwareViewModel` を新設**し全 ViewModel に継承させる。
2. **エラー表示は全画面で「赤バナー」に統一**（ScrollView の最上段または ContentPage 最上段に固定配置）。
3. **`DisplayAlert` は確認ダイアログ専用**（削除確認・キャッシュクリア確認等）に限定。エラー通知用途では使わない。
4. **`HasCheckError` は `HasError` + `ErrorMessage` で置き換え**、手動更新ボタンは「`HasError` && `RefreshCommand.CanExecute`」で表示。

### 修正 1 — 共通基底クラス追加

**新規ファイル `_Apps/ViewModels/ErrorAwareViewModel.cs`:**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace LanobeReader.ViewModels;

/// <summary>
/// 全画面共通のエラー状態を持つ基底 ViewModel。
/// HasError=true のとき ErrorMessage を赤バナーで表示する規約。
/// 確認ダイアログ（削除確認等）には使わず、エラー通知のみに用いる。
/// </summary>
public abstract partial class ErrorAwareViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
```

`CommunityToolkit.Mvvm` 8.x は `[ObservableProperty]` の継承に対応している（partial class + source generator）。子クラスでも `[ObservableProperty]` を追加できる。

### 修正 2 — 共通バナースタイル追加

**`_Apps/Resources/Styles/Styles.xaml` または共通リソース辞書に追加（既存ファイル名に合わせる）:**

```xml
<Style x:Key="ErrorBanner" TargetType="Label">
    <Setter Property="BackgroundColor" Value="{StaticResource DestructiveRed}" />
    <Setter Property="TextColor" Value="White" />
    <Setter Property="Padding" Value="16,12" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="LineBreakMode" Value="WordWrap" />
</Style>
```

`DestructiveRed` は既存 `SettingsPage.xaml:25` で参照済み。新規定義不要。

### 修正 3 — 4 ViewModel の継承先を統一

| ViewModel | Before | After |
|---|---|---|
| `NovelListViewModel` | `ObservableObject` + `HasCheckError` | `ErrorAwareViewModel` |
| `SearchViewModel` | `ObservableObject` + `HasError`/`ErrorMessage` | `ErrorAwareViewModel` |
| `EpisodeListViewModel` | `ObservableObject` | `ErrorAwareViewModel` |
| `ReaderViewModel` | `ObservableObject` | `ErrorAwareViewModel` |
| `SettingsViewModel` | `ObservableObject` | `ErrorAwareViewModel`（`ClearCacheAsync` の DisplayAlert は確認ダイアログのため据え置き）|

各クラスの修正:

**`NovelListViewModel.cs`:**
```csharp
public partial class NovelListViewModel : ErrorAwareViewModel  // 継承変更

// 削除: [ObservableProperty] private bool _hasCheckError;
// LoadNovelsAsync 内 HasCheckError を SetError("...") / ClearError() に置換

private async Task LoadNovelsAsync()
{
    try
    {
        var rows = await _novelRepo.GetAllWithUnreadCountAsync(SortKey);
        Novels = new ObservableCollection<NovelCardViewModel>(
            rows.Select(r => NovelCardViewModel.FromModel(r.Novel, r.UnreadCount)));
        if (rows.Any(r => r.Novel.HasCheckError))
            SetError("一部のタイトルで更新チェックに失敗しました");
        else
            ClearError();
    }
    catch (Exception ex)
    {
        LogHelper.Error(nameof(NovelListViewModel), $"LoadNovelsAsync failed: {ex.Message}");
        SetError("一覧の読み込みに失敗しました");
    }
}

// RefreshAsync の catch も同様に SetError("...")
```

**`SearchViewModel.cs`:**
```csharp
public partial class SearchViewModel : ErrorAwareViewModel  // 継承変更

// 削除: [ObservableProperty] private bool _hasError;
//       [ObservableProperty] private string _errorMessage = string.Empty;
// ExecuteSiteQueryAsync 内 `HasError = false; ErrorMessage = string.Empty;` → ClearError();
//                          `HasError = true; ErrorMessage = ...` → SetError(...);
```

**`EpisodeListViewModel.cs`:**
```csharp
public partial class EpisodeListViewModel : ErrorAwareViewModel, IQueryAttributable

// InitializeAsync の catch:
catch (Exception ex)
{
    LogHelper.Error(nameof(EpisodeListViewModel), $"InitializeAsync failed: {ex.Message}");
    SetError($"目次の読み込みに失敗しました: {ex.Message}");
}

// RefreshReadStatusAsync の DB 例外も SetError でハンドル
```

**`ReaderViewModel.cs`（重要な挙動変更あり）:**
```csharp
public partial class ReaderViewModel : ErrorAwareViewModel, IQueryAttributable

// LoadEpisodeAsync の catch を全面置換:
catch (TaskCanceledException)
{
    SetError("タイムアウトしました");
}
catch (HttpRequestException ex)
{
    SetError($"本文の取得に失敗しました（HTTPエラー: {ex.Message}）");
}
catch (Exception ex)
{
    SetError($"本文の取得に失敗しました（{ex.Message}）");
}

// オフライン早期 return（M-3 の修正を最終形に発展、GoToAsync は呼ばない）:
if (connectivity != NetworkAccess.Internet)
{
    // ★ v7 注記: 下の 3 行のコンテンツクリアは PR-3 M-3 で既に投入済み。
    //   L-9 (本 PR) では既存の 3 行はそのまま残し、M-3 で書いた DisplayAlert 行のみを
    //   SetError("...") 1 行に**置換**する。3 行を二重に追加しないこと（git diff レビュー時に
    //   コンテンツクリアの追加差分が見えるのは異常）。
    EpisodeContent = string.Empty;
    EpisodeTitle = string.Empty;
    EpisodeHtml = string.Empty;
    SetError("オフラインのため表示できません。キャッシュもありません");
    // ReaderPage に留まりバナー表示。ユーザは戻るボタンまたは目次ボタンで自分で戻る。
    return;
}
```

**M-3 (PR-3) との関係（重要・v7 で二重実装防止を強調）:** PR-3 M-3 で「コンテンツクリア + DisplayAlert」を導入し、本 PR-6 (L-9) でその DisplayAlert を `SetError` バナーに置換する。**両 PR は直列で適用される**（M-3 が中間状態、L-9 が最終形）。自動遷移 (`GoToAsync`) は両 PR で採用しない。

PR 別の差分内訳:
- **PR-3 M-3 担当**: `EpisodeContent` / `EpisodeTitle` / `EpisodeHtml` の明示クリア（**3 行追加**）と DisplayAlert 表示。基底クラスは `ObservableObject` のまま。
- **PR-6 L-9 担当**: `ReaderViewModel` の継承を `ErrorAwareViewModel` に変更し、M-3 で入れた DisplayAlert **1 行を `SetError("...")` に置換**。コンテンツクリアの 3 行は**新規追加せず M-3 のものをそのまま残す**（git diff 上で 3 行のコンテンツクリアが追加されている場合は二重実装になっているサイン）。

ここでの整理理由:
- ErrorAwareViewModel の導入は L-9 の責務であり、M-3 単独でバナー化すると基底クラス変更が PR-3 と PR-6 の両方に分散して conflict 余地が増える。
- 「前話の表示が残らない」目的は M-3 のコンテンツクリア（PR-3）で先行達成できるため、L-9 まで待たずに UX 悪化を解消できる。
- ReaderViewModel と EpisodeListViewModel は別インスタンスのため、`SetError` 後に `GoToAsync` するとバナーがほぼ視認されないまま遷移する。よって自動遷移は両 PR で不採用。

**DisplayAlert 廃止の UX 影響:** ReaderViewModel の `DisplayAlert` 廃止により**モーダル待機がなくなる**。元々モーダルで「OK タップでブロック」していた挙動は失われるが、(a) インライン赤バナー表示の方がエラー文言を継続的に視認できる、(b) 戻るボタン/目次ボタンを操作可能なまま保てるため UX 改善の方向。要件書 §6.3 例外・エラーハンドリングへの追記が必要（L-4 と整合する）。

### 修正 4 — 5 画面の XAML に共通バナーを配置（ページごとの詳細）

各 `ContentPage` の **現行ルート構造が異なる**ため、共通パターンだけを書くと既存子要素の `Grid.Row` 番号付け直し（リナンバリング）を見落とす。**各ページ個別に手順を明示する**。

共通バナー要素（5 画面で同一）:

```xml
<Label Text="{Binding ErrorMessage}"
       IsVisible="{Binding HasError}"
       Style="{StaticResource ErrorBanner}" />
```

#### 4-1: `NovelListPage.xaml`
**現行**: ルートは `<Grid>`（[NovelListPage.xaml:15](../Views/NovelListPage.xaml#L15)、`RowDefinitions` なしの暗黙単行）。子要素 `ActivityIndicator`（line 17）と `CollectionView`（line 21）に `Grid.Row` 指定なし。

**修正**:
- ルート `<Grid>` → `<Grid RowDefinitions="Auto,*">`
- 既存子要素（`ActivityIndicator`, `CollectionView`）に **`Grid.Row="1"` を新規付与**
- 新 Row 0 にエラーバナー Label を追加

```xml
<Grid RowDefinitions="Auto,*">
    <Label Grid.Row="0" Text="{Binding ErrorMessage}" IsVisible="{Binding HasError}"
           Style="{StaticResource ErrorBanner}" />
    <ActivityIndicator Grid.Row="1" IsRunning="{Binding IsLoading}" ... />
    <CollectionView Grid.Row="1" ItemsSource="{Binding Novels}" ... />
</Grid>
```

#### 4-2: `SearchPage.xaml`
**現行**: ルート `<Grid RowDefinitions="Auto,Auto,Auto,*">` ([SearchPage.xaml:10](../Views/SearchPage.xaml#L10))。Row 0=モードタブ / Row 1=モード別入力 / Row 2=サイトフィルタ / Row 3=結果。
**重要な既存負債**: [SearchPage.xaml:85-86](../Views/SearchPage.xaml#L85-L86) に旧仕様のエラー表示 `<Label Text="{Binding ErrorMessage}" IsVisible="{Binding HasError}" TextColor="Red" Padding="16" FontSize="14" />` がすでに存在する（Row 3 内）。L-9 でルート行に格上げするため、**この line 85-86 の Label は削除する**。

**修正**:
- ルート `RowDefinitions="Auto,Auto,Auto,*"` → `"Auto,Auto,Auto,Auto,*"`（先頭に Auto 1 行追加）
- 既存子要素の `Grid.Row` を **+1 リネーム**: `"0"` → `"1"`、`"1"` → `"2"`、`"2"` → `"3"`、`"3"` → `"4"`（HorizontalStackLayout, Grid, HorizontalStackLayout, Grid の 4 個）
- 新 Row 0 にエラーバナー Label を追加
- 旧 line 85-86 の Label `TextColor="Red"` 版を削除

#### 4-3: `EpisodeListPage.xaml`
**現行**: ルート `<Grid RowDefinitions="Auto,*,Auto">` ([EpisodeListPage.xaml:10](../Views/EpisodeListPage.xaml#L10))。Row 0=フィルタ+アクション / Row 1=ローディング+リスト（`ActivityIndicator` と `CollectionView` の重ね描画） / Row 2=フッター。

**修正**:
- ルート `RowDefinitions="Auto,*,Auto"` → `"Auto,Auto,*,Auto"`
- 既存子要素の `Grid.Row` を **+1 リネーム**: HorizontalStackLayout `"0"` → `"1"`、ActivityIndicator/CollectionView `"1"` → `"2"`、フッター Grid `"2"` → `"3"`
- 新 Row 0 にエラーバナー Label を追加

#### 4-4: `ReaderPage.xaml`
**現行（PR-6 着手時の想定 base）**: 推奨マージ順 `PR-7 → PR-6` に従うため、PR-6 着手時点では PR-7 (B-4 修正 e) で追加された **既読ボタン単独 Overlay** が `Grid.Row="2"` で既に存在している。base 構造はルート `<Grid RowDefinitions="Auto,*,Auto">` ([ReaderPage.xaml:24](../Views/ReaderPage.xaml#L24))。Row 0=ヘッダ / Row 1=ローディング+本文（Label）+本文（ReaderWebView）の 3 重ね / Row 2=フッター + Overlay Button (PR-7 で追加)。

**修正**:
- ルート `RowDefinitions="Auto,*,Auto"` → `"Auto,Auto,*,Auto"`
- 既存子要素の `Grid.Row` を **+1 リネーム**（**6 個**、v10 で Overlay Button を追加）: ヘッダ Grid `"0"` → `"1"`、ActivityIndicator `"1"` → `"2"`、ScrollView `"1"` → `"2"`、ReaderWebView `"1"` → `"2"`、フッター Grid `"2"` → `"3"`、**Overlay Button (PR-7 B-4 修正 e で追加) `"2"` → `"3"`**
- 新 Row 0 にエラーバナー Label を追加（ヘッダの上に表示される）

**v10 注記（リナンバリング漏れの実害）:** Overlay Button のリナンバリングを忘れると、新 Row 0 にエラーバナーが配置されたあと、Overlay Button が `Grid.Row="2"` のまま残り **本文行（ScrollView/ReaderWebView の新 Grid.Row="2"）と重なって左下に表示**される。HasError=false でも Overlay Button は `IsManualReadButtonOverlayVisible` の条件で表示され得るため、リナンバリング漏れは見た目で即わかる重大な不具合になる。実機検証時は「自動 OFF + フッタ非表示」状態を作って必ず確認。

**PR-7 マージ前 base で着手した場合の差分**: もし PR-6 を PR-7 より前に着手する変則順序を採る場合（推奨外）、base に Overlay Button は無いため上記の 6 個目のリナンバリングは不要（5 個に戻る）。PR-7 マージ時に「Overlay Button を追加する際の Grid.Row 値」を `"2"` ではなく `"3"` で書く必要があるため、PR-7 のサンプル XAML（B-4 修正 e）も同時に書き替える必要がある。本プランでは推奨マージ順 `PR-7 → PR-6` を前提とするため、6 個リナンバリングが正となる。

#### 4-5: `SettingsPage.xaml`
**現行**: ルートは `<ScrollView>`（[SettingsPage.xaml:9](../Views/SettingsPage.xaml#L9)）で `<VerticalStackLayout>` を内包。Grid 構造を持たない。

**修正**: 全体を `<Grid RowDefinitions="Auto,*">` でラップする。

```xml
<Grid RowDefinitions="Auto,*">
    <Label Grid.Row="0" Text="{Binding ErrorMessage}" IsVisible="{Binding HasError}"
           Style="{StaticResource ErrorBanner}" />
    <ScrollView Grid.Row="1">
        <VerticalStackLayout ...>
            <!-- 既存内容そのまま -->
        </VerticalStackLayout>
    </ScrollView>
</Grid>
```

---

#### 共通の検証チェックリスト
全 5 ページで以下を確認:
- [ ] エラーバナー Label の `Grid.Row="0"` がルート Grid 直下にある
- [ ] 既存子要素の `Grid.Row` が漏れなくリナンバリングされている（特に SearchPage の 4 個・**ReaderPage の 6 個（Overlay Button 含む、v10 で訂正）** に注意）
- [ ] `HasError=false` 時にバナー領域が `IsVisible=false` で完全に非表示になり、レイアウトに空白行が残らない（Auto 行は IsVisible=false で潰れる）
- [ ] SearchPage 旧 Label（`TextColor="Red"`）が削除されている
- [ ] `Style="{StaticResource ErrorBanner}"` がリソース解決できる（修正 2 で Styles.xaml に追加済みであること）
- [ ] **ReaderPage で「自動 OFF + フッタ非表示（`IsManualReadButtonOverlayVisible=true`）」状態を作り、Overlay Button が左下に正しく表示され、本文行（新 Grid.Row="2"）と重ならないこと**（v10 で追加。PR-7 で追加した Overlay の Grid.Row リナンバリング漏れを捕捉する。binding 未導入で IsFooterVisible=false 経路に到達できない場合は、デバッグ用に `IsFooterVisible = false` を手動で立てるユニットテスト的な確認、もしくは ReaderViewModel に一時的なデバッグコマンドを足してから戻すことで検証）

### 修正 5 — `RefreshCommand` の表示条件を更新（NovelListViewModel）

旧仕様: `HasCheckError == true` で手動更新ボタン表示。
新仕様: `HasError == true` で表示（同じ意味になる）。

**`NovelListPage.xaml`** の ToolbarItem:
```xml
<ToolbarItem Text="再試行" Command="{Binding RefreshCommand}"
             IsEnabled="{Binding HasError}" />
<!-- IsVisible= の代替として IsEnabled で制御。常時アイコン表示で UI 跳ね回りを防ぐ -->
```

`HasError` フラグは「手動再試行が意味を持つかどうか」と一致するため流用可能。意味が乖離するケース（DB 読込失敗 vs 通信失敗）が出てきたら別フラグに分離するが、現状要件では問題なし。

### 検証

| 観察 | 期待 |
|---|---|
| NovelListPage で更新失敗 → 赤バナー「一部のタイトルで…」+ ToolbarItem 「再試行」活性 | ✓ |
| SearchPage 通信エラー → 赤バナーに「なろうの通信エラー…」 | ✓ |
| EpisodeListPage 初回読込失敗 → 赤バナー表示 | ✓ |
| ReaderPage 本文取得失敗 → 赤バナー、目次遷移なし、再読込可能 | ✓（仕様変更）|
| ReaderPage オフライン+キャッシュ無 → 赤バナー表示、本文/タイトル/HTML がクリアされ前話が残らない、目次/戻るボタンで手動退出可能（自動遷移なし） | ✓ |
| 設定変更ダイアログ・削除確認ダイアログは従来どおり `DisplayAlert` でモーダル表示 | ✓ |

### スコープと PR 配置

- **新ファイル 1 / 既存 5 ViewModel + 5 XAML + 1 リソースファイル = 12 ファイル / ~150 行**
- 当初プランの「最低限 EpisodeListViewModel だけ」より大きいが、PR-3 (M-1〜M-5) と PR-4 のサイズ感には収まる。
- **L-9 を独立 PR に切り出し**（PR-6: `feature/refactor-error-ui-unification`）が妥当。理由: (a) 全 ViewModel/View に変更が及ぶため diff レビューが大きい、(b) 仕様変更（ReaderViewModel の DisplayAlert 廃止）を含むためレビューポイントが質的に異なる、(c) 他の PR と独立してマージ可能。
- 要件書 §6.3 への追記（L-4 / PR-5）と整合させる。
- `HasCheckError` カラムは DB に残るが（NovelRepository が SELECT で読む）、ViewModel 側で `HasError` の判定材料として使うのみ → 後方互換破壊なし。

### rebase 要件（重要）

- 推奨マージ順 `PR-1 → PR-2 → PR-4 → PR-7 → PR-3 → PR-6 → PR-5` に従うと、**PR-6 着手時点で PR-2 / PR-3 / PR-7 の ViewModel 変更が全て base に取り込まれている**。本 PR の対象 5 ViewModel（NovelListViewModel / SearchViewModel / EpisodeListViewModel / ReaderViewModel / SettingsViewModel）はそれぞれ:
  - `SearchViewModel`: H-1（PR-2）/ M-1（PR-3）/ L-3 + N-1 + N-3（PR-7）の改修済み
  - `EpisodeListViewModel`: H-3（PR-2）/ M-5（PR-3）の改修済み
  - `ReaderViewModel`: M-3（PR-3）/ N-2（PR-7）の改修済み
  - `NovelListViewModel` / `SettingsViewModel`: 他 PR の対象外
- L-9 の改修内容（継承先を `ErrorAwareViewModel` に変更、`HasError`/`ErrorMessage` 統一、DisplayAlert → SetError 置換、XAML への赤バナー配置）はそれぞれ独立した位置の編集のためロジック衝突は無いが、**ブランチ作成直前に `git fetch && git rebase origin/app-novelviewer` を必ず実行**してから手を入れること。
- PR description にも上記順序と「全先行 PR を取り込み済みであること」を明記する。

---

## L-10: `KakuyomuApiService` の `[class*='EpisodeBody']` 過剰マッチ可能性

**問題:** [KakuyomuApiService.cs:213](../Services/Kakuyomu/KakuyomuApiService.cs#L213) は class 名部分一致のため `EpisodeBodyHeader` 等にもマッチし得る。

**修正方針:** 主セレクタ → fallback セレクタの優先順を明示。`QuerySelector` を 2 段階で試す。

**修正 — `KakuyomuApiService.cs:209-217`:**

```csharp
// CSS3 の [class~='X'] は「スペース区切り単語の完全一致」を表すため、
// `EpisodeBodyHeader` 等の連結クラス名にはマッチしない（過剰マッチ回避）。
var contentEl =
    episodeDoc.QuerySelector(".widget-episodeBody") ??
    episodeDoc.QuerySelector("[class~='EpisodeBody']");

if (contentEl is null)
{
    throw new InvalidOperationException("本文の取得に失敗しました（サイト構造が変わった可能性があります）");
}
```

**注意点:** `[class~='EpisodeBody']` は CSS3 標準の word-match セレクタで、AngleSharp も対応。`[class^=...]` + `[class*=' ...']` の 2 段組より意図が明確で 1 セレクタで完結する。

---

## L-11: `BackgroundJobQueue.StopWorker` の `CancellationTokenSource.Dispose` 競合

**問題:** [BackgroundJobQueue.cs:83-105](../Services/Background/BackgroundJobQueue.cs#L83-L105)。`oldTask is null` 経路で即 Dispose、`oldTask is not null` 経路で ContinueWith 経由で Dispose。Worker 内 `Task.Delay(_, ct)` が ObjectDisposedException を投げる経路がレアながら残る。

**判定（取り下げ）:** 現行 [BackgroundJobQueue.cs:97-104](../Services/Background/BackgroundJobQueue.cs#L97-L104) は既に
```csharp
_ = oldTask.ContinueWith(_ => oldCts.Dispose(), TaskScheduler.Default);
```
で **Worker 完了後にのみ Dispose** している。よって「Cancel 後すぐ Dispose → Worker 内 `Task.Delay(_, ct)` が ObjectDisposedException」という race は実コードでは発生しない。

差分として残るのは「Dispose を try/catch で囲む防衛コード」と「H-4 由来の `SyncEnqueuedIdsFromQueues` 呼び出し追加」のみで、前者は具体的な原因不明な多重 Dispose を想定する形になっており**現実の問題への対応として根拠が弱い**。

**結論:** L-11 は **取り下げ**、`SyncEnqueuedIdsFromQueues` の呼び出しは H-4（PR-2）の修正に既に含めて完結させる。PR-4 では本項を扱わない。

これにより PR-2 / PR-4 の同ファイル衝突も自動的に解消する。

---

# 検証計画

## ビルド検証

- 全 PR で `dotnet build _Apps/App.sln --no-restore` がエラー 0、警告増加なし。
- PR-2 の `using` 追加 / PR-3 の XAML 編集後は MAUI XAML コンパイルエラーがないか確認。

## 機能検証（手動）

| 項目 | 観察 |
|---|---|
| C-1 | 設定で `update_interval_hours` を 12 に変更 → アプリ再起動 → `adb shell dumpsys jobscheduler | grep lanobe` で 12h スケジュール確認 |
| C-2 | アプリ起動時の `adb logcat | grep FontRegistrar` で WARN が出ないこと |
| C-3 | アプリを「最近のアプリ」から swipe 削除後、6h 待たずに `adb shell cmd jobscheduler run` で Worker 強制実行 → Result.Retry / Success が返ること |
| H-1 | 機内モードで `RegisterAsync` を実行 → Novel が DB に残らないこと（再検索で「未登録」表示） |
| H-2 | HasUnconfirmedUpdate=true の小説に手動で新話追加 → 次回 UpdateCheck で取得されること |
| H-3 | 未読フィルタ ON → リーダーで既読化 → 戻ると当該話がリストから消えること |
| H-4 | Wi-Fi → モバイル切替 → Wi-Fi 再接続 → 同 episode の prefetch が再開すること |
| M-1 | ランキング「四半期」+ Kakuyomu ON → 「カクヨムは四半期非対応」表示、なろう結果のみ |
| M-2 | (a) PrefetchEnabled OFF → 新規登録 → メモリプロファイラで HashSet/Queue が空のまま (b) PrefetchEnabled OFF + UpdateCheckWorker を `adb shell cmd jobscheduler run` で先行実行 → BackgroundJobQueue.PendingCount が 0 のまま（race window 完全抑止の検証） (c) PrefetchEnabled ON → 新規登録 → 未キャッシュ話が PendingCount に積まれること |
| M-3 | オフラインで未キャッシュ話を開く → ReaderPage に留まり、本文/タイトル/HTMLが空、ダイアログ「オフライン...」が表示される（PR-6 後はバナー表示） |
| M-4 | テーマ切替（白 → 黒 → セピア）が即座に反映 |
| M-5 | リーダー → 戻る、を 5 回繰り返して Logcat に DB 例外が出ないこと |

Low 系（PR-4）は機能影響が小さいので、ビルド・起動確認のみ。例外として **L-2 はリクエストディレイの Clamp 範囲ズレを是正する事実バグ**のため、設定 UI で 500/1000/2000ms に変更後、Logcat の HTTP 出力で実ディレイを確認する。

**PR-6 (L-9) の検証:**
| 観察 | 期待 |
|---|---|
| 5 画面すべてで同じ赤バナー UI | ✓ |
| 機内モード起動 → NovelListPage で「一部のタイトル…」+ 再試行ボタン活性 | ✓ |
| 機内モードで検索 → SearchPage 赤バナー | ✓ |
| 機内モードで未キャッシュ話を開く → ReaderPage 赤バナー「オフラインのため表示できません...」、本文/タイトル/HTML がクリアされ前話が残らない、目次/戻るボタンで自分で抜けられる | ✓ |
| ReaderPage でオンライン復帰 → 戻る → 再度同じ話を開く → バナー消失、本文表示成功 | ✓ |
| 削除確認・キャッシュクリア確認は従来どおりモーダルダイアログ | ✓ |
| **(v10 追加) ReaderPage で「自動 OFF + フッタ非表示」（`IsManualReadButtonOverlayVisible=true`）状態を作り、Overlay Button が左下に表示され、本文（新 Grid.Row="2"）と重ならないこと** — Overlay Button の Grid.Row リナンバリング（`"2"` → `"3"`）漏れを必ず捕捉する | ✓ |
| **(v10 追加) ReaderPage で `HasError=true` + 自動 OFF + フッタ非表示の同時成立** — Row 0 にバナー、Row 3 の左下に Overlay Button が**互いに干渉せず**正しく表示されること | ✓ |

**PR-5 (L-4) の検証:**
- `requirements_lanovereader.md` を grep で `vertical_writing` `prefetch_enabled` `is_favorite` `RankingPeriod` `BackgroundJobQueue` `NetworkPolicyService` `novel_sort_key` `request_delay_ms` 等が**ヒットすること**を確認
- `_Apps/Helpers/SettingsKeys.cs` の全キーが要件書に登場することを確認
- 削除した plan ファイルが `git log --diff-filter=D` で確認できること

**PR-7 (L-3 + N-1〜N-4) の検証:**
| 項目 | 観察 |
|---|---|
| L-3 | Step 1 単体で `dotnet build` がエラー 0 で通ること（searchTarget 削除後の searchViewModel/INovelService/両 ApiService の整合）|
| N-1 | なろうで「異世界転生」を検索 → タイトル or 作者名に当該語を含む作品のみ表示（あらすじ・タグのみマッチは除外）|
| N-1 | 作者名で検索 → 該当作者の全作品が出ること（作者名がタイトルに無い作品も拾う）|
| N-1 | 検索結果数は現状より減る方向（広範マッチによる無関係結果が消える）|
| N-2 | N=5 まで読了済みの状態で N=10 のリーダー画面を末尾までスクロール → 1..10 が既読、11..max が未読化される |
| N-2 | N=10 既読状態で N=3 を読了 → 1..3 が既読、4..max が未読化される（巻き戻し挙動）|
| N-2 | `read_at` は元々既読だった話は元の日時を保持、新規既読化された話は now |
| N-3 | ジャンル「すべて」選択 → なろう全ジャンルの週間ポイント TOP 30 が表示される（旧: 0 件）|
| N-3 | ジャンル「恋愛」選択 → 恋愛系（biggenre=1: 異世界恋愛・現実世界恋愛 等）のみ表示される（旧: 不定または 0 件）|
| N-3 | API URL を Logcat で確認 → `&biggenre=1` パラメータが付与されている |
| N-4 | カクヨムのランキング「総合」「日間」 → 30 件のランキングが順位 1〜30 の DOM 順で表示される |
| N-4 | カクヨムのジャンル「異世界ファンタジー」 → ジャンル特化のランキング 30 件、ページ `<title>` のジャンル名と一致 |
| N-4 | 各カードに作者名・話数・完結フラグが表示されること |
| N-4 | 重複作品が出ないこと（旧コードでは HashSet で排除していた重複が、新コードでは `widget-work-rank` フィルタで原理的に発生しない）|
| N-4 | 広告/おすすめ枠の作品が混入しないこと（事前調査で 5 件分混在を確認した枠）|
| B-4 | SettingsPage で「スクロール終端で自動的に既読にする」トグルが表示され、デフォルト ON で既存挙動と一致 |
| B-4 | トグル ON: 短編をリーダーで開く → スクロールせずとも数秒以内に既読化が走り、戻ると目次でグレーアウト |
| B-4 | トグル OFF: 同じ操作で既読化が走らないこと（目次表示が未読のまま）。ReaderPage フッターの「既読」ボタンを手動タップ → 既読化される |
| B-4 | トグル OFF + 過去話 N=3（既読 N=10 の状態）を開いて自動経路を観察 → 4..10 の `read_at` が NULL に戻らない（巻き戻し抑止が機能） |
| B-4 | トグル OFF + N=3 を開いて手動「既読」ボタンタップ → 4..10 が `read_at = NULL` に戻る（手動経路は常に巻き戻し有効） |
| B-4 | 縦書き WebView で `lanobe://read-end` 受信時もトグル OFF なら no-op（ScrollView と WebView の両経路で同じ挙動） |
| B-4 | 設定変更 → リーダーへ戻る（目次経由）→ `OnAppearing` の `ReloadSettingsAsync` で最新値が反映され、別エピソード開いた時から新挙動が適用 |
| B-4 (v7) | **自動 OFF + ヘッダ/フッタ非表示トグル**: フッタ全体が消えても、左下に「既読」ボタン単独 Overlay が表示され、タップで既読化できる |
| B-4 (v7) | **自動 OFF + フッタ表示中**: 通常 4 列フッターの「既読」ボタンが見え、左下 Overlay は二重表示されない（Overlay は IsFooterVisible=false 時のみ） |
| B-4 (v7) | **自動 ON（既定）+ フッタ非表示**: 左下 Overlay は出ない（自動経路があるため救済 UI 不要） |

---

---

# PR-7: 検索系バグ修正（L-3 + N1-N4）

L-3（dead parameter 削除）と N-1（検索精度改善）はどちらも `NarouApiService.SearchAsync` を触るため、本 PR で同時に処理する。**1 コミットに統合**して、個別 revert 事故を構造的に防止する。

**コミット構成:**
1. `fix: narrow Narou search scope (remove dead searchTarget, add title/wname flags)` — Step 1+2 を 1 コミットに統合（下記参照）
2. `feat: read-position based mark-as-read with auto-toggle and manual button` — N-2 + B-4 を**1 コミットに統合**（SQL 仕様変更 + 設定キー + UI + 自動経路ガードを一式で実装）
3. `fix: correct biggenre parameter and handle "all" selection in Narou genre browse` — N-3 修正
4. `fix: scrape Kakuyomu rankings using widget-work card selectors` — N-4 修正

L-3 と N-1 は同一ファイル（主に `NarouApiService.SearchAsync` および呼び出し側のシグネチャ追従）を触るため、コミットを分けるメリットよりも個別 cherry-pick / revert 時のビルド破綻リスクの方が大きい。1 コミットに統合し、PR description で L-3 と N-1 のそれぞれの背景を見出し付き（"## L-3: dead parameter removal" / "## N-1: search scope narrowing"）に記述してレビュアビリティを担保する。

**N-2 と B-4 を 1 コミットに統合する理由（v6 で変更）:** B-4 の手動経路は N-2 の `SetReadStateUpToAsync` を呼び、自動経路は同じものを設定 (`auto_mark_read_enabled`) でガードする。両者は `ReaderViewModel.MarkAsReadAsync` の同一箇所に対する変更であり、N-2 を先に入れて B-4 で再リファクタすると、コミット 2 で一旦書いた `MarkAsReadAsync` をコミット 3 でまた書き換える流れになりレビュー時に差分が混乱する。1 コミットで `ApplyMarkAsReadAsync` (private ヘルパー) + `MarkAsReadAsync` (手動) + `MarkAsReadFromAutoAsync` (自動・ガード付き) の 3 メソッド構造を最終形として投入することで、レビュアーは「N-2 の SQL 仕様 + B-4 の経路分離」を 1 つの差分として把握できる。PR description で N-2 / B-4 のそれぞれの背景を見出し付きで記述する。

---

## Step 1: L-3 — `INovelService.SearchAsync(searchTarget)` パラメータ削除（統合コミットの前半）

**問題:** [SearchViewModel.cs:182](../ViewModels/SearchViewModel.cs#L182) で `var searchTarget = "Both";` 固定。なろう側に分岐コード ([NarouApiService.cs:35-40](../Services/Narou/NarouApiService.cs#L35-L40)) はあるがカクヨム側は未使用 ([KakuyomuApiService.cs:32-39](../Services/Kakuyomu/KakuyomuApiService.cs#L32-L39))。dead code。

**修正方針:** UI を増やすコストよりも dead parameter を消す方が pragmatic。インターフェースから引数を削除し、なろうの分岐ロジックも削除（常に `word` 検索）。これにより N-1 で URL 生成のみを触ればよい状態になる。

**修正 1 — `INovelService.cs:8`:**

```csharp
Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default);
```

**修正 2 — `NarouApiService.cs:33-50` の SearchAsync:**

```csharp
public async Task<List<SearchResult>> SearchAsync(string keyword, CancellationToken ct = default)
{
    var encoded = Uri.EscapeDataString(keyword);
    var url = $"{API_BASE}?out=json&lim=20&word={encoded}";

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(10));

    var response = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
    return ParseNovelApiJson(response);
}
```

**修正 3 — `KakuyomuApiService.cs:32` の SearchAsync シグネチャから `searchTarget` 削除**（参照されていないので body は変更不要）。

**修正 4 — `SearchViewModel.cs:179-187`:**

```csharp
[RelayCommand(CanExecute = nameof(CanSearch))]
private Task SearchAsync()
{
    return ExecuteSiteQueryAsync(
        "Search",
        SearchNarou    ? ct => _narou.SearchAsync(SearchKeyword, ct)    : null,
        SearchKakuyomu ? ct => _kakuyomu.SearchAsync(SearchKeyword, ct) : null);
}
```

**注意点:** `SearchTarget` の選択 UI を将来追加するなら別 PR で interface に戻す。今は使っていないものを削るのが最善。要件書 §3.2 F-001 の searchTarget 入力定義は PR-5（L-4）で削除する。

---

## Step 2: N-1 — 検索結果に無関係な小説が混じる（統合コミットの後半）

**前提:** Step 1 と同一コミット内で適用する。Step 1 で `NarouApiService.SearchAsync(string keyword, CancellationToken ct = default)` のシグネチャになっていることを想定する。本項は **URL 生成 1 行のみの変更**。

**問題:** [NarouApiService.cs:43](../Services/Narou/NarouApiService.cs#L43) は **`{wordParam}={encoded}` の変数経由**で URL を組み立てているが、[SearchViewModel.cs:182](../ViewModels/SearchViewModel.cs#L182) で `searchTarget="Both"` がハードコードされているため、wordParam switch ([NarouApiService.cs:35-40](../Services/Narou/NarouApiService.cs#L35-L40)) は常に `"word"` 分岐に落ちる → **runtime 上は実質 `word=keyword` のみで動作**。なろう公式 API の `word` パラメータはフラグ未指定時に「**タイトル + あらすじ + キーワード（タグ） + 作者名**」を全文検索するため、対象語があらすじ・タグに含まれる無関係な作品まで大量にヒットする（例: 「異世界転生」と入力すると本筋と関係なくあらすじやタグに語が出てくる作品まで全部出てくる）。

なお Step 1 (L-3) で wordParam switch が削除されると、line 43 はリテラル `word=` の URL になる。本 N-1 修正はその状態に対して `&title=1&wname=1` フラグを追加する形で適用される。

Step 1 (L-3) の修正後の URL は `$"{API_BASE}?out=json&lim=20&word={encoded}"`（searchTarget 分岐削除済み）。これに **タイトル + 作者名フラグ** を追加して検索範囲を絞る。

カクヨム側 ([KakuyomuApiService.cs:38](../Services/Kakuyomu/KakuyomuApiService.cs#L38)) は `kakuyomu.jp/search?q=...` でカクヨムの検索エンジンに丸投げしており、関連度はサイト側のロジック任せ。これは比較的妥当なため据え置く。

**修正方針:** なろう側を **タイトル + 作者名検索 (`title=1` + `wname=1`)** に切り替える。なろう API のドキュメント (https://dev.syosetu.com/man/api/) によると、`title` `ex`（あらすじ）`keyword`（タグ）`wname`（作者名）の各フラグは独立に有効化でき、複数指定すると **OR 検索**（指定したターゲットのいずれかにマッチ）になる。

**修正 — `NarouApiService.cs` の `SearchAsync` URL のみ変更（Step 1 後の状態に対する diff）:**

```diff
- var url = $"{API_BASE}?out=json&lim=20&word={encoded}";
+ // title=1 + wname=1 で「タイトル or 作者名」にマッチする作品のみ取得
+ // （word 単独だとあらすじ・キーワード・作者名まで全文検索され、無関係な作品が大量にヒットする）
+ var url = $"{API_BASE}?out=json&lim=20&word={encoded}&title=1&wname=1";
```

**シグネチャ・その他の本体は変更しない**（Step 1 で確定済み）。

**注意点:**
- なろう API のフラグ仕様（公式 https://dev.syosetu.com/man/api/ 「抽出対象の範囲」セクションで 2026-04-30 確認）: `title=1` =「タイトルを `word`/`notword` の抽出対象にする」/ `wname=1` =「作者名を抽出対象にする」。`title`/`ex`/`keyword`/`wname` の **4 項目すべて未指定の場合のみ全項目が抽出対象**であり、1 つでも指定があれば「指定された範囲のみ」が抽出対象になる（複数指定は OR 相当の集合追加）。本修正で `title=1&wname=1` を指定すれば「タイトル or 作者名」マッチに絞り込まれ、`ex`（あらすじ）・`keyword`（タグ）は対象から外れる。
- 1 コミット統合のため、L-3 と N-1 の片方だけ revert される事故は構造的に発生しない。
- 要件書 §3.2 F-001 の searchTarget 入力定義は PR-5（L-4）で削除し、「タイトル + 作者名検索（固定）」に書き換える。

**v7 で追加: 要件書同期マージブロックルール**

PR-7 (L-3 + N-1) は「`searchTarget="Title"`/`"Author"` の単独検索」が消える仕様変更を伴うが、要件書 §3.2 F-001 への反映は PR-5 (L-4) 任せ。両 PR の同期が遅れると**マージ済みコードと要件書記述が食い違う**期間が生まれる。これを防ぐため、PR-7 のマージ条件として以下のいずれかを満たすことを必須とする:

- (a) **PR-5 で要件書 §3.2 F-001 修正がコミット済み**（PR-5 全体マージ前でも、F-001 部分だけ先行コミットしておけば OK。コミット粒度は PR-5 セクション内で示している 4 コミット構成のうちの 1 つ目「`docs: requirements に F-009..F-014 を追加`」と並列）
- (b) **PR-7 内で要件書 §3.2 F-001 を同 PR の修正対象に含める**（PR-7 のスコープに `requirements_lanovereader.md` の §3.2 F-001 のみ追加。L-4 の他項目は引き続き PR-5 で扱う）

レビュアーは PR-7 マージ前に上記 (a)/(b) のどちらが選択されているか PR description で確認し、満たされていなければ approve 保留。CLAUDE.md の「ファイルが base にあるなら base からブランチ → PR」ルールに従い、`requirements_lanovereader.md` は `app-novelviewer` に既存のため (b) を選んでも CLAUDE.md ルール違反にはならない。

実用面では (b) を推奨する（レビュー単位で仕様と実装が必ず揃うため）。Doc-only PR である PR-5 のマージタイミングが PR-7 後ろ倒しになっても、PR-7 マージ時点で要件書整合が保たれる。

---

## N-2: 既読化を「読了点までの一括既読化」に変更

**問題（仕様変更）:** [ReaderViewModel.cs:269-287](../ViewModels/ReaderViewModel.cs#L269-L287) の `MarkAsReadAsync` は当該 episode 1 件のみ既読化。ユーザが N 話まで連続して読んだ場合、1..N-1 が未読のまま残ると「過去話を読み返したと誤検知」してリストの未読カウントがいつまでも減らない。

ユーザの直感的モデルは「**現在読んでいる位置 = 読了点**」で、N 話を読了したら 1..N は既読、N+1 以降は未読扱い（後で N より前に戻った場合、戻った位置以降を未読に巻き戻す）。

**「巻き戻し挙動」の仕様根拠（2026-04-30 ユーザ確認済み）:** 既読 N=10 状態でユーザが N=3 を再読 → 4..10 が未読化される挙動について、ユーザに確認したところ「**読み直した場合はもう一度それ以降を最後まで読みたい**」という要望があり、これがそのまま仕様。代替案（単調増加既読化 = N+1 以降は触らない）は不採用。再読時は「過去話を改めて読み進める起点」として扱われる。
**How to apply:** SQL の `WHEN episode_no > ? THEN NULL` で N+1 以降の `read_at` を NULL に戻すロジックは仕様通りであり、変更や条件追加は不要。実装レビューで「巻き戻しは不具合では？」と指摘された場合は本セクションを根拠に提示する。

**発火条件（2026-04-30 ユーザ確認済み）:** `MarkAsReadAsync` の発火は「手動タップ（既読化ボタン）」「自動既読化（リーダー表示時）」「スクロール終端到達」のいずれも許可する。発火条件の絞り込みは行わない。

**UX リスク（要件書 §6.3 への明記必須・PR-5 / L-4 で対応）:**
- 過去話の誤タップ（カード一覧から N=10 既読のところを N=3 に戻ってしまう等）でも、上記巻き戻しが発火し 4..10 の `read_at` が **NULL に戻されて復元不可能**となる。
- これは仕様承認済みの挙動だが、ユーザが「読み直し意図ではなく単に確認したかった」場合の損失は救済できない。
- 要件書 §6.3 例外・エラーハンドリングに「読了点を巻き戻したエピソードの `read_at` は復元不可」を明記する。アンドゥ機構やゴミ箱は本 PR では実装しない（要件範囲外）。

**v9 で追加: デフォルト挙動の攻撃性に関する注記（リリース後の再評価ポイント）:**

既定 `auto_mark_read_enabled=1` + 巻き戻し挙動は、**設定変更しないユーザにとっては「過去話を確認しただけで read_at が NULL に戻り復元不可」という攻撃的な挙動**になり得る。具体的には:
- 多数のユーザは設定画面を開かないため、デフォルト ON のまま運用される。
- 自動既読化は OnScrolled / `lanobe://read-end` 受信のいずれかで発火し、短編作品だと画面遷移直後に終端到達して即発火する。
- N=10 まで読了済みのユーザが目次から N=3 を「ちょっと確認」目的でタップしただけで、4..10 の `read_at` が即 NULL 復元不可となるケースが発生し得る。

本プランでは設計変更しない（ユーザ承認済みの「読み直したらそれ以降を最後まで読み直したい」仕様のため）が、リリース後にユーザフィードバック・サポート問い合わせ・アプリレビュー等で「勝手に既読が消えた」「過去話を見ただけなのに既読が壊れた」系の声が観測された場合、後続 PR で以下のいずれかを検討する余地を残す:
- (a) **既定値を `0`（OFF）に変更**: 大多数のユーザは手動「既読」ボタンで明示既読化することになる。誤タップで巻き戻しが起きない代わりに、既読化を意識的に行う必要が出る UX 変化。
- (b) **巻き戻し抑止オプションを追加**: 新キー `auto_mark_read_rollback_enabled` を追加し、auto ON でも巻き戻しだけ OFF にできる設定を提供。設計が複雑化する代わりに既存ユーザの設定移行が不要。
- (c) **巻き戻し対象に時間ガード**: 「直近 N 日以内に既読化された話の `read_at` は巻き戻さない」等の保護ロジック。実装複雑度高。

判断は実運用データを取得してから行うため、本 PR ではあくまで仕様通りに実装する。リリースノートまたは初回起動時の説明で「既読化挙動」を明示することで、ユーザの予期と実装のギャップを最小化する選択肢もあり得る（PR-5 / L-4 で要件書に挙動を明記済みのため、ヘルプ画面追加は別 PR）。

**自動既読化のオプトアウト経路（B-4 対応・設定キー + UI + ガード一式を本 PR で実装）:**

現コードの `MarkAsReadCommand` の発火経路を grep で確認した結果、**全て自動経路** だった:

- [ReaderPage.xaml.cs:24-35](../Views/ReaderPage.xaml.cs#L24-L35) `OnScrolled`: 横書きのスクロール終端到達時
- [ReaderPage.xaml.cs:37-58](../Views/ReaderPage.xaml.cs#L37-L58) `OnWebViewNavigating`: 縦書き WebView の `lanobe://read-end` ナビゲーション受信時

[ReaderPage.xaml](../Views/ReaderPage.xaml) のフッター (line 82-92) に手動「既読」ボタンは無い。短編作品ではページ表示直後に終端が見えており、画面遷移直後に OnScrolled が走り MarkAsRead が発火するケースもあり得る。これと N-2 の巻き戻し挙動が組み合わさると「過去話を確認しただけで巻き戻し」が発生し得るため、**設定キー + 手動既読ボタン UI + 自動経路ガードを一式で本 PR に追加**する。

設計判断:
- 設定 ON（既定）: 自動既読化 + 巻き戻し（現仕様維持）。手動ボタンも有効。
- 設定 OFF: 自動経路は no-op、手動ボタンのみで既読化 + 巻き戻し可能。誤タップで読み返しただけのケースで巻き戻しが起きない。
- ViewModel 側に「自動」「手動」の 2 コマンドを分離し、自動コマンドだけが設定値で gate される設計（OnScrolled / WebView read-end の側でフラグ判定をするより、ViewModel に責務を集約する方がテスタブル）。

### 修正 a — `SettingsKeys.cs` に定数追加

```csharp
public const string AUTO_MARK_READ_ENABLED = "auto_mark_read_enabled";
public const int DEFAULT_AUTO_MARK_READ_ENABLED = 1;  // 1=ON（現状仕様維持）
```

### 修正 b — `DatabaseService.SeedSettingsAsync` の defaults 辞書に追加

PR-4 L-1 の段階で defaults 辞書は `SettingsKeys.*` 定数経由の形式に書き換わっているため、本 PR-7 では同形式で 1 行追加する:

```csharp
[SettingsKeys.AUTO_MARK_READ_ENABLED] = SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED.ToString(),
```

**追加位置:** L-1 で書き直された defaults 辞書の `[SettingsKeys.LAST_SCHEDULED_HOURS]` 行の直後（v6 で明示化）。これにより PR-4 と PR-7 の defaults 辞書編集が同一箇所で重ならず、機械的 rebase で解決可能。

### 修正 c — `ReaderViewModel.cs` にコマンド分離 + `[ObservableProperty]` フラグ追加（v8 で c2 と統合）

`AutoMarkReadEnabled` プロパティを `[ObservableProperty]` で追加し、`LoadSettingsAsync` で設定を読み込む。**XAML 側で `IsManualReadButtonOverlayVisible` 算出プロパティへの変更通知を取るため、最初から `[ObservableProperty]` で実装する**（旧 v7 では「private bool で導入 → c2 で `[ObservableProperty]` 格上げ」の 2 段階構造だったが、v8 で 1 段階に統合）。`MarkAsReadAsync` は手動コマンドとして残し（[RelayCommand]）、自動経路用に新規 `MarkAsReadFromAutoAsync` を追加。共通の更新処理は private ヘルパー `ApplyMarkAsReadAsync` に抽出する。

```csharp
// フィールド宣言: 既存の [ObservableProperty] 群（line 42-81 付近）の末尾に追加。
// XAML から Binding するため public プロパティが必要 + 算出プロパティへの変更通知が必要なので
// [ObservableProperty] で生成する。private bool で導入してから格上げするのは中間状態が冗長なので避ける。
[ObservableProperty]
private bool _autoMarkReadEnabled = true;   // 既定 ON、LoadSettingsAsync で上書き

// 算出プロパティ: 「自動 OFF + フッタ非表示」の AND 条件で表示する既読ボタン単独 Overlay 用。
// AutoMarkReadEnabled / IsFooterVisible のいずれかが変わったら通知する。
public bool IsManualReadButtonOverlayVisible
    => !AutoMarkReadEnabled && !IsFooterVisible;

partial void OnAutoMarkReadEnabledChanged(bool value)
    => OnPropertyChanged(nameof(IsManualReadButtonOverlayVisible));

// 既存の IsFooterVisible は [ObservableProperty]（[ReaderViewModel.cs:51-52](../ViewModels/ReaderViewModel.cs#L51-L52) で確認済み、
// ToggleHeaderFooter は [ReaderViewModel.cs:253-256](../ViewModels/ReaderViewModel.cs#L253-L256)）なので
// partial メソッドで通知をフックできる:
partial void OnIsFooterVisibleChanged(bool value)
    => OnPropertyChanged(nameof(IsManualReadButtonOverlayVisible));

// LoadSettingsAsync ([ReaderViewModel.cs:124-134](../ViewModels/ReaderViewModel.cs#L124-L134)) の末尾に追加:
AutoMarkReadEnabled = (await _settingsRepo.GetIntValueAsync(
    SettingsKeys.AUTO_MARK_READ_ENABLED,
    SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED)) == 1;

// 既存の MarkAsReadAsync (line 269-287) を以下の 3 メソッドに置き換え:

[RelayCommand]
private Task MarkAsReadAsync() => ApplyMarkAsReadAsync();   // 手動経路（フッターボタン）。常に発火。

[RelayCommand]
private Task MarkAsReadFromAutoAsync()
{
    // 自動経路（OnScrolled / WebView read-end）。設定 OFF なら no-op。
    if (!AutoMarkReadEnabled) return Task.CompletedTask;
    return ApplyMarkAsReadAsync();
}

private async Task ApplyMarkAsReadAsync()
{
    if (_episode is null) return;
    // N-2 の仕様に沿い、既読でも N+1 以降の未読化を走らせるため IsRead チェックは外す。
    await _episodeRepo.SetReadStateUpToAsync(_novelDbId, _episode.EpisodeNo);
    _episode.IsRead = true;

    var allRead = await _episodeRepo.AreAllReadAsync(_novelDbId);
    if (allRead)
    {
        var novel = await _novelRepo.GetByIdAsync(_novelDbId);
        if (novel is not null && novel.HasUnconfirmedUpdate)
        {
            novel.HasUnconfirmedUpdate = false;
            await _novelRepo.UpdateAsync(novel);
        }
    }
}
```

`ReloadSettingsAsync()`（[ReaderViewModel.cs:136](../ViewModels/ReaderViewModel.cs#L136)）は `LoadSettingsAsync()` を呼ぶだけなので、SettingsPage で設定変更後にリーダーへ戻ると `OnAppearing` 経由で最新値が反映される（[ReaderPage.xaml.cs:15-22](../Views/ReaderPage.xaml.cs#L15-L22)）。追加の通知配線は不要。

**注意点:**
- 本オーバーレイは「自動 OFF」ユーザのみへの救済 UI のため、既定 ON のままのユーザには一切表示されず UX 影響ゼロ。
- `IsFooterVisible` が手書きプロパティに変わっていた場合（要件 P-7 で実コード確認済み = 現状は `[ObservableProperty]`）、`OnIsFooterVisibleChanged` partial メソッドは生成されない。その場合は `ToggleHeaderFooter` 内で `OnPropertyChanged(nameof(IsManualReadButtonOverlayVisible))` を直接呼ぶ形にフォールバック。
- `[RelayCommand]` 属性で生成されるコマンド名は `MarkAsReadCommand` / `MarkAsReadFromAutoCommand`（メソッド名末尾の `Async` を除く CommunityToolkit.Mvvm 規約）。XAML / ReaderPage.xaml.cs での参照名はこれに従う。

### 修正 d — `ReaderPage.xaml.cs` の自動経路を新コマンドに切替

```csharp
// OnScrolled (line 24-35):
if (scrollView.ScrollY + scrollView.Height >= scrollView.ContentSize.Height - 10)
{
    if (BindingContext is ReaderViewModel vm)
    {
        await vm.MarkAsReadFromAutoCommand.ExecuteAsync(null);   // ← MarkAsReadCommand から変更
    }
}

// OnWebViewNavigating (line 44-47):
if (e.Url.Contains("read-end", StringComparison.OrdinalIgnoreCase))
{
    await vm.MarkAsReadFromAutoCommand.ExecuteAsync(null);   // ← 同上
}
```

### 修正 e — `ReaderPage.xaml` のフッターに手動「既読」ボタン追加（v8 で 修正 c と整合）

現フッター ([ReaderPage.xaml:83-92](../Views/ReaderPage.xaml#L83-L92)) は `ColumnDefinitions="*,*,*"` で「目次/前へ/次へ」の 3 列。これを **4 列** に変更し、左から「目次/既読/前へ/次へ」の順とする（既読は読書中に押す可能性が高いので目次の隣に配置）。

**v9 重要注記（dead code 前提の先回り対応）:** 後述の `IsManualReadButtonOverlayVisible` Overlay は **`IsFooterVisible=false` 経路への救済 UI** だが、v9 で実コード調査した結果、**現状 `ToggleHeaderFooter` コマンドは XAML/コードビハインドのいずれからも binding されておらず（[ReaderViewModel.cs:253](../ViewModels/ReaderViewModel.cs#L253) の定義のみ）、`IsFooterVisible=false` 経路は到達不能**。事前確認 P-8 参照。

つまり**現状は救済 Overlay が表示されることがない**。本 Overlay は以下を目的として先回りで投入する:
- 将来 `ToggleHeaderFooter` binding（タップジェスチャ等）が導入された際に、自動 OFF ユーザの既読化経路が失われるのを防ぐ保険
- 「自動 OFF + フッタ非表示」の詰み UX を構造的に塞ぐ設計を、binding 導入時の追加修正なしに完成させる

**もし将来 `ToggleHeaderFooter` binding が削除されたまま固定方針となる場合**は、本 Overlay も同時に削除し、4 列フッタの「既読」ボタンのみを残す簡素化を行う（dead code を増やさない方針）。実装着手時に再度 P-8 の grep で binding 追加状況を確認し、追加されていれば「実到達可能シナリオへの対応」として救済 Overlay の正当性が強化される。

**v7 仕様変更（v8 で実装方針を 修正 c に集約）:** 「自動既読化 OFF + ToggleHeaderFooter でフッタ非表示」の組み合わせで既読化経路が完全に失われる UX 詰みを回避するため、**既読ボタン単独の Visibility を独立制御**する。`ReaderViewModel.IsManualReadButtonOverlayVisible` 算出プロパティ（修正 c で実装）の AND 条件で、フッタ全体の `IsFooterVisible` が false でも、自動 OFF のときは既読ボタンだけ表示し続ける。

フッタ Grid 自体は `IsFooterVisible=false` で消えるが、その上に**重ねて配置**する形で「自動 OFF 時の既読ボタン専用 Overlay」を追加するのが最もシンプル（Grid.Row を 2 に揃え、`HorizontalOptions="Start"` で左端に小さく出す）。これによりフッタ非表示 = 没入読書状態でも、自動 OFF ユーザは既読ボタンを失わない（binding 導入後）。

```xml
<!-- 通常フッター（既存の 3 列を 4 列に拡張、IsFooterVisible で制御） -->
<Grid Grid.Row="2" Padding="8" BackgroundColor="{StaticResource Overlay}"
      ColumnDefinitions="*,*,*,*"
      IsVisible="{Binding IsFooterVisible}">
    <Button Grid.Column="0" Text="目次" Command="{Binding NavigateToTocCommand}"
            Style="{StaticResource OverlayButton}" />
    <Button Grid.Column="1" Text="既読" Command="{Binding MarkAsReadCommand}"
            Style="{StaticResource OverlayButton}" />
    <Button Grid.Column="2" Text="◀ 前へ" Command="{Binding PrevEpisodeCommand}"
            Style="{StaticResource OverlayButton}" />
    <Button Grid.Column="3" Text="次へ ▶" Command="{Binding NextEpisodeCommand}"
            Style="{StaticResource OverlayButton}" />
</Grid>

<!-- 自動 OFF 時の既読ボタン単独 Overlay（フッタ非表示でも残す。v7 で追加） -->
<Button Grid.Row="2" Text="既読" Command="{Binding MarkAsReadCommand}"
        Style="{StaticResource OverlayButton}"
        HorizontalOptions="Start" VerticalOptions="End"
        Margin="8,0,0,8" Padding="12,6"
        IsVisible="{Binding IsManualReadButtonOverlayVisible}" />
```

注意点:
- 4 列分割により各ボタン幅が縮むが、`OverlayButton` スタイルが文字数に応じて伸縮する想定であれば問題なし。狭すぎる場合は `Text="既読"` のままでも 2 文字なので極端には潰れない。
- 単独 Overlay の `IsManualReadButtonOverlayVisible` は「**自動 OFF かつ フッタ非表示**」の AND 条件。両方真のときだけ表示（通常フッターと二重表示しない）。算出プロパティは `修正 c2` で実装。
- 単独 Overlay は `Padding` で小さく作り、本文を覆う面積を最小化する。読書 UX への侵襲性を抑えるため、文字色/背景は `OverlayButton` Style を継承（半透明前提）。
- **v10 注記（PR-6 マージ後の Grid.Row 値）**: 上記サンプルの Overlay Button の `Grid.Row="2"` は **PR-6 (L-9) マージ前の base** を前提にした値。PR-6 マージ後はルート Grid の RowDefinitions が `"Auto,Auto,*,Auto"` に拡張され、本文/フッターは Row +1 されるため、Overlay Button も `Grid.Row="3"` にリナンバリングが必要。リナンバリング作業自体は PR-6 (L-9) 修正 4-4 のスコープ内に **v10 で明示的に追加済み**（リナンバリング 6 個目）。PR-7 単体マージ時点では `Grid.Row="2"` のままで動作するため、本サンプルはそのままで OK（PR-6 着手時に対応）。

### 修正 f — `SettingsViewModel.cs` にプロパティ追加

[SettingsViewModel.cs](../ViewModels/SettingsViewModel.cs) の既存パターンに整合させる:
- `[ObservableProperty]` で bool プロパティを宣言（line 41-45 の `_verticalWriting` / `_prefetchEnabled` と同じパターン）
- `InitializeAsync`（line 50-69）に `await ... == 1` 形式の代入を 1 行追加
- `OnAutoMarkReadEnabledChanged` から `DebounceSave`（line 74-94）を呼ぶ（`OnPrefetchEnabledChanged` (line 108) と同形）

```csharp
// フィールド宣言: line 45 の _prefetchEnabled の直後に追加
[ObservableProperty]
private bool _autoMarkReadEnabled = true;   // 既定 ON、InitializeAsync で上書き

// InitializeAsync (line 62 の PrefetchEnabled 行の直後) に追加:
AutoMarkReadEnabled = await _settingsRepo.GetIntValueAsync(
    SettingsKeys.AUTO_MARK_READ_ENABLED,
    SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED) == 1;

// 変更ハンドラ: line 108 の OnPrefetchEnabledChanged の直後に追加
partial void OnAutoMarkReadEnabledChanged(bool value)
    => DebounceSave(SettingsKeys.AUTO_MARK_READ_ENABLED, value ? "1" : "0");
```

`DebounceSave` は内部で `_isInitializing` ガード付き（line 76）なので、`InitializeAsync` 中の代入が誤って save を発火することはない。既存パターンと完全整合。

### 修正 g — `SettingsPage.xaml` にトグル UI 追加

「読書設定」セクション末尾（`vertical_writing` トグルの直下、[SettingsPage.xaml:96-99](../Views/SettingsPage.xaml#L96-L99) の HorizontalStackLayout の直下）に追加:

```xml
<HorizontalStackLayout Spacing="8" Margin="0,8,0,0">
    <Switch IsToggled="{Binding AutoMarkReadEnabled}" />
    <Label Text="スクロール終端で自動的に既読にする" Style="{StaticResource BodyLabel}" VerticalOptions="Center" />
</HorizontalStackLayout>
<Label Style="{StaticResource SmallMetaLabel}"
       Text="OFF にすると手動の「既読」ボタンのみで既読化されます" />
```

### 修正 h — 要件書追記（PR-5 / L-4 のスコープ）

- §3.2 F-007 設定キー表に `auto_mark_read_enabled` (int, 既定=1, 即時反映) を追加
- §3.2 F-006 既読仕様セクションを以下に書き換え:
  - 「既読化は手動（フッタの『既読』ボタン）と自動（横書きスクロール終端 / 縦書き WebView の read-end ナビゲーション）の 2 経路。いずれも `SetReadStateUpToAsync` で 1..N=既読、N+1..max=未読化（巻き戻し）」
  - 「`auto_mark_read_enabled=0` の場合、自動経路は no-op。手動経路は常に有効」
- §6.3 例外・エラーハンドリングに「読了点を巻き戻したエピソードの `read_at` は復元不可。誤タップ防止のため `auto_mark_read_enabled` を OFF にすると、自動経路の発火を抑止できる」を明記
- §3.1 機能一覧テーブルに F-006 の派生として「F-006a 自動既読化 ON/OFF 設定」を追加

### スコープと PR 配置

- **本 PR (PR-7) のスコープに収める**。N-2 と密結合のため分離する意義が薄く、検証もまとめて実施できる。
- 想定差分: PR-7 全体で +6 ファイル / ~150 行 → +9 ファイル / ~210 行 程度に拡大。SettingsKeys / DatabaseService / ReaderViewModel / ReaderPage.xaml.cs / ReaderPage.xaml / SettingsViewModel / SettingsPage.xaml の 7 ファイル分の変更が増える。
- PR-6 (L-9) との衝突: `ReaderViewModel` と `SettingsViewModel` は L-9 で継承先が `ErrorAwareViewModel` に変わる予定。本 PR-7 は同 ViewModel のメソッド/プロパティ追加のみで継承宣言は触らないため、PR-6 着手時の rebase で機械的に解決可能。
- コミット構成: 本 B-4 関連は **N-2 と同一コミット 2** で投入する（v6 で変更）。両者は `ReaderViewModel.MarkAsReadAsync` の同一箇所への変更で、別コミットに分けると「コミット 2 で書いた MarkAsReadAsync をコミット 3 で再リファクタ」する流れになる。一体化することで、レビュアーは N-2 の SQL 仕様変更（`SetReadStateUpToAsync` 導入）と B-4 の経路分離（自動/手動コマンド分離 + 設定ガード + 手動 UI 追加）を**1 つの差分として把握**できる。コミットメッセージは `feat: read-position based mark-as-read with auto-toggle and manual button` とし、PR description で N-2 / B-4 のそれぞれの背景を見出し付きで記述する。

**修正方針:**
- `EpisodeRepository.MarkAsReadAsync(episodeId)` を**廃止**し、`SetReadStateUpToAsync(novelId, episodeNo)` に置き換え。
- 1 SQL で 1..N を既読化 + N+1..max を未読化（既読→未読化時は `read_at = NULL` に戻す）。
- `read_at` は新規既読化時のみ NOW を入れ、既存の `read_at` は保持（読了履歴の最初の日時を残す）。

**修正 1 — `EpisodeRepository.cs` （MarkAsReadAsync を置き換え）:**

実装前提として、現行の既読化フロー（旧 MarkAsReadAsync）は `read_at` に NULL または ISO-8601 文字列のみを書き込み、空文字列は使われていないことを `EpisodeRepository` 全体の grep で事前確認すること。確認できれば SQL の `read_at = ''` 比較は不要（dead branch）になり、より単純な NULL 判定だけで済む。

**事前確認結果（v8 で実コード調査済み）:** SQLite-net (sqlite-net-pcl) の `_db.ExecuteAsync(sql, params...)` での positional placeholder (`?`) 使用先例を `_Apps/Services/Database/*.cs` 全体で集計した結果、**EpisodeRepository での先例は最大 3 個**（`is_read=?, read_at=?, id=?` 等）で、4 個以上の先例は 0 件。よって本修正は **2 文分割案を本実装**として採用し、各 UPDATE を placeholder 3 個以下に収める形で書く。

なお `read_at` への書き込み経路は `EpisodeRepository.MarkAsReadAsync` (line 125-132) のみで、NULL または ISO-8601 文字列のみが書き込まれており空文字列経路は存在しない（v8 で grep 確認済み）。よって SQL の比較は `read_at IS NULL` のみで足り、`read_at = ''` 併用は不要。

事前確認用 grep コマンド（実装着手時に再確認推奨）:
```bash
grep -nE "ExecuteAsync\(.*\?.*\?.*\?.*\?" _Apps/Services/Database/*.cs
grep -n "read_at" _Apps/Services/Database/EpisodeRepository.cs
```

```csharp
// 旧 MarkAsReadAsync は廃止。呼び出し元（ReaderViewModel のみ）も差し替える。
// 2 文分割案: 既読側 (3 placeholder) + 未読側 (2 placeholder) を RunInTransactionAsync で atomicity 保証。
public async Task SetReadStateUpToAsync(int novelId, int episodeNo)
{
    await EnsureAsync().ConfigureAwait(false);
    var now = DateTime.UtcNow.ToString("o");

    await _db.RunInTransactionAsync(conn =>
    {
        // 1..N: is_read=1。既存 read_at が NULL なら now を入れる、既存値があれば COALESCE で保持。
        conn.Execute(
            "UPDATE episodes SET is_read = 1, read_at = COALESCE(read_at, ?) " +
            "WHERE novel_id = ? AND episode_no <= ?",
            now, novelId, episodeNo);

        // N+1..max: is_read=0、read_at = NULL に巻き戻し。
        conn.Execute(
            "UPDATE episodes SET is_read = 0, read_at = NULL " +
            "WHERE novel_id = ? AND episode_no > ?",
            novelId, episodeNo);
    }).ConfigureAwait(false);
}
```

**v8 注記**: `RunInTransactionAsync` は SQLiteAsyncConnection の同期トランザクション lambda（[NovelRepository.cs](../Services/Database/NovelRepository.cs) で同パターン使用、参照 P-3/P-4）。lambda 内では同期 API (`conn.Execute`) を使う。これは `_db.ExecuteAsync` を 2 回続けて呼ぶ単純連結よりも安全（途中で例外が出た場合に rollback される）。

**補足 — 1 文 CASE 案（見送り）:** 5 placeholder で 1 つの UPDATE を投げる旧 v7 案も技術的には動作する想定（sqlite-net-pcl は `params object[]` で受ける）。ただし、(a) 既存コードベースに 4 個以上の先例が無く可読性で劣る、(b) CASE 文が複雑で SQL レビュー時の認知コストが高い、(c) 2 文分割の方が個別の UPDATE のパフォーマンスを EXPLAIN で確認しやすい、の 3 点で 2 文案を採用。1 文案は実装メモとしてのみ記録（v8 で本案からは廃案扱い）。

**修正 2 — `ReaderViewModel.cs:269-287` の `MarkAsReadAsync`:**

本 N-2 修正と B-4 修正は**統合コミット 2 で一体実装**するため、最終形は B-4 修正 c (上の「修正 c — `ReaderViewModel.cs` にコマンド分離 + フラグ追加」セクション) の 3 メソッド構造 (`MarkAsReadAsync` / `MarkAsReadFromAutoAsync` / `ApplyMarkAsReadAsync`) を採用する。N-2 のコア仕様（`SetReadStateUpToAsync` への置換 + `IsRead` チェック削除 + 全話既読時の `HasUnconfirmedUpdate=false`）は `ApplyMarkAsReadAsync` 内に集約されている。

旧プラン v5 までで提示していた「`MarkAsReadAsync` 1 メソッドだけを書き換える形」は、コミット 2 (N-2) → コミット 3 (B-4) の 2 段階リファクタを引き起こすため**廃止**。N-2 と B-4 は密結合（B-4 の手動経路は N-2 の SetReadStateUpToAsync を呼ぶ、自動経路は同じものを設定でガードする）であり、別コミットに分離するメリットよりレビュー時の差分混在リスクの方が大きい。

**注意点:**
- 重複呼び出し（同じ N で何度も発火）は SQL 側で is_read を再計算するだけ。コスト軽微。
- H-3 の `RefreshReadStatusAsync` と組み合わせると、戻ってきた目次画面で 1..N が即座に既読、N+1 以降が未読表示になる（H-3 が表示反映、N-2 が DB 反映で補完関係）。
- ユーザが過去話に戻って読んだ場合 → episode_no が小さくなる → N+1 以降を未読に戻すのは想定挙動。
- 要件書 §3.2 F-006 「is_read を 1 に UPDATE」を「読了点までの一括既読化と N+1 以降のリセット」に書き換える（L-4 / PR-5）。
- **大量話数時のパフォーマンス**: SQL は `WHERE novel_id = ?` で 1 小説の全エピソードを 1 トランザクションで UPDATE する。`(novel_id, episode_no)` のインデックス（v2 で UNIQUE 化）が効くため、10,000 話超の連載でも実用的な時間で完了する想定。**実機検証はユーザの登録済み最大話数の小説**で行えば十分（典型的には 500〜1,500 話）。5,000 話超を持つユーザは稀で、検証ハードルを上げて未検証マージにつながる方が問題。MarkAsRead 1 タップで体感遅延（>500ms）が出るかを判断基準とする。
- `MarkAsReadAsync` 呼び出し元は `ReaderViewModel` のみ（[grep 確認済み](../ViewModels/ReaderViewModel.cs#L274)）。他に広がらない。

---

## N-3: なろうジャンルブラウズが動作していない

**問題（バグ）:**

(a) 「**すべて**」選択時の 0 件問題: [SearchViewModel.cs:222-234](../ViewModels/SearchViewModel.cs#L222-L234) で
```csharp
int? narouBg = (... && int.TryParse(SelectedNarouBigGenre.Id, out var bg)) ? bg : null;
return ExecuteSiteQueryAsync(...,
    narouBg is int bgv ? ct => _narou.FetchByGenreAsync(bgv, ...) : null, ...);
```
[NarouGenres.cs:7](../Models/NarouGenres.cs#L7) の `new("", "すべて")` → Id="" → `int.TryParse` 失敗 → `narouBg = null` → なろう fetch 自体が呼ばれない → 結果 0 件。

(b) 「**他のジャンル**」（恋愛=1, ファンタジー=2 等）選択時のジャンル絞り込み失敗: [NarouApiService.cs:242](../Services/Narou/NarouApiService.cs#L242):
```csharp
var url = $"{API_BASE}?out=json&lim={lim}&genre={genre}&order=...";
```
**なろう API の `genre=` パラメータはサブジャンル ID** (101=異世界恋愛, 102=現実世界恋愛 等) を期待する一方、UI のドロップダウンはビッグジャンル (`BigGenres`: 1=恋愛, 2=ファンタジー 等)。`biggenre=` パラメータを使うべきところを誤って `genre=` で渡している → サブジャンル ID として 1, 2 等は存在しないため API は実質「該当なし」を返すか不定な結果になる。

**API 仕様の裏取り（2026-04-30 https://dev.syosetu.com/man/api/ で確認済み）:**
- `biggenre` パラメータは整数値で大ジャンル指定: 0=未選択, 1=恋愛, 2=ファンタジー, 3=文芸, 4=SF, 99=その他, 98=ノンジャンル。ハイフン区切りで複数指定可（本プランでは単一値のみサポート）。
- `genre` パラメータはサブジャンル指定: 101=異世界〔恋愛〕, 102=現実世界〔恋愛〕, 201=ハイファンタジー〔ファンタジー〕, ..., 9801=ノンジャンル〔ノンジャンル〕等の 3〜4 桁 ID。
- 旧コードが `genre={1, 2, ...}` と渡していたのは API 仕様上どのサブジャンル ID にもマッチしない無効値で、API は実質「ヒットなし」を返していた。この点はプラン v3 の主張通り。
- `biggenre` 未指定時は全大ジャンルが対象になる（パラメータ自体を URL に付けない）。

**修正方針:**
- `FetchByGenreAsync` のシグネチャを `int? biggenre` に変更（null 許容 = 全ジャンル）。
- パラメータを `genre=` → `biggenre=` に修正。
- 「すべて」選択時は `biggenre` 指定なしで全ジャンル取得（API 仕様で order のみで全ジャンルランキングが返る）。

**修正 1 — `NarouApiService.cs:236-246` の `FetchByGenreAsync`:**

```csharp
/// <summary>
/// ジャンル別の作品取得（novelapi）。biggenre=null で全ジャンル。
/// </summary>
public async Task<List<SearchResult>> FetchByGenreAsync(int? biggenre, string order, int limit, CancellationToken ct = default)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(20));

    var lim = Math.Clamp(limit, 1, 100);
    var url = $"{API_BASE}?out=json&lim={lim}&order={Uri.EscapeDataString(order)}";
    if (biggenre.HasValue) url += $"&biggenre={biggenre.Value}";

    var json = await _network.GetStringAsync(SiteType.Narou, url, cts.Token).ConfigureAwait(false);
    return ParseNovelApiJson(json);
}
```

**修正 2 — `SearchViewModel.cs:221-234` の `FetchGenreAsync`:**

```csharp
[RelayCommand]
private Task FetchGenreAsync()
{
    int? narouBg = (SelectedNarouBigGenre is not null
        && int.TryParse(SelectedNarouBigGenre.Id, out var bg)) ? bg : null;
    // narouBg is null → 「すべて」選択 → biggenre なしで fetch（全ジャンル）

    return ExecuteSiteQueryAsync(
        "Genre fetch",
        SearchNarou
            ? ct => _narou.FetchByGenreAsync(narouBg, "weeklypoint", 30, ct)
            : null,
        (SearchKakuyomu && SelectedKakuyomuGenre is not null)
            ? ct => _kakuyomu.FetchRankingAsync(SelectedKakuyomuGenre.Id, "weekly", ct)
            : null);
}
```

「`SearchNarou ? ... : null`」に修正（旧コードは `narouBg is int bgv ? ... : null` で「すべて」時は null になっていた）。

**注意点:**
- `_narou.FetchByGenreAsync` の他の呼び出し箇所はなし（[grep 確認](../ViewModels/SearchViewModel.cs#L230)）。シグネチャ変更の影響範囲は本ファイル一箇所のみ。
- 要件書 §7.1 のクエリパラメータ表に `biggenre`（ビッグジャンル ID 1=恋愛 等）を追記し、`genre`（サブジャンル）と区別を明記（L-4 / PR-5）。
- N-3 のバグ修正は機能を初めて意図通り動かすことになるため、実機確認は必須。

---

## N-4: カクヨムのランキング・ジャンルブラウズが取得できない

**問題（バグ）:** [KakuyomuApiService.cs:278-335](../Services/Kakuyomu/KakuyomuApiService.cs#L278-L335) の `FetchRankingAsync` が無関係な結果を返す。原因は **selector が広すぎる**:

- 現在の実装は `a[href*='/works/']` でリンクを総当たりし、ナビゲーション・キャッチコピー（広告枠）の作品リンクまで拾う。同じ作品が複数回出る、ランキング順が崩れる、ランキング 100 位以下のおすすめ枠が混入する等が起こる。

ジャンルブラウズ（Issue 4）も `FetchRankingAsync` を流用するため、根本原因は同一。両方を 1 修正で解消する。

### 事前調査結果（2026-04-30 Firecrawl MCP で実 HTML を確認）

カクヨムランキングページ (`https://kakuyomu.jp/rankings/all/weekly` および `/rankings/fantasy/weekly`) の構造を Firecrawl の `rawHtml` で取得して確認した:

- **サーバ側 HTML レンダリング**。`__NEXT_DATA__` / Apollo State **不使用**（小説詳細ページとは異なる系統）。
- 各ランキング項目: `<div class="widget-work float-parent" itemscope itemtype="schema.org/CreativeWork">`
- ランキング順位: `<p class="widget-work-rank">{N}</p>`（`widget-work` 配下の最初の子）
  - **これがない `widget-work` は広告/おすすめ枠** → `widget-work-rank` の有無でフィルタする
- タイトル: `<a href="/works/{workId}" class="widget-workCard-titleLabel ..." itemprop="name">タイトル</a>`
- 作者名: `<a class="widget-workCard-authorLabel" href="/users/{userId}" itemprop="author">作者名</a>`
- ステータス: `<span class="widget-workCard-statusLabel">連載中</span>` or `完結`
- 話数: `<span class="widget-workCard-episodeCount">49話</span>`
- 順序: **DOM 順** に 1〜100 位（事前調査で `widget-work-rank` が `1, 2, 3, ..., 100` の順に並ぶことを確認）

**確認したこと:**
- `all/weekly`: `widget-work` が 105 件、うち `widget-work-rank` 付きが 100 件（広告 5 件混在）
- `fantasy/weekly`: 同様に rank 付き 100 件、ジャンル絞り込み機能している
- `<title>` タグでジャンル/期間ラベルが切り替わる（"異世界ファンタジー 週間の長編ランキング" 等）
- 属性順: title anchor は `href` → `class` の順、author anchor は `class` → `href` の順（AngleSharp の CSS セレクタは属性順非依存なので問題なし）

→ **Apollo State 解析は不要**。`div.widget-work` で確実にスコープして、その中の各要素を AngleSharp の CSS セレクタで取れば良い。これは当初の方針より大幅に単純な実装で済む。

**注: 事前調査の代替手段**

将来再調査が必要になったら以下のコマンドで再現可能:

```
mcp__firecrawl-mcp__firecrawl_scrape(
    url="https://kakuyomu.jp/rankings/all/weekly",
    formats=["rawHtml"],
    onlyMainContent=false,
    waitFor=5000)
```

### 修正 — `KakuyomuApiService.cs:278-335` の `FetchRankingAsync`

```csharp
public async Task<List<SearchResult>> FetchRankingAsync(string genreSlug, string periodSlug, CancellationToken ct = default)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(20));

    var url = $"{BASE_URL}/rankings/{genreSlug}/{periodSlug}";
    var html = await _network.GetStringAsync(SiteType.Kakuyomu, url, cts.Token).ConfigureAwait(false);

    var config = Configuration.Default;
    var context = BrowsingContext.New(config);
    var document = await context.OpenAsync(req => req.Content(html), cts.Token).ConfigureAwait(false);

    var results = new List<SearchResult>();
    var seen = new HashSet<string>(); // 重複排除フォールバック保険（事前調査では重複なしだが、サイト構造変化への保険）

    // widget-work のうち、widget-work-rank を持つカードのみ対象(広告/おすすめ枠を除外)
    var workCards = document.QuerySelectorAll("div.widget-work");
    foreach (var card in workCards)
    {
        // ランキング順位を持つカードに限定
        var rankEl = card.QuerySelector("p.widget-work-rank");
        if (rankEl is null) continue;

        // タイトルリンク → workId と title
        var titleLink = card.QuerySelector("a.widget-workCard-titleLabel");
        if (titleLink is null) continue;

        var href = titleLink.GetAttribute("href") ?? "";
        var workId = ExtractWorkId(href);
        if (string.IsNullOrEmpty(workId)) continue;
        if (!seen.Add(workId)) continue; // 既出 workId は無条件スキップ

        var title = titleLink.TextContent.Trim();
        if (string.IsNullOrEmpty(title)) continue;

        // 作者名（同カード内の widget-workCard-authorLabel）
        var authorLink = card.QuerySelector("a.widget-workCard-authorLabel");
        var author = authorLink?.TextContent.Trim() ?? "";

        // ステータス（完結フラグ）
        var statusLabel = card.QuerySelector("span.widget-workCard-statusLabel");
        var isCompleted = statusLabel?.TextContent.Trim() == "完結";

        // 話数（"49話" 等から数値部分を抽出）
        var episodeCountText = card.QuerySelector("span.widget-workCard-episodeCount")?.TextContent ?? "";
        var episodeMatch = System.Text.RegularExpressions.Regex.Match(episodeCountText, @"\d+");
        var totalEpisodes = episodeMatch.Success && int.TryParse(episodeMatch.Value, out var n) ? n : 0;

        results.Add(new SearchResult
        {
            SiteType = SiteType.Kakuyomu,
            NovelId = workId,
            Title = title,
            Author = author,
            TotalEpisodes = totalEpisodes,
            IsCompleted = isCompleted,
        });

        if (results.Count >= 30) break;
    }

    return results;
}
```

**注意点:**
- 旧スクレイプロジック (lines 290-332 の HashSet 重複排除と `a[href*='/works/']` foreach) は**完全削除**。
- `widget-work-rank` の存在チェックで広告/おすすめ枠の混入を防ぐ（事前調査で 5 件混在を確認）。
- **重複排除（HashSet）はフォールバック保険として残す**: ランキングページは事前調査時点で同一作品を重複表示しないが、サイト構造変化やキャンペーン枠での同作品再掲などに対する低コストな保険。`seen.Add(workId)` 1 行のみで実装でき、想定通り重複ゼロなら overhead はほぼゼロ。
- 取得件数は 30 件で打ち切り（実機では SearchView の表示と整合させる現状値を維持）。事前調査では 100 件あることは確認済み。
- **DOM 順序保証**: AngleSharp の `QuerySelectorAll` は仕様上 document order を返す（W3C DOM 仕様準拠）。`div.widget-work` の出現順がランキング順位の昇順になる事前調査結果と整合する。万一順序がブレる場合は `widget-work-rank` の数値をパースして `OrderBy` する保険ロジックを追加するが、初版では不要と判断。
- 話数 (`TotalEpisodes`) と完結フラグ (`IsCompleted`) も同時に取れるため、検索結果カードの情報量が現状より増える（メリット）。
- **既存 `ExtractApolloState` / `ParseApolloState` ロジックは触らない**。それらは `FetchEpisodeListAsync` 等の小説詳細ページ用で、ランキングページとは別系統。
- ジャンルブラウズ（Issue 4）は [SearchViewModel.cs:232](../ViewModels/SearchViewModel.cs#L232) から同メソッドが呼ばれるため、N-4 の修正で同時解消。Service 側の追加修正なし。
- 要件書 §7.2 のエンドポイント表に「ランキング/ジャンル: `https://kakuyomu.jp/rankings/{genre}/{period}` をサーバ HTML スクレイピング (`div.widget-work` セレクタ)」を明記（L-4 / PR-5）。

**カクヨム HTML 構造変更時のフォールバック:**
- `widget-work` クラス名が変わったら `[itemscope][itemtype*='CreativeWork']` への切替を検討。
- それでも壊れたら、ランキングページに `__NEXT_DATA__` が導入される可能性も含めて再調査。

---

# 想定外で詰まった場合

- C-1 のリトライループで 3 秒待っても `IPlatformApplication.Current` が null のままなら、`MauiAppCompatActivity` が `IPlatformApplication` を当 Activity から確立しないバグの可能性がある。その場合はフォールバック登録（既定 6h）が走るので、最低限の動作は維持される。次回起動以降に正常化する。
- C-1 の `MainActivity.OnCreate` で fire-and-forget 例外が出る場合は、`L-5` を先にマージしておくと TaskScheduler.UnobservedTaskException で拾えるため切り分けやすい（`Task.Run(async () => ...)` の経路は UnobservedTaskException 対象）。
- M-5 の `EpisodeListPage.OnAppearing` を `async void` 化する修正は、内部の `EnsureInitializedAsync` / `RefreshReadStatusAsync` で例外を出すと **UnobservedTaskException では拾えない**（async void の例外は SynchronizationContext に直接ポストされる）。`InitializeAsync` 自体は try/catch 完備なので実害は無いが、念のため OnAppearing 内に `try { ... } catch (Exception ex) { LogHelper.Warn(...); }` を追加して async void 例外がプロセスクラッシュに繋がらないようにする。
- M-4 の `<x:Int32>` 構文がコンパイルエラーになる MAUI バージョンがあれば、`<DataTrigger Value="{x:Static helpers:BackgroundTheme.Light}" />` の形に切り替える代替案がある（[ReaderThemeIndex.cs](../Helpers/ReaderThemeIndex.cs) の定数を活用、`xmlns:helpers="clr-namespace:LanobeReader.Helpers"` を追加）。
- H-4 の `SyncEnqueuedIdsFromQueues` で `_enqueuedEpisodeIds` の lock 取得順がデッドロックを起こす設計は無いことを確認済み（lock は `_enqueuedEpisodeIds` 単独で取り、内部で他 lock を取らないため）。
- **PR-4 が想定外に大きく膨らんで他 PR が長期ブロックされる場合**: PR-4 (L-1, L-2, L-5-L-10) は L-3 を含まないため、PR-7 (L-3 + N1-N4) と並列でレビューできる。PR-7 が L-3 と N-1 を同 PR で扱う構造のため、PR-4 とのファイル衝突は発生しない。PR-4 が長引いても PR-7 を先に投入する運用も可能（推奨順序の変更）。
- **PR-7 内で L-3 と N-1 が分離 revert される事故**: 1 コミットに統合したので構造的に発生しないが、万一誰かが手動で部分パッチを当てた場合（searchTarget だけ削除したまま title/wname 追加忘れ等）はビルドエラー (CS0535) ではなく、ビルドは通るが「全項目検索のまま」になる経路がある。レビュー時はコミット内に searchTarget 削除と URL に title=1 wname=1 追加が**両方含まれている**ことを確認する。

---

# 削除予定（全 PR 完了後）

本ファイル `plan_2026-04-30_review-c1-l11.md` は **PR-1〜PR-7 全マージ後**に GitHub PR description に内容を移し、リポジトリから削除する。PR-5 でその他の plan ファイルを除去するタイミングと合わせず、本プランは最後の cleanup commit で削除する（PR-5 は他プランの整理に専念）。
