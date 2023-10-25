using System.Windows;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Core.Windows
{
	public abstract class WorkspaceViewModel : BindableBase
	{
		public abstract MenuType Type { get; }

		public ICommand OnLoaded => _OnLoaded = _OnLoaded ?? RelayCommand.Create(async _ =>
		{
			MainViewModel.Instance.ShowProgress = true;

			await Loaded.ExecuteAsync();

			MainViewModel.Instance.ShowProgress = false;
		});
		private ICommand _OnLoaded;

		public BackgroundTaskManager Loaded { get; } = new BackgroundTaskManager();

		public string Title => $"Moviewer [{Type.GetLabel()}]";

		public ICommand OnDrop =>
			_OnDrop = _OnDrop ?? RelayCommand.Create<DragEventArgs>(OnDropProcess);
		private ICommand _OnDrop;

		protected virtual void OnDropProcess(DragEventArgs e)
		{
			if (e.Data.GetData(DataFormats.Text) is string droptxt)
			{
				OnDropProcess(droptxt);
			}
		}

		protected virtual void OnDropProcess(string droptxt)
		{

		}

	}
}