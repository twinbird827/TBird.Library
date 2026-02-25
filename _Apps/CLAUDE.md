# Netkeiba（_Apps）

競馬データ分析WPFデスクトップアプリケーション。

## 処理パイプライン

- `STEP1Command` - netkeiba.comからのデータ収集（Webスクレイピング）
- `STEP1OikiriCommand` - オイキリデータ処理
- `STEP2Command` - データ前処理
- `STEP3Command` - ML.NETによる機械学習モデル構築
- `STEP4*` - 予測結果の表示・分析
