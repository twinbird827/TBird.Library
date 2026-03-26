using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using Moviewer.Nico.Core;
using Moviewer.Tube.Controls;
using System;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Moviewer.Tube.Workspaces
{
	public class TubeHistoryViewModel : WorkspaceViewModel, IVideoParentViewModel
	{
		public override MenuType Type => MenuType.TubeHistory;

		public TubeHistoryViewModel()
		{
			DateCondition = new ComboboxViewModel(ComboUtil.GetViews("history_date_condition"));
			DateCondition.SelectedItem = DateCondition.GetItemNotNull(NicoSetting.Instance.NicoRankingGenre);

			Videos = VideoUtil.Histories
				.ToBindableWhereCollection(x => x.Mode == MenuMode.Youtube)
				.ToBindableSelectCollection(TubeVideoModel.FromHistory)
				.ToBindableSortedCollection(x => x.TempTime, isDescending: true)
				.ToBindableSelectCollection(x => new TubeVideoViewModel(this, x))
				.ToBindableContextCollection();

			AddDisposed((sender, e) =>
			{
				Videos.Dispose();
			});
		}

		public ComboboxViewModel DateCondition { get; private set; }

		public BindableContextCollection<TubeVideoViewModel> Videos
		{
			get => _Videos;
			set => SetProperty(ref _Videos, value);
		}
		public BindableContextCollection<TubeVideoViewModel> _Videos;

		public void DeleteOnVideo(VideoViewModel vm)
		{
			VideoUtil.DelHistory(MenuMode.Youtube, vm.Source.ContentId);
		}

		private bool ShowHistory(VideoHistoryModel m)
		{
			if (m.Mode != MenuMode.Youtube) return false;

			var condition = DateCondition.SelectedItem;

			if (condition == null) return true;

			if (int.TryParse(condition.Value, out int dateint))
			{
				return DateTime.Now < m.Date.AddDays(dateint);
			}
			else
			{
				return true;
			}
		}

	}
}