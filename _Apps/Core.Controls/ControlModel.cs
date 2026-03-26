using System.Threading.Tasks;
using TBird.Wpf;

namespace Moviewer.Core.Controls
{
	public class ControlModel : BindableBase
	{
		public Task OnLoadedModel()
		{
			if (_loaded) return Task.CompletedTask;

			_loaded = true;
			return OnLoaded();
		}

		private bool _loaded = false;

		protected virtual Task OnLoaded()
		{
			return Task.CompletedTask;
		}

	}
}