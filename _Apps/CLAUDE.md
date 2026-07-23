# CLAUDE.md

このファイルは `_Apps` 配下（app-trade-analyzer ブランチ固有）で作業する際のガイド。

## 概要

TradeAnalyzer は日本株の「データ取得 → ルール/ML シグナル → バックテスト → 当日 EOD 推論 → Claude 定性根拠 → 配信」を行う CLI アプリ。TBird.* を参照しない独立 net10.0 複数プロジェクト構成（TBirdObject 継承・命名規則等の全体共通ルールは適用外。詳細はルート CLAUDE.md「独立アプリ例外」参照）。

## 構成

- `TradeAnalyzer.Data` — EF Core (SQLite) エンティティ / マイグレーション、外部 API クライアント（J-Quants / EDINET）、`AppPaths`（実行時パス解決）
- `TradeAnalyzer.Core` — テクニカル指標、ルールエンジン、ingest、バックテスト
- `TradeAnalyzer.Worker` — CLI エントリ（composition root）、Claude 定性層（`claude -p` 直結）、Python 採点連携（`ProcessRunner`）、`SelfTest`
- `ml/` — Python 側 ML（LightGBM LambdaRank）。uv 管理（`pyproject.toml` / `uv.lock`、`.venv` は追跡外）。詳細は [ml/README.md](ml/README.md)
- `scripts/` — 運用 PowerShell（`run-today.ps1` / `explain-today.ps1` / `retrain.ps1`。タスクスケジューラ登録前提）

## ビルド・実行

```bash
dotnet build _Apps/App.sln
dotnet run --project _Apps/TradeAnalyzer.Worker -- <command>
```

コマンド一覧と引数は `Commands.PrintUsage`（引数なし実行で表示）が正。概要:

- `migrate` / `ingest` / `analyze` / `signals` / `backtest` — 段階1〜2（履歴データ・検証）
- `run-today` — 段階3a: 当日 EOD 推論（ingest → analyze → Python 採点 → Top-K）
- `explain-today` — 段階3b: Claude 根拠文生成 → `QualitativeJson` 書戻し
- `notify-today` — 段階3c: 配信ペイロード組み立て
- `selftest` — APIキー不要の単体検証（指標 / ルール / 先読み防止 / パーサ回帰）
- `stats` — DB テーブル行数等の確認

## 実行時状態（git 追跡外）

置き場はルート CLAUDE.md のルール通り `_Tools/TradeAnalyzer/`:
`trade.db` / `Secrets.json`（APIキー）/ `ml/models` / `logs` / bin・obj（`Directory.Build.props` の ArtifactsPath）。
パス解決は `TradeAnalyzer.Data/AppPaths.cs`（CWD 非依存。環境変数 `TRADEANALYZER_DATA_DIR` で上書き可）。

## 重要な規約

- **ExitCode 契約が段階で逆向き**: `explain-today` は Claude 実行時失敗を非致命スキップ（データ前提未達のみ ExitCode=1）。`notify-today` は「届ける」ことが仕事のため送信系失敗＝ExitCode=1、Passed=0 は正常（0 件ペイロード・ExitCode=0）。コマンド内で catch して契約を潰さないこと。
- **文字列化は InvariantCulture**: 日付・数値の補間（SQL 文字列・CLI 引数含む）は culture 明示。過去レビューで複数回再発した箇所。例外: 表示専用の数値書式（Console 出力の `:F4` 等）は CurrentCulture 容認（日付は表示でも Invariant 明示）。
- **Claude 出力の数値安全性**: `QualitativeNumberGuard` で検証。プロンプト側で単位換算を禁止している（換算による誤検出対策）。
- **Configure は拡張メソッドに集約**（Core / Data）。Worker は composition root のため `Program.cs` で直接バインドしてよい。
- プランファイル置き場: `docs/plans/app-trade-analyzer/`
