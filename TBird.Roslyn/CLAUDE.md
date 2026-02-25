# TBird.Roslyn

Roslynコンパイラを使用したC#スクリプティング機能。

## 開発時の注意

- 利用側プロジェクトではFluentValidationの不要カルチャーを除外する設定が必要（roslyntest/roslyntest.csprojを参照）
- Partialクラスで`_dispose.cs`に破棄処理を分離するパターンを使用
