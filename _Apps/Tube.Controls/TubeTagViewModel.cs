using Moviewer.Core.Controls;
using System.Windows.Input;

namespace Moviewer.Tube.Controls
{
	public class TubeTagViewModel : TagViewModel
	{
		public TubeTagViewModel(string tag) : base(tag)
		{

		}

		protected override ICommand CreateOnClickTag()
		{
			return base.CreateOnClickTag();
		}
	}
}