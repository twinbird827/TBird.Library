using System.Runtime.CompilerServices;
using TBird.Core;

namespace Netkeiba.Scheduler
{
	public class SchedulerMessageService : ConsoleMessageService
	{
		public override void Debug(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			MessageService.AppendLogfile(message);
			base.Debug(message, callerMemberName, callerFilePath, callerLineNumber);
		}
	}
}
