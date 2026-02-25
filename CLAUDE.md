# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## リポジトリ概要

TBird.Libraryは、WPFアプリケーション、Webスクレイピング、データベース操作、PDF処理などのユーティリティを提供する包括的な.NETライブラリコレクションです。ソリューションは複数の.NETバージョン（.NET Standard 2.0、.NET Framework 4.8、.NET 5.0、.NET 7.0、.NET 8.0）をターゲットとしています。

## ブランチとアプリケーションの構成

`app-`プレフィックスのブランチ（例：`app-netkeiba`）ごとに、`_Apps`フォルダ内のアプリケーションプロジェクトが切り替わる。ライブラリ群（TBird.Core, TBird.Wpf等）は全ブランチで共有され、`_Apps`と`_Core`の内容のみがブランチ固有となる。

- `master` - ライブラリのみ（アプリケーションなし）
- `app-*` - 各ブランチで異なるアプリケーションが`_Apps`/`_Core`に配置される

## プロジェクト別CLAUDE.md

各プロジェクトの詳細は個別のCLAUDE.mdを参照：

### ライブラリ（全ブランチ共通）
- [TBird.Core/CLAUDE.md](TBird.Core/CLAUDE.md) - コア基盤
- [TBird.Wpf/CLAUDE.md](TBird.Wpf/CLAUDE.md) - WPF MVVMフレームワーク
- [TBird.DB/CLAUDE.md](TBird.DB/CLAUDE.md) - データベース抽象化
- [TBird.DB.SQLite/CLAUDE.md](TBird.DB.SQLite/CLAUDE.md) - SQLiteプロバイダー
- [TBird.DB.SQLServer/CLAUDE.md](TBird.DB.SQLServer/CLAUDE.md) - SQL Serverプロバイダー
- [TBird.Plugin/CLAUDE.md](TBird.Plugin/CLAUDE.md) - プラグインシステム
- [TBird.Roslyn/CLAUDE.md](TBird.Roslyn/CLAUDE.md) - C#スクリプティング
- [TBird.Web/CLAUDE.md](TBird.Web/CLAUDE.md) - Webスクレイピング
- [TBird.IO/CLAUDE.md](TBird.IO/CLAUDE.md) - IO/HTML解析
- [TBird.IO.Img/CLAUDE.md](TBird.IO.Img/CLAUDE.md) - 画像操作
- [TBird.IO.Pdf/CLAUDE.md](TBird.IO.Pdf/CLAUDE.md) - PDF処理
- [TBird.Service/CLAUDE.md](TBird.Service/CLAUDE.md) - Windowsサービス
- [TBird.Console/CLAUDE.md](TBird.Console/CLAUDE.md) - コンソールアプリ基底

### アプリケーション（ブランチ固有）
- [_Apps/CLAUDE.md](_Apps/CLAUDE.md) - アプリケーション本体
- [_Core/CLAUDE.md](_Core/CLAUDE.md) - アプリケーション共有ライブラリ
- [_Browser/CLAUDE.md](_Browser/CLAUDE.md) - Webフロントエンド

### テスト
- [coretest/CLAUDE.md](coretest/CLAUDE.md) - Core テスト
- [wpftest/CLAUDE.md](wpftest/CLAUDE.md) - WPF テスト
- [roslyntest/CLAUDE.md](roslyntest/CLAUDE.md) - Roslyn テスト

## テスト

ユニットテストフレームワークではなく、実行可能なテストアプリケーションを使用している点に注意。

## 全体共通ルール

### TBirdObjectの継承規則

すべての主要コンポーネントは`TBirdObject`を継承する。Disposeメソッドはsealedのため、リソース解放は以下をオーバーライドすること：
- `DisposeManagedResource()` - マネージドリソースの解放
- `DisposeUnmanagedResource()` - アンマネージドリソースの解放

### コード標準

- C# 10言語機能が有効
- null許容参照型が有効（`<Nullable>enable</Nullable>`）
- 拡張メソッドは`{型名}Extension.cs`の命名規則に従う
- 名前空間規則：TBird.{レイヤー}.{機能}
