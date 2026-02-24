using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TBird.Wpf.Controls;

namespace Netkeiba
{
	public class MainViewService : WpfMessageService
	{
		public override void Debug(string message, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
		{
			MainViewModel.AddLog(message);
			base.Debug(message, callerMemberName, callerFilePath, callerLineNumber);
		}
	}
}