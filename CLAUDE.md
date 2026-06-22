# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## リポジトリ概要

TBird.Libraryは、WPFアプリケーション、Android MAUIアプリケーション、Webスクレイピング、データベース操作、PDF処理などのユーティリティを提供する包括的な.NETライブラリコレクションです。ソリューションは複数の.NETバージョン（.NET Standard 2.0、.NET Framework 4.8、.NET 5.0、.NET 7.0、.NET 8.0、.NET 10.0-android）をターゲットとしています。

ソリューションファイルは 2 つに分かれており、用途に応じて使い分ける：
- `TBird.Library.sln` - WPF / Console / Service / DB / IO 等の WPF 系・サーバ系プロジェクト集合（MAUI workload 不要）
- `TBird.Maui.sln` - TBird.Core + TBird.Maui.* 4 プロジェクト（MAUI workload 必要、Android MAUI アプリ開発時のみ開く）

## ビルド

```bash
dotnet build TBird.Library.sln   # WPF/サーバ系（MAUI workload 不要）
dotnet build TBird.Maui.sln      # MAUI（要 MAUI workload。Android MAUI 開発時のみ）
```

- 一部プロジェクトは .NET Framework 4.8 のレガシー（非 SDK）形式（`TBird.Service` / `coretest` 等）。これらは Visual Studio もしくは `msbuild` でのビルドが確実
- `_Core` / `_Browser` の CLAUDE.md はそれらのプロジェクトを持つ `app-*` ブランチでのみ存在（`master` には無い）

## ブランチとアプリケーションの構成

`app-`プレフィックスのブランチ（例：`app-netkeiba`）ごとに、`_Apps`フォルダ内のアプリケーションプロジェクトが切り替わる。ライブラリ群（TBird.Core, TBird.Wpf等）は全ブランチで共有され、`_Apps`と`_Core`の内容のみがブランチ固有となる。

- `master` - ライブラリのみ（アプリケーションなし）
- `app-*` - 各ブランチで異なるアプリケーションが`_Apps`/`_Core`に配置される

## プロジェクト別CLAUDE.md

各プロジェクトの詳細は個別のCLAUDE.mdを参照：

### ライブラリ（全ブランチ共通）
- [TBird.Core/CLAUDE.md](TBird.Core/CLAUDE.md) - コア基盤
- [TBird.Windows/CLAUDE.md](TBird.Windows/CLAUDE.md) - Windows 専用 Win32 ラッパー
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
- [TBird.Maui/CLAUDE.md](TBird.Maui/CLAUDE.md) - MAUI 共通基盤（ViewModel / Converter / 通知許可 / メッセージサービス）
- [TBird.Maui.DB/CLAUDE.md](TBird.Maui.DB/CLAUDE.md) - SQLite 基底クラス（マイグレーション枠組み）
- [TBird.Maui.Background/CLAUDE.md](TBird.Maui.Background/CLAUDE.md) - 優先度キュー / ネットワーク監視 / レートリミッタ
- [TBird.Maui.Web/CLAUDE.md](TBird.Maui.Web/CLAUDE.md) - AngleSharp / HTTP transient リトライ

### アプリケーション（ブランチ固有）
- [_Apps/CLAUDE.md](_Apps/CLAUDE.md) - アプリケーション本体
- [_Core/CLAUDE.md](_Core/CLAUDE.md) - アプリケーション共有ライブラリ（`_Core` プロジェクトを持つ app-* ブランチでのみ存在）
- [_Browser/CLAUDE.md](_Browser/CLAUDE.md) - Webフロントエンド（`_Browser` プロジェクトを持つ app-* ブランチでのみ存在）

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

### アプリ新規開発時のルール

- ソリューションファイルは `_Apps/App.sln` とする（アプリ名のslnにしない）
- ソリューションフォルダは作成しない（`_Apps/` 直下に `.sln` を配置）
- プロジェクトフォルダも作成しない（`_Apps/` 直下に `.csproj` とソースファイルを配置）

### コード標準

- C# 10言語機能が有効
- null許容参照型が有効（`<Nullable>enable</Nullable>`）
- 拡張メソッドは`{型名}Extension.cs`の命名規則に従う
- 名前空間規則：TBird.{レイヤー}.{機能}
