# TradeAnalyzer ML（段階2: LambdaRank 一次モデル）

特徴量・学習・推論を**すべて Python で完結**するバッチ。C#↔Python の境界は `trade.db`（同一 SQLite）のみ。
Python が `Signal.MlScore` に out-of-sample スコアを書き戻し、C# はそれを読むだけ（ONNX 不使用・プロセス連携なし）。

## 依存・セットアップ

[`uv`](https://docs.astral.sh/uv/) 管理（`pyproject.toml`）。`.venv/` とモデル実体 `models/` は gitignore。

```bash
cd _Apps/ml
uv sync              # .venv 作成＋依存解決（lightgbm/optuna/pandas/numpy/scikit-learn）
```

## 実行手順（正しい順序）

段階2は**手動起動の逐次バッチ**。SQLite 書込ロック衝突を避けるため Python は **C# プロセス完全終了後**に実行する。

```bash
# 0. (C#) 段階1で ingest 済みの trade.db を用意（要 APIキー。複数年の DailyBar が必要）
#    dotnet run --project ../TradeAnalyzer.Worker -- ingest --from 2024-01-01 --to 2025-12-31

# 1. (C#) ルール母集団を生成（IS/OOS 各ウィンドウの全リバランス日。MlScore=null で挿入）
dotnet run --project ../TradeAnalyzer.Worker -- signals --is 2024 --oos 2025

# 2. (Python) 特徴量→ラベル→ウォークフォワード学習→OOS スコアを Signal.MlScore に書き戻し
uv run python train.py --db ../TradeAnalyzer.Worker/trade.db --is 2024 --oos 2025

# 3. (Python) OOS 検証レポート（Rank-IC / NDCG@K）
uv run python evaluate.py --db ../TradeAnalyzer.Worker/trade.db --is 2024 --oos 2025

# 4. (C#) A/B バックテスト（MlScore 順 vs RuleScore 順）
dotnet run --project ../TradeAnalyzer.Worker -- backtest --is 2024 --oos 2025 --use-ml false
dotnet run --project ../TradeAnalyzer.Worker -- backtest --is 2024 --oos 2025 --use-ml true
```

### DB パスの注意

`signals` を `dotnet run`（cwd=Worker プロジェクト直下）で実行すると `trade.db` は
`_Apps/TradeAnalyzer.Worker/trade.db` に出来る。`--db` にその実パスを渡す。
`bin/` から `.dll` 直接起動した場合は実体位置が変わるので注意。

## ⚠ 順序ガード（MlScore 全消去）

`signals` は date 単位の delete→insert で `MlScore=null` を再挿入するため、書き戻し済みスコアを消す。
**`signals` を回したら同期間の `train.py` を必ず再実行してから `backtest --use-ml true`** すること
（さもないと C# 側の null 検査で即エラー＝silent fallback を防ぐ意図的挙動）。
正しい運用順序は常に `signals → train → (evaluate) → backtest --use-ml`。
`RuleOptions` を変えた場合も母集団が変わるので `signals` 再生成→再学習が必要。

## ファイル構成

| ファイル | 役割 |
|---|---|
| `db.py` | `trade.db` 読み書き（Signals/DailyBars/FinSummaries、MlScore 書戻し） |
| `features.py` | 点-in-time 特徴量・クロスセクション Rank/ZScore（学習・推論で同一） |
| `labels.py` | N日後フォワード相対リターン → gain ラベル（先読み防止・未確定除外） |
| `train.py` | ウォークフォワード学習＋optuna（Purged K-Fold + embargo）＋OOS 書戻し |
| `evaluate.py` | Rank-IC / NDCG@K 検証レポート（t統計量・p値・ICIR・符号検定＋ML−Rule 対日差検定） |
| `models/` | 学習済みモデル（`*.txt` LightGBM native。gitignore） |

## 既知の制約

- **OOS 母数とサバイバーシップ**: 検証は J-Quants Light の5年データ（OOS ≈ 1.5年/72リバランス日）。
  OOS 母数が限られ Rank-IC の統計的有意性には限界（株式の Rank-IC は本来 0.02〜0.05）。加えて master は
  単一スナップショットで survivorship バイアスが残る（上場廃止/新規上場が母集団に反映されず楽観方向に偏りうる）。
  段階2の目的はパイプライン疎通＋ルール単純合算を超えうるかの一次確認。実運用精度の作り込みは段階3以降。
  実測（Light 5年, OOS 68日）: ML Rank-IC +0.036 は t=+2.17/p≒0.03 で有意（IC>0率63%）だが、
  **ML−Rule の上乗せ +0.038 は t=+1.77/p≒0.076 と5%有意には未達**＝「ルール超え」は示唆どまり。
  p値・符号検定は正規近似（標本日数が小さいほど近似は粗い）。
- **足切り後母集団のみ学習**: ML はルール `Passed` 銘柄だけを並べ替える（段階ゲート）。
- **ラベルとバックテストエグジットの非対称**: ラベルは固定 H ホライズン、バックテストは ATR ストップ/MaxHoldDays。
  ランキング学習は相対順位なので許容。実損益（バックテスト）とランキング指標（Rank-IC）は別物として読む。
