using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using Moviewer.Nico.Core;
using Moviewer.Nico.Workspaces;
using System.Threading.Tasks;

namespace Moviewer.Nico.Controls
{
	public class NicoUserViewModel : UserViewModel
	{
		public NicoUserViewModel()
		{

		}

		protected override void OnClickUsernameCommand(object _)
		{
			MainViewModel.Instance.Current = new NicoSearchViewModel(Userid, NicoSearchType.User);
		}

		public override Task SetThumbnail(string url)
		{
			return this.SetThumbnail(Userid, url, NicoUtil.NicoBlankUserUrl);
		}
	}
}