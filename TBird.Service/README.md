# TBird.Service

## 概要

このライブラリは、Windowsサービスを実装するためのヘルパーです。

## TBird.Service.ServiceManager

Windowsサービスで実行する処理を実装します。
最小の実装例は以下の通りです。

```csharp
using System;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Service;

public class MyService : ServiceManager
{
    public MyService()
    {

    }

    protected override Task<bool> StartProcess()
    {
        ServiceFactory.MessageService.Info("開始処理");
        return ToStartResult(true);
    }

    protected override async Task TickProcess()
    {
        ServiceFactory.MessageService.Info("B:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        await Task.Delay(new Random().Next(100, 900));
        ServiceFactory.MessageService.Info("E:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
    }

    protected override void StopProcess()
    {
        ServiceFactory.MessageService.Info("停止処理");
    }
}
```

- `TickProcess()` は必ず継承する必要があります。  
- `StartProcess()` と `StopProcess()` は必要に応じて省略可能です。  

## TBird.Service.ServiceRunner

Windowsサービスとして登録するためのヘルパーです。
本ライブラリを使用する場合、以下のようなクラスを作成する必要があります。

```csharp
using System.ComponentModel;
using TBird.Service;

[RunInstaller(true)]
public class MyInstaller : ServiceRunner
{

}
```

## 実装したクラスの実行

上記2つのクラスを実装したら、以下の通り実行できます。

```csharp
using System;
using TBird.Service;

class Program
{
    static void Main(string[] args)
    {
        ServiceRunner.Run(new MyService(), args);
    }
}
```

- 実行時引数に `"/i"` オプションを指定することでサービス登録が可能です。
- 実行時引数に `"/u"` オプションを指定することでサービス解除が可能です。
- 実行時引数に何も指定しないことでコンソール実行が可能です。

## TBird.Service.ServiceSetting

Windowsサービスとして登録するための設定情報を管理します。
本ライブラリを使用する場合、以下の設定値を編集してください。

| プロパティ名 | 設定値の概要 |
|:------------|:------------|
| Interval    | `TickProcess()` を実行する間隔(ms)を設定します。デフォルトは1000msです。 |
| ServiceName | サービスを一意に識別するための名前を設定します。タスクマネージャ->サービスタブ->名前欄に表示されます。 |
| DisplayName | サービスの表示名を設定します。タスクマネージャ->サービスタブ->説明欄、及び、サービス管理画面->名前欄に表示されます。 |
| Description | サービスの概要を設定します。サービス管理画面->説明欄に表示されます。 |
| StartType   | スタートアップの種類を設定します。設定は `System.ServiceProcess.ServiceStartMode` 列挙型で設定します。 |
| Username    | サービスを実行するユーザを設定します。 |
| Account     | 実行時の権限を設定します。設定は `System.ServiceProcess.ServiceAccount` 列挙型で設定します。 |
| WriteInformationEventLog | サービス実行時に、Informationログをイベントログに出力するかどうかを設定します。 |

