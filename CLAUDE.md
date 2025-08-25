# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## リポジトリ概要

TBird.Libraryは、WPFアプリケーション、Webスクレイピング、データベース操作、PDF処理などのユーティリティを提供する包括的な.NETライブラリコレクションです。ソリューションは複数の.NETバージョン（.NET Standard 2.0、.NET Framework 4.8、.NET 5.0、.NET 7.0、.NET 8.0）をターゲットとしています。

## ビルドコマンド

```bash
# ソリューション全体をビルド
dotnet build TBird.Library.sln
dotnet build TBird.Library.sln -c Release

# クリーンして再ビルド
dotnet clean TBird.Library.sln
dotnet build TBird.Library.sln --no-incremental

# NuGetパッケージの復元
dotnet restore TBird.Library.sln

# 個別プロジェクトのビルド
dotnet build TBird.Core/TBird.Core.csproj
dotnet build TBird.Wpf/TBird.Wpf.csproj
```

## テストコマンド

プロジェクトはユニットテストフレームワークではなく、実行可能なテストアプリケーションを使用しています：

```bash
# テストアプリケーションの実行
dotnet run --project coretest/coretest.csproj    # .NET Framework 4.8
dotnet run --project wpftest/wpftest.csproj      # .NET 8.0 Windows
dotnet run --project roslyntest/roslyntest.csproj # .NET 5.0
```

## アーキテクチャ概要

### コア基盤パターン

すべての主要コンポーネントは`TBirdObject`を継承し、以下を提供します：
- GUIDベースのアイデンティティ管理
- マネージド/アンマネージドリソース処理を含むIDisposableパターン
- `ILocker`インターフェースを使用したスレッドセーフなロック機構
- 破棄時のイベントクリーンアップ

**重要**: TBirdObjectではDisposeメソッドはsealedであり、派生クラスでオーバーライドできません。代わりに以下のメソッドをオーバーライドします：
- `DisposeManagedResource()` - マネージドリソースの解放
- `DisposeUnmanagedResource()` - アンマネージドリソースの解放

### レイヤー構造

1. **コアレイヤー** (`TBird.Core`)
   - 基底クラス：TBirdObject、BindableBase
   - サービス抽象化：IMessageService
   - 共通型の拡張メソッド
   - スレッドセーフティのためのLockerパターン

2. **UIレイヤー** (`TBird.Wpf`)
   - 拡張されたINotifyPropertyChangedを持つMVVMフレームワーク
   - 特殊な監視可能コレクション（BindableCollectionバリアント）
   - WPFビヘイビアとコントロール
   - ダイアログとウィンドウの基底ビューモデル

3. **データレイヤー** (`TBird.DB.*`)
   - IDbControlによるデータベース抽象化
   - SQLiteとSQL Server実装
   - トランザクションと非同期操作サポート

4. **プラグインシステム** (`TBird.Plugin`)
   - 動的読み込み用のIPluginインターフェース
   - ライフサイクル管理用のPluginManagerシングルトン
   - "plugins"ディレクトリからDLLを読み込み

5. **サービスレイヤー** (`TBird.Service`)
   - コンソールフォールバック付きWindowsサービスサポート
   - 自己インストール機能（/i、/uスイッチ）

### 主要パターン

- **Disposableパターン**：広範囲にわたるリソース管理
- **シングルトン**：マネージャー（Plugin、Roslyn）で使用
- **MVVM**：自動破棄とコレクション管理で拡張
- **Async/Await**：全レイヤーでの包括的な非同期サポート

### アプリケーション例

**Netkeiba** (_Apps/Netkeiba)：競馬データ分析アプリケーション
- MahApps.MetroによるWPF UI
- SQLiteデータベース
- 機械学習用のML.NET
- WebスクレイピングのためのSelenium
- HTML解析のためのAngleSharp

## 開発ガイドライン

### 新しいコンポーネントを追加する場合

1. 適切な破棄管理のために`TBirdObject`を継承
   - リソース解放が必要な場合は`DisposeManagedResource()`をオーバーライド
   - `Dispose(bool disposing)`をオーバーライドしないこと（sealedのため）
2. MVVMビューモデルには`BindableBase`を使用
3. スレッドセーフなコンポーネントには`ILocker`を実装
4. 既存の名前空間規則に従う（TBird.{レイヤー}.{機能}）

### WPFで作業する場合

1. 監視可能コレクションには`BindableCollection`バリアントを使用
2. TBird.Wpf.Behaviorsの既存ビヘイビアを活用
3. ダイアログビューモデルは`DialogViewModel`を継承

### データベースで作業する場合

1. `IDbControl`抽象化を使用
2. 既存の非同期パターンに従う
3. データベース接続を適切に破棄

### コード標準

- C# 10言語機能が有効
- null許容参照型が有効（`<Nullable>enable</Nullable>`）
- プロジェクトタイプに基づいて適切な.NETバージョンをターゲット
- 拡張メソッドの既存パターンに従う

## 一般的なタスク

### 新しいプラグインの追加
1. `IPlugin`インターフェースを実装するクラスを作成
2. DLLとしてビルドし、"plugins"ディレクトリに配置
3. PluginManagerが自動的に検出して読み込み

### コンソールアプリケーションの作成
1. `ConsoleExecuter`を継承
2. `MainExecute`メソッドをオーバーライド
3. 組み込みの引数解析を使用（'-'付きオプション、なしはパラメータ）

