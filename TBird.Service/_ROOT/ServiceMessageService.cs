using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
			WriteEntry(message, EventLogEntryType.Information);
		}

		public override void Error(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			base.Error(message, callerMemberName, callerFilePath, callerLineNumber);
			WriteEntry(message, EventLogEntryType.Error);
		}

		public override void Exception(Exception exception, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			base.Exception(exception, callerMemberName, callerFilePath, callerLineNumber);

			if (_lastex == null || _lastex.ToString() != exception.ToString())
			{
				WriteEntry((_lastex = exception).ToString(), EventLogEntryType.Error);
			}
		}

		private Exception _lastex;

		private void WriteEntry(string message, EventLogEntryType type)
		{
			// ﾃﾞﾊﾞｯｸﾞ実行中はｽｷｯﾌﾟ
			if (Environment.UserInteractive) return;
			// ｵﾌﾟｼｮﾝでInfoﾛｸﾞをｲﾍﾞﾝﾄﾛｸﾞに出力しない設定にしていたらｽｷｯﾌﾟ
			if (!ServiceSetting.Instance.WriteInformationEventLog && type == EventLogEntryType.Information) return;
			// ｲﾍﾞﾝﾄﾛｸﾞ書込み
			_log.WriteEntry(message, type);
		}
	}
}