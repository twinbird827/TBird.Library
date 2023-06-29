using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace TBird.Core
{
    public class ConsoleMessageService : IMessageService
    {
        public ConsoleMessageService()
        {
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));
        }

        public virtual void Error(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Writeline(GetString(MessageType.Error, message, callerMemberName, callerFilePath, callerLineNumber));
        }

        public virtual void Info(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Writeline(GetString(MessageType.Info, message, callerMemberName, callerFilePath, callerLineNumber));
        }

        public virtual void Debug(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
#if DEBUG
            Writeline(GetString(MessageType.Debug, message, callerMemberName, callerFilePath, callerLineNumber));
#else
            if (CoreSetting.Instance.IsDebug)
            {
                Writeline(GetString(MessageType.Debug, message, callerMemberName, callerFilePath, callerLineNumber));
            }
#endif
        }

        public virtual bool Confirm(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Writeline(GetString(MessageType.Confirm, message, callerMemberName, callerFilePath, callerLineNumber));
            return true;
        }

        public virtual void Exception(Exception exception,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Writeline(GetString(MessageType.Exception, exception.ToString(), callerMemberName, callerFilePath, callerLineNumber));
        }

        private string GetString(MessageType type,
                string message,
                string callerMemberName,
                string callerFilePath,
                int callerLineNumber)
        {
            var txt = $"[{type}][{DateTime.Now.ToString("yy/MM/dd HH:mm:ss.fff")}][{callerFilePath}][{callerMemberName}][{callerLineNumber}]\n{message}\n";

            switch (type)
            {
                case MessageType.Error:
                case MessageType.Exception:
                    AppendLogfile(txt);
                    break;
            }

            return txt;
        }

        private void AppendLogfile(string message)
        {
            lock (_lock)
            {
                var dir = FileUtil.RelativePathToAbsolutePath("log");
                var tmp = Path.Combine(dir, $"{DateTime.Now.ToString("yyyy-MM-dd")}.log");

                // ﾃﾞｨﾚｸﾄﾘを作成
                Directory.CreateDirectory(dir);

                try
                {
                    File.AppendAllText(tmp, message);
                }
                catch (Exception ex)
                {
                    Writeline(ex.ToString());
                }
            }
        }

        private static object _lock = new object();

        private void Writeline(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}