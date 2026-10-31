# TradeAnalyzer ML（段階2: LambdaRank 一次モデル）

特徴量・学習・推論を**すべて Python で完結**するバッチ。C#↔Python の境界は `trade.db`（同一 SQLite）のみ。
Python が `Signal.MlScore` に out-of-sample スコアを書き戻し、C# はそれを読むだけ（ONNX 不使用・プロセス連携なし）。

## 依存・セットアップ

[`uv`](https://docs.astral.sh/uv/) 管理（`pyproject.toml`）。`.venv/` は `_Scheduler/ml/` 内（gitignore 済・`uv sync` で再生成可）。
学習済みモデル実体は **`_Tools/TradeAnalyzer/ml/models/`**（`train.py` が `__file__` からリポジトリルートを求めて解決。
`_Tools/` は gitignore 済＝_ フォルダの削除やブランチ切替でも生存する。C# の `AppPaths.MlModelsDir` と一致）。

```bash
cd _Scheduler/ml
uv sync              # .venv 作成＋依存解決（lightgbm/optuna/pandas/numpy/scikit-learn）
```

## 実行手順（正しい順序）

段階2は**手動起動の逐次バッチ**。SQLite 書込ロック衝突を避けるため Python は **C# プロセス完全終了後**に実行する。

```bash
# 0. (C#) 段階1で ingest 済みの trade.db を用意（要 APIキー。複数年の DailyBar が必要）
#    dotnet run --project .. -- ingest --from 2024-01-01 --to 2025-12-31

# 1. (C#) ルール母集団を生成（IS/OOS 各ウィンドウの全リバランス日。MlScore=null で挿入）
dotnet run --project .. -- signals --is 2024 --oos 2025

# 2. (Python) 特徴量→ラベル→ウォークフォワード学習→OOS スコアを Signal.MlScore に書き戻し
uv run python train.py --db ../../_Tools/TradeAnalyzer/trade.db --is 2024 --oos 2025

# 3. (Python) OOS 検証レポート（Rank-IC / NDCG@K）
uv run python evaluate.py --db ../../_Tools/TradeAnalyzer/trade.db --is 2024 --oos 2025

# 4. (C#) A/B バックテスト（MlScore 順 vs RuleScore 順）
dotnet run --project .. -- backtest --is 2024 --oos 2025 --use-ml false
dotnet run --project .. -- backtest --is 2024 --oos 2025 --use-ml true
```

### 買い方の探索（#414）

`rules.py` の全ルールを月末形成・翌月末保有で評価し、事前に固定した選抜基準で候補を選ぶ（基準は `explore.py` の docstring）。DB は読み取り専用で開く。

```bash
uv run python explore.py --db ../../_Tools/TradeAnalyzer/trade.db
```

要約表・期間の境界・候補を標準出力に出し、`_Tools/TradeAnalyzer/explore/` に `summary.csv`（ルール×版ごとの要約と選抜基準の合否）と `monthly.csv`（形成月ごとの TX・LS・銘柄数・上位 5 分位の最大業種比率）を書く。

`triggers.py` の全トリガーを「トリガー日の翌営業日の始値で建て、5・60・120・240 営業日持つ」前提で評価し、通過したトリガー・売りのトリガーを使った見送り/早めの売り・通過したトリガーの OR 戦略を要約する（基準は `explore_triggers.py` の docstring）。DB は読み取り専用で開く。

```bash
uv run python explore_triggers.py --db ../../_Tools/TradeAnalyzer/trade.db
```

要約表・期間の境界・通過したトリガーと採った保有期間・OR 戦略の同時保有数を標準出力に出し、`_Tools/TradeAnalyzer/explore/triggers/` に `summary.csv`（トリガー×版×保有期間ごとの要約と合否）と `monthly.csv`（月ごとの平均超過リターン）を書く。

`--combo` を付けると、組み合わせのトリガーと財務の指標のトリガー（F1〜F5 と、それに PBR 1 倍未満を掛けた C12〜C16。`triggers.COMBO_TRIGGERS`）を足して同じ基準で選び、通過した買いのトリガーごとに負けた買い（P2 出来高急増の大陽線・P4 急落）が直前 20 営業日に出ていたら建てない見送り（版は「(b) 負けた買いの見送り」）を (b) と同じ基準で評価し、C3 の頑健性グリッドと時価総額・地合いの層別（`triggers.ROBUSTNESS`、版は「頑健性」で選抜に使わない）も並べて、末尾に試行数を出す。出力先は `_Tools/TradeAnalyzer/explore/triggers-combo/`。

```bash
uv run python explore_triggers.py --db ../../_Tools/TradeAnalyzer/trade.db --combo
```

`explore_events.py` は、イベント（開示・修正）で立つ買いのトリガー（E1〜E4・C2・C3・C6・C9〜C16・F1〜F5）について、イベントの後の値動きを記述する（トリガーの選抜には使わない。定義は docstring）。取れない部分（トリガー日の値動きと翌日の窓開け）と取れる部分（翌日始値から 5〜240 営業日）の超過リターン、イベントの後 60 営業日の累積の超過リターン、翌日始値から 3%・5%・10% 下の指値が 5 営業日以内に約定したもの・しなかったものの値動き、窓開けの 5 分位ごとの値動きを、P1〜P3 とともに出す。DB は読み取り専用で開く。

```bash
uv run python explore_events.py --db ../../_Tools/TradeAnalyzer/trade.db
```

トリガーごとの要約（寄らなかったイベントの件数を含む）・期間の境界・試行数を標準出力に出し、`_Tools/TradeAnalyzer/explore/events/` に `decompose.csv`（トリガー×区間）・`path.csv`（トリガー×イベントの後の日数）・`dip.csv`（トリガー×押し目の幅×保有期間×約定したか）・`gap.csv`（トリガー×窓開けの分位×保有期間）を書く。

`dataset.py` は、総当たりと決定木・LightGBM が共有する、銘柄 × 毎週の最終営業日ごとの指標 34 本（`dataset.FEATURES`）と、翌営業日の始値 → 5・20・60 営業日後の終値の超過リターン（母集団の等金額平均とコスト 0.3% を引いた値）の表を作る。封印期間（2023-01-01 以降）の価格は読まない。境界 `dataset.SEAL_END` は引数で変えられない定数。作った表は `_Tools/TradeAnalyzer/explore/scan/dataset.pkl` に保存し、DB・境界・計算に使うモジュールが同じなら次から読み込む。

`scan.py` は、その表で「1 指標の 5 分位」と「2 指標の分位の組み合わせ」を総当たりする。形成日ごとに超過リターンを銘柄の間で並べ替えて総当たりを 200 回やり直し、偶然でも出る最大の t 値の分布を作る。その 95% 点で候補（最大 5 本）と避ける条件を選ぶ（基準は `scan.py` の docstring）。DB は読み取り専用で開く。

```bash
uv run python scan.py --db ../../_Tools/TradeAnalyzer/trade.db
```

D1・D2 の境界・試行数・帰無分布の 95% 点・候補・避ける条件・単独の分位の傾き・実行時間を標準出力に出す。`_Tools/TradeAnalyzer/explore/scan/` には次を書く: `singles.csv`（指標×分位×保有期間）、`pairs.csv`（2 指標×分位×保有期間。足切りを通ったものだけ）、`null.csv`（シャッフルの回ごとの最大の t 値）、`candidates.csv`（候補と避ける条件）。

### DB パスの注意

`trade.db` は C# 側 `AppPaths` が **`_Tools/TradeAnalyzer/trade.db`** に絶対解決する（CWD 非依存。タスクスケジューラは `_Apps/deploy.ps1` が `_Tools/TradeAnalyzer/app/` へ配備した exe・スクリプトを `TRADEANALYZER_DATA_DIR` 付きで起動し、同じファイルを使う）。Python から実行するときは `_Scheduler/ml` を cwd に `--db ../../_Tools/TradeAnalyzer/trade.db` を渡す。環境変数 `TRADEANALYZER_DATA_DIR` を設定すると C#/Python ともその配下（`<dir>/trade.db`・`<dir>/ml/models`）を使う（別配置・テスト向けの上書き）。

## ⚠ 順序ガード（MlScore 全消去）

`signals` は date 単位の delete→insert で `MlScore=null` を再挿入するため、書き戻し済みスコアを消す。
**`signals` を回したら同期間の `train.py` を必ず再実行してから `backtest --use-ml true`** すること
（さもないと C# 側の null 検査で即エラー＝silent fallback を防ぐ意図的挙動）。
正しい運用順序は常に `signals → train → (evaluate) → backtest --use-ml`。
`RuleOptions` を変えた場合も母集団が変わるので `signals` 再生成→再学習が必要。

**段階2以降は `--use-ml false`（非ML）も `signals` 実行が前提**になった（破壊的変更）。
backtest は ML/非ML とも母集団を保存済み Signal から読む（provenance 対称化＝同一 OOS で
ML/非ML が定義上同一 picks 母集団を共有し A/B を公正化する）。OOS ウィンドウに取引日が在るのに
Signal 行が皆無なら「signals 未実行」として `InvalidOperationException` で停止する（従来の「非ML は
signals 無しでも RuleEngine 再評価で完結」する挙動は廃止）。

## ファイル構成

| ファイル | 役割 |
|---|---|
| `db.py` | `trade.db` 読み書き（Signals/DailyBars/FinSummaries、MlScore 書戻し） |
| `features.py` | 点-in-time 特徴量・クロスセクション Rank/ZScore（学習・推論で同一） |
| `labels.py` | N日後フォワード相対リターン → gain ラベル（先読み防止・未確定除外） |
| `train.py` | ウォークフォワード学習＋optuna（Purged K-Fold + embargo）＋OOS 書戻し |
| `evaluate.py` | Rank-IC / NDCG@K 検証レポート（t統計量・p値・ICIR・符号検定＋ML−Rule 対日差検定） |
| `rules.py` | 月末パネルと買い方ルールのスコア関数（探索とフォワードテストで共有）。`CANDIDATES` がフォワードテストの対象 |
| `explore.py` | 全ルールを P1・P2・P3 の 3 期間で評価し、事前固定の選抜基準で候補を選ぶ |
| `triggers.py` | 日次パネルと買い・売りのトリガー（探索とフォワードテストで共有） |
| `explore_triggers.py` | 全トリガー×保有期間を P1・P2・P3 の 3 期間で評価し、通過したトリガーの OR 戦略を要約する |
| `explore_events.py` | イベントで立つ買いのトリガーの、取れない部分と取れる部分・イベントスタディ・押し目・窓開けの大きさ別の値動きを記述する |
| `dataset.py` | 封印期間を除いた、銘柄 × 毎週の形成日ごとの指標 34 本と先の超過リターンの表（総当たりと決定木・LightGBM で共有） |
| `scan.py` | 1 指標・2 指標の分位の条件を総当たりし、シャッフルの帰無分布の 95% 点で候補を選ぶ |
| `_Tools/TradeAnalyzer/ml/models/` | 学習済みモデル（`*.txt` LightGBM native）＋`best_params.json`（gitignore 済） |

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