### WPFウィンドウ/ダイアログの追加
1. `WindowViewModel`または`DialogViewModel`を継承するビューモデルを作成
2. データバインディングには`BindableBase`プロパティパターンを使用
3. 一般的なシナリオには既存のビヘイビアを活用

### リソース管理の実装
`TBirdObject`を継承したクラスでリソース管理が必要な場合：

**マネージドリソースの例**：
```csharp
public class MyClass : TBirdObject
{
    private FileStream _fileStream;
    private SqliteConnection _connection;
    
    protected override void DisposeManagedResource()
    {
        _fileStream?.Dispose();
        _connection?.Dispose();
        base.DisposeManagedResource();
    }
}
```

**アンマネージドリソースの例**：
```csharp
public class MyNativeWrapper : TBirdObject
{
    private IntPtr _nativeHandle;
    
    public MyNativeWrapper()
    {
        _nativeHandle = NativeMethods.CreateHandle();
    }
    
    protected override void DisposeUnmanagedResource()
    {
        if (_nativeHandle != IntPtr.Zero)
        {
            NativeMethods.ReleaseHandle(_nativeHandle);
            _nativeHandle = IntPtr.Zero;
        }
        base.DisposeUnmanagedResource();
    }
    
    // アンマネージドリソースを扱う場合はファイナライザーも実装
    ~MyNativeWrapper()
    {
        Dispose(false);
    }
}
```

**両方のリソースを扱う例**：
```csharp
public class HybridResource : TBirdObject
{
    private Bitmap _bitmap;              // マネージドリソース
    private IntPtr _deviceContext;       // アンマネージドリソース
    
    protected override void DisposeManagedResource()
    {
        _bitmap?.Dispose();
        base.DisposeManagedResource();
    }
    
    protected override void DisposeUnmanagedResource()
    {
        if (_deviceContext != IntPtr.Zero)
        {
            NativeMethods.DeleteDC(_deviceContext);
            _deviceContext = IntPtr.Zero;
        }
        base.DisposeUnmanagedResource();
    }
    
    ~HybridResource()
    {
        Dispose(false);
    }
}
```

## 追加のビルドとデバッグコマンド

### リンティングとコード品質

```bash
# コード分析の実行
dotnet build /p:RunAnalyzers=true /p:AnalysisLevel=latest

# 特定のプロジェクトでコード分析
dotnet build TBird.Core/TBird.Core.csproj /p:RunAnalyzers=true
```

### デバッグビルド

```bash
# デバッグシンボル付きビルド
dotnet build -c Debug /p:DebugType=full /p:DebugSymbols=true

# 詳細なビルドログ
dotnet build -v detailed > build.log
```

## プロジェクト間の依存関係

### 依存関係階層

```
TBird.Core (基盤)
├── TBird.Wpf (UIフレームワーク)
├── TBird.DB (データベース抽象化)
│   ├── TBird.DB.SQLite
│   └── TBird.DB.SQLServer
├── TBird.Web (Web関連機能)
├── TBird.Pdf (PDF処理)
├── TBird.Roslyn (C#スクリプティング)
├── TBird.Plugin (プラグインシステム)
└── TBird.Service (サービス基盤)
```

### ターゲットフレームワーク別の考慮事項

- **.NET Standard 2.0**: 最大の互換性のための基本ライブラリ
- **.NET Framework 4.8**: レガシーWindowsアプリケーションサポート
- **.NET 5.0/7.0/8.0**: モダンなクロスプラットフォーム機能

## 重要な実装詳細

### TBirdObjectのライフサイクル

1. **コンストラクタ**: GUIDが自動生成される
2. **使用中**: イベントハンドラーとリソースが管理される
3. **Dispose呼び出し**: 
   - マネージドリソースが解放される
   - イベントハンドラーが自動的に削除される
   - アンマネージドリソースが解放される
4. **ファイナライザー**: アンマネージドリソースのみ解放

### BindableCollectionの使い分け

- `BindableCollection<T>`: 基本的なスレッドセーフコレクション
- `SortableBindableCollection<T>`: ソート機能付き
- `FilterableBindableCollection<T>`: フィルタリング機能付き
- `GroupableBindableCollection<T>`: グルーピング機能付き

### 非同期パターン

```csharp
// データベース操作の例
public async Task<IEnumerable<T>> GetDataAsync<T>(string query)
{
    using var control = new SqliteDbControl(connectionString);
    return await control.SelectAsync<T>(query);
}
```

## トラブルシューティング

### ビルドエラーの対処

1. **NuGetパッケージの復元エラー**
   ```bash
   dotnet nuget locals all --clear
   dotnet restore --force
   ```

2. **ターゲットフレームワークの不一致**
   - プロジェクトファイルの`<TargetFramework>`を確認
   - 必要に応じて適切なSDKをインストール

3. **参照の循環**
   - プロジェクト間の依存関係を確認
   - 必要に応じてインターフェースを別プロジェクトに分離

## セキュリティとベストプラクティス

### 接続文字列の管理

- ハードコードされた接続文字列を避ける
- アプリケーション設定または環境変数を使用
- 例: `ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString`

### リソースの適切な解放

- `using`ステートメントまたは`using`宣言を必ず使用
- `TBirdObject`を継承する場合は適切なDisposeパターンに従う
- 非同期操作では`await using`を使用