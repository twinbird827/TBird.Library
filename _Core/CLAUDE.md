# Netkeiba.Core（_Core）

競馬データのモデル・スクレイピングコアライブラリ。

## 開発時の注意

- `OptimizedHorseFeatures.cs`は非常に大きなファイル（50KB超）
- _Appsと_Schedulerの両方から参照される共有ライブラリ
- STEP1/2/3 の本体は `Steps/` にあり、WPF のコマンドと _Scheduler の weekly の両方から呼ばれる
