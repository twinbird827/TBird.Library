using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace TBird.Core
{
    public class ConsoleMessageService : IMessageService
    {
        public virtual void Error(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Console.WriteLine(GetString(MessageType.Error, message, callerMemberName, callerFilePath, callerLineNumber));
        }

        public virtual void Info(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Console.WriteLine(GetString(MessageType.Info, message, callerMemberName, callerFilePath, callerLineNumber));
        }

        public virtual void Debug(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
#if DEBUG
            Console.WriteLine(GetString(MessageType.Debug, message, callerMemberName, callerFilePath, callerLineNumber));
#else
            if (CoreSetting.Instance.IsDebug)
            {
                Console.WriteLine(GetString(MessageType.Debug, message, callerMemberName, callerFilePath, callerLineNumber));
            }
#endif
        }

        public virtual bool Confirm(string message,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Console.WriteLine(GetString(MessageType.Confirm, message, callerMemberName, callerFilePath, callerLineNumber));
            return true;
        }

        public virtual void Exception(Exception exception,
                [CallerMemberName] string callerMemberName = "",
                [CallerFilePath] string callerFilePath = "",
                [CallerLineNumber] int callerLineNumber = 0)
        {
            Console.WriteLine(GetString(MessageType.Exception, exception.ToString(), callerMemberName, callerFilePath, callerLineNumber));
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
                    Console.WriteLine(ex.ToString());
                }
            }
        }
        private static object _lock = new object();

    }
}
