using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Wpf;

namespace Netkeiba
{
	public abstract class STEPBase : TBirdObject
	{
		protected STEPBase(MainViewModel vm)
		{
			VM = vm;
		}

		protected MainViewModel VM { get; }

		protected ProgressViewModel Progress => VM.Progress;

		public IRelayCommand CreateCommand()
		{
			return RelayCommand.Create(ActionAsync, Predicate);
		}

		protected abstract Task ActionAsync(object dummy);

		protected virtual bool Predicate(object dummy)
		{
			return true;
		}

		protected void AddLog(string message)
		{
			MainViewModel.AddLog(message);
		}
	}
}