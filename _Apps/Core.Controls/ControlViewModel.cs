using Moviewer.Core.Windows;
using System.Windows.Input;
using TBird.Wpf;

namespace Moviewer.Core.Controls
{
	public class ControlViewModel : BindableBase
	{
		public ControlViewModel(ControlModel m)
		{
			if (m != null) Loaded.Add(m.OnLoadedModel);

			AddDisposed((sender, e) =>
			{
				Loaded.Dispose();
			});
		}

		public ICommand OnLoaded => _OnLoaded = _OnLoaded ?? RelayCommand.Create(async _ =>
		{
			if (ShowProgress) MainViewModel.Instance.ShowProgress = true;

			await Loaded.ExecuteAsync();
			Loaded.Dispose();

			if (ShowProgress) MainViewModel.Instance.ShowProgress = false;
		});
		private ICommand _OnLoaded;

		protected bool ShowProgress { get; set; } = false;

		protected BackgroundTaskManager Loaded { get; } = new BackgroundTaskManager();
	}
}