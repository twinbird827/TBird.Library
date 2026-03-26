using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using System;
using System.Windows.Input;

namespace Moviewer.Tube.Controls
{
	public class TubeVideoViewModel : VideoViewModel
	{
		public TubeVideoViewModel(WorkspaceViewModel parent, TubeVideoModel m) : base(m)
		{
			Parent = parent;

			AddOnPropertyChanged(this, (sender, e) =>
			{
				ContentUrl = $"https://www.youtube.com/watch?v={ContentId}";
			}, nameof(ContentId), true);

			AddDisposed((sender, e) =>
			{
				Parent = null;
			});
		}

		public WorkspaceViewModel Parent { get; private set; }

		protected override UserViewModel CreateUserInfo()
		{
			return new TubeUserViewModel();
		}

		protected override TagViewModel CreateTagViewModel(string tag)
		{
			return new TubeTagViewModel(tag);
		}

		protected override void DoubleClickCommand(object parameter)
		{
			//// TODO 子画面出して追加するかどうかを決めたい
			//// TODO ﾘﾝｸも抽出したい
			//// TODO smだけじゃなくてsoとかも抽出したい
			//foreach (var videoid in Regex.Matches(Description, @"(?<id>sm[\d]+)").OfType<Match>()
			//        .Select(m => m.Groups["id"].Value)
			//        .Where(tmp => VideoUtil.Histories.GetModel(Source) == null)
			//    )
			//{
			//    VideoUtil.AddTemporary(Source.Mode, videoid);
			//}

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
			throw new NotImplementedException();
			//return new NicoDownloadModel(Source as NicoVideoModel);
		}

	}
}