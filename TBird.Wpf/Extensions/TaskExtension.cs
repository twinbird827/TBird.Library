using System;
using System.Threading.Tasks;

namespace TBird.Wpf
{
	public static class TaskExtension
	{
		public static Task ContinueOnUI<T>(this Task<T> task, Action<Task<T>> action)
		{
			return task.ContinueWith(x => WpfUtil.ExecuteOnUI(() => action(x)));
		}

		public static Task ContinueOnUI<T>(this Task<T> task, Func<Task<T>, Task> func)
		{
			return task.ContinueWith(x => WpfUtil.ExecuteOnUI(() => func(x)));
		}
	}
}