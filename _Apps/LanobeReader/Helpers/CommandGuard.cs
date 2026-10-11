using System.Runtime.CompilerServices;
using TBird.Core;

namespace LanobeReader.Helpers;

public static class CommandGuard
{
    public static async Task RunAsync(
        Func<Task> body,
        Action<string> setError,
        string userMessage,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            // AsyncRelayCommand は例外を UI スレッドへ再送出してプロセスを落とすため、ここで止める。
            MessageService.Error($"{operationName} failed: {ex.Message}", operationName, filePath, lineNumber);
            setError(userMessage);
        }
    }
}
