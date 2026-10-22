# Netkeiba（_Apps）

競馬データ分析WPFデスクトップアプリケーション。

## 処理パイプライン

- `STEP1Command` - netkeiba.comからのデータ収集（Webスクレイピング）
- `STEP1OikiriCommand` - オイキリデータ処理
- `STEP2Command` - データ前処理
- `STEP3Command` - ML.NETによる機械学習モデル構築
- `STEP4*` - 予測結果の表示・分析
- STEP1/2/3 の本体は `_Core/Steps/` にあり、コマンドは確認ダイアログ・進捗表示などの UI だけを持つ

配備は `_Apps/deploy.ps1` を実行して `_Tools\Netkeiba\app\` と `_Tools\Netkeiba\scheduler\` へ出力し、`_Tools\Netkeiba\app\Netkeiba.exe` から起動する（ビルドでは配備しない）。scheduler はタスクスケジューラから起動する。
