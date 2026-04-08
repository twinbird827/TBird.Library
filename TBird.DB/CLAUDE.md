# TBird.DB

データベース操作の抽象化レイヤー。

## 開発時の注意

- `IDbControl`抽象化を使用し、新しいDBプロバイダーを追加する場合は`DbControl`を継承
- 非同期パターンに従うこと（SelectAsync等）
- 接続はusing文で適切に破棄すること
