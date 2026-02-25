# TBird.Console

コンソールアプリケーションの基底クラスライブラリ。

## 開発時の注意

- `ConsoleExecuter`（または非同期版`ConsoleAsyncExecuter`）を継承し`MainExecute`をオーバーライド
- 組み込み引数解析：'-'付きはオプション、なしはパラメータ
