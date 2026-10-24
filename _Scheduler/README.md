# Netkeiba.Scheduler

タスクスケジューラから起動する無人実行のバッチ。週次のモデル更新と、開催日の予想表を Discord webhook へ送る。

## 引数

- `weekly` - STEP1（差分取得とデータ補完）→ STEP2（差分生成）→ STEP3（再学習）を順に実行し、完了時は各モデルの NDCG@1（STEP2 で生成に失敗したレースがあればその件数も）を、失敗時は失敗した STEP と例外メッセージを Discord へ送る。失敗の復旧は WPF から手動で再実行する。
- `raceday` - 当日が非開催日なら何も送らず終了する。開催日なら各レースの発走 10 分前にそのレースを予想して予想表画像を送り、最終レース後に終了する。
- `predict <raceid...>` - 指定レースを今すぐ予想して送る。画像と送信の確認や手動の再送に使う。

## 配備とタスク登録

1. `_Tools\Netkeiba\lib\app-setting.json` の `DiscordWebhookUrl` に送信先の webhook URL を書く（WPF を閉じてから書く）。空のままだと何も処理せず終了コード 1 で終わる。
2. `_Apps\deploy.ps1` を実行する。`_Tools\Netkeiba\scheduler\` へ配備される。
3. `_Scheduler\register-tasks.ps1` を実行する。`Netkeiba weekly`（毎週金曜 20:00）と `Netkeiba raceday`（毎日 9:00）が登録される。時刻を変えるときはこのスクリプトを直して再実行する。

ログは WPF と同じ `_Tools\Netkeiba\log\yyyy-MM-dd.log` に出る。

## 運用上の注意

- weekly 実行中と raceday 実行中に WPF の STEP1〜3 を実行しない。同じ DB と `app-setting.json` を書くため。WPF の STEP4 は raceday 実行中も使ってよい。
- weekly 完了後は WPF を再起動してから STEP を実行する。WPF は `app-setting.json` を起動時にしか読まず、古いモデル一覧のまま予想・保存する。
- PC のスリープを無効にしておく。タスクで自動起床した PC は約 2 分で再スリープし、10 分前の送信が欠ける。
- 実行時刻にユーザーがログオンしていること。タスクはログオン中のみ実行で登録される。
- weekly の失敗を raceday 起動後に WPF で直したら、raceday タスクを再実行する。raceday は起動時の `app-setting.json` を使い続ける。
