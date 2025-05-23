using TBird.Core;
using TBird.Wpf.Controls;

namespace TBird.Wpf
{
	public class TaskViewModel<T> : DialogViewModel
	{
		public TaskViewModel(TaskManager<T> manager, T parameter)
		{
			Loaded.Add(() => manager.ExecuteAsync(parameter));

			Loaded.Add(() => DialogResult = true);
		}
	}
}