using Browser.Pages;
using System.Runtime.CompilerServices;
using TBird.Core;

namespace Browser.Models
{
	public class RazorMessageService : ConsoleMessageService
	{
		private ILogger<RaceModel> _logger;

		public RazorMessageService(ILogger<RaceModel> logger)
		{
			_logger = logger;
		}

		public override void Debug(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			_logger.LogDebug(message);
			base.Debug(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		public override void Info(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			_logger.LogInformation(message);
			base.Info(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		public override void Error(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			_logger.LogError(message);
			base.Error(message, callerMemberName, callerFilePath, callerLineNumber);
		}

		public override void Exception(Exception exception, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			_logger.LogError(exception, exception.Message);
			base.Exception(exception, callerMemberName, callerFilePath, callerLineNumber);
		}
	}
}