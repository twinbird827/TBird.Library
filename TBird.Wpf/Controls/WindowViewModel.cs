using System.ComponentModel;
using System.Windows.Input;

namespace TBird.Wpf.Controls
{
	public class WindowViewModel : BindableBase
	{
		public ICommand OnLoaded => _OnLoaded = _OnLoaded ?? RelayCommand.Create(async _ =>
		{
			ShowProgress = true;

			await Loaded.ExecuteAsync();

			ShowProgress = false;
		});
		private ICommand _OnLoaded;

		public BackgroundTaskManager Loaded { get; } = new BackgroundTaskManager();

		public ICommand OnClosing => _OnClosing = _OnClosing ?? RelayCommand.Create<CancelEventArgs>(e =>
		{
			ShowProgress = true;

			Closing.Execute(e);

			ShowProgress = false;
		});
		private ICommand _OnClosing;

		public BackgroundTaskManager<CancelEventArgs> Closing { get; } = new BackgroundTaskManager<CancelEventArgs>();

		/// <summary>
		/// ﾀﾞｲｱﾛｸﾞ結果
		/// </summary>
		public bool ShowProgress
		{
			get => _ShowProgress;
			set => SetProperty(ref _ShowProgress, value);
		}
		private bool _ShowProgress;
	}
}