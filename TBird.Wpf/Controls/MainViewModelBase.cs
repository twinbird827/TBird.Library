using TBird.Core;

namespace TBird.Wpf.Controls
{
	public class MainViewModelBase : WindowViewModel
	{
		protected MainViewModelBase()
		{
			MessageService.SetService(new WpfMessageService());
		}
	}
}