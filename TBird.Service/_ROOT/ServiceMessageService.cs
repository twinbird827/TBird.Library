using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Service
{
    public class ServiceMessageService : ConsoleMessageService
    {
        public ServiceMessageService(EventLog log)
        {
            _log = log;
        }
        private EventLog _log;

        public override void Debug(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Debug(message, callerMemberName, callerFilePath, callerLineNumber);
        }

        public override void Info(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Info(message, callerMemberName, callerFilePath, callerLineNumber);
        }

        public override void Error(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Error(message, callerMemberName, callerFilePath, callerLineNumber);
        }

        public override void Exception(Exception exception, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            base.Exception(exception, callerMemberName, callerFilePath, callerLineNumber);
        }
    }
}
