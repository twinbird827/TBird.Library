using System.Runtime.CompilerServices;
using TBird.Core;

namespace TBird.Maui;

/// <summary>
/// ConsoleMessageService を継承し、Android/MAUI 環境向けに出力を logcat と
/// FileSystem.AppDataDirectory/log/yyyy-MM-dd.log に振り分ける実装。
///
/// 全 5 メソッド (Error / Exception / Info / Warn / Debug) を override する。
/// ConsoleMessageService の Writeline 経路 (Trace.WriteLine) は Android logcat に
/// 転送されないため、明示的に System.Diagnostics.Debug.WriteLine を呼ぶ必要がある。
///
/// GetString は base 実装を再利用し、[appName] プレフィックスを付加する形に override する。
/// MessageService.AppendLogfile は AppDomain.CurrentDomain.BaseDirectory 相対パスを使うため
/// Android サンドボックスで書込権限エラーが出る。代わりに FileSystem.AppDataDirectory/log に書く。
/// </summary>
public class MauiMessageService : ConsoleMessageService
{
    private readonly string _appName;

    public MauiMessageService(string appName)
    {
        _appName = appName;
    }

    // base.GetString は §1.5.4 で純粋関数化されているため副作用なし。
    // base のフォーマット [Level][datetime][file][member][line]\n{message} の先頭に
    // [appName] プレフィックスを付加して [LanobeReader][Level]... の形にする。
    protected override string GetString(MessageType type,
            string message,
            string callerMemberName,
            string callerFilePath,
            int callerLineNumber)
    {
        var baseLine = base.GetString(type, message, callerMemberName, callerFilePath, callerLineNumber);
        return $"[{_appName}]{baseLine}";
    }

    // Error / Exception: base 実装は Writeline (= Trace.WriteLine) +
    // MessageService.AppendLogfile を呼ぶが、Android では Trace.WriteLine が logcat に
    // 出ない & AppendLogfile が AppDomain.CurrentDomain.BaseDirectory 相対で
    // サンドボックス書込権限エラーを起こす。base を呼ばずに同等出力を組み立てる。
    public override void Error(string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var line = GetString(MessageType.Error, message, callerMemberName, callerFilePath, callerLineNumber);
        System.Diagnostics.Debug.WriteLine(line);
        TryAppendToAppData(line);
    }

    public override void Exception(System.Exception exception,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var line = GetString(MessageType.Exception, exception.ToString(), callerMemberName, callerFilePath, callerLineNumber);
        System.Diagnostics.Debug.WriteLine(line);
        TryAppendToAppData(line);
    }

    // Info / Warn / Debug: base.* は Trace.WriteLine を呼ぶが、Android では
    // ConsoleMessageService の TextWriterTraceListener (Console.Out) が logcat に
    // 転送されない。明示的に Debug.WriteLine で logcat 出力を担保する。
    // ファイル書込は行わない（Error/Exception のみ AppData に書く）。
    public override void Info(string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        System.Diagnostics.Debug.WriteLine(GetString(MessageType.Info, message, callerMemberName, callerFilePath, callerLineNumber));
    }

    public override void Warn(string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        System.Diagnostics.Debug.WriteLine(GetString(MessageType.Warn, message, callerMemberName, callerFilePath, callerLineNumber));
    }

    public override void Debug(string message,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(GetString(MessageType.Debug, message, callerMemberName, callerFilePath, callerLineNumber));
#else
        if (CoreSetting.Instance.IsDebug)
        {
            System.Diagnostics.Debug.WriteLine(GetString(MessageType.Debug, message, callerMemberName, callerFilePath, callerLineNumber));
        }
#endif
    }

    // Confirm: Shell.Current.DisplayAlert は async Task<bool>。同期 bool を返す
    // IMessageService.Confirm を素直にサポートできないため、base 実装 (true 固定) のまま。
    // 将来必要になったら IMessageService に ConfirmAsync を追加する設計判断が必要。

    private void TryAppendToAppData(string line)
    {
        try
        {
            var dir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "log");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"{System.DateTime.Now:yyyy-MM-dd}.log");
            System.IO.File.AppendAllText(path, line + "\n");
        }
        catch
        {
            // ログ書込失敗自体は飲み込む（logcat 出力は既に出ているため致命的でない）
        }
    }
}
