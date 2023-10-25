using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using Moviewer.Nico.Core;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Moviewer.Nico.Controls
{
	public class NicoVideoViewModel : VideoViewModel
	{
		public NicoVideoViewModel(WorkspaceViewModel parent, NicoVideoModel m) : base(m)
		{
			Parent = parent;

			AddOnPropertyChanged(this, (sender, e) =>
			{
				ContentUrl = $"http://nico.ms/{ContentId}";
			}, nameof(ContentId), true);

			AddDisposed((sender, e) =>
			{
				Parent = null;
			});
		}

		public WorkspaceViewModel Parent { get; private set; }

		public override Task SetThumbnail(string url)
		{
			return this.SetThumbnail(
				ContentId,
				Arr(".L", ".M", "").Select(x => $"{url}{x}").ToArray()
			);
		}

		protected override UserViewModel CreateUserInfo()
		{
			return new NicoUserViewModel();
		}

		protected override TagViewModel CreateTagViewModel(string tag)
		{
			return new NicoTagViewModel(tag);
		}

		protected override void DoubleClickCommand(object parameter)
		{
            // TODO 子画面出して追加するかどうかを決めたい
            // TODO ﾘﾝｸも抽出したい
            // TODO smだけじゃなくてsoとかも抽出したい
            foreach (var videoid in Regex.Matches(Description, @"(?<id>sm[\d]+)").OfType<Match>()
                    .Select(m => m.Groups["id"].Value)
                    .Where(tmp => VideoUtil.Histories.GetModel(Source.Mode, tmp) == null)
				)
            {
                VideoUtil.AddTemporary(Source.Mode, videoid);
			}

			base.DoubleClickCommand(parameter);
		}

		protected override void KeyDownCommand(KeyEventArgs e)
		{
			base.KeyDownCommand(e);

			//else if (e.Key == Key.Delete && Parent is INicoVideoParentViewModel parent)
			//{
			//    parent.NicoVideoOnDelete(this);
			//}
		}

		protected override DownloadModel GetDownloadModel()
		{
			return new NicoDownloadModel(Source as NicoVideoModel);
		}
	}
}