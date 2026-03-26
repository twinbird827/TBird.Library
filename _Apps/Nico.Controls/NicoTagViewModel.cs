using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using Moviewer.Nico.Core;
using Moviewer.Nico.Workspaces;
using System.Windows.Input;
using TBird.Wpf;

namespace Moviewer.Nico.Controls
{
	public class NicoTagViewModel : TagViewModel
	{
		public NicoTagViewModel(string tag) : base(tag)
		{

		}

		protected override ICommand CreateOnClickTag()
		{
			return RelayCommand.Create(_ =>
			{
				MainViewModel.Instance.Current = new NicoSearchViewModel(Tag, NicoSearchType.Tag);
			});
		}
	}
}