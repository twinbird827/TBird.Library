using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using Moviewer.Nico.Controls;
using Moviewer.Nico.Core;
using System;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Moviewer.Nico.Workspaces
{
	public class NicoHistoryViewModel : WorkspaceViewModel, IVideoParentViewModel
	{
		public override MenuType Type => MenuType.NicoHistory;

		public NicoHistoryViewModel()
		{
			DateCondition = new ComboboxViewModel(ComboUtil.GetViews("history_date_condition"));
			DateCondition.SelectedItem = DateCondition.GetItemNotNull(NicoSetting.Instance.NicoRankingGenre);

			Videos = VideoUtil.Histories
				.ToBindableWhereCollection(ShowHistory)
				.ToBindableSelectCollection(NicoVideoModel.FromHistory)
				.ToBindableSortedCollection(x => x.TempTime, isDescending: true)
				.ToBindableSelectCollection(x => new NicoVideoViewModel(this, x))
				.ToBindableContextCollection();

			AddDisposed((sender, e) =>
			{
				Videos.Dispose();
			});
		}

		public ComboboxViewModel DateCondition { get; private set; }

		public BindableContextCollection<NicoVideoViewModel> Videos
		{
			get => _Videos;
			set => SetProperty(ref _Videos, value);
		}
		public BindableContextCollection<NicoVideoViewModel> _Videos;

		public void DeleteOnVideo(VideoViewModel vm)
		{
			VideoUtil.DelHistory(MenuMode.Niconico, vm.Source.ContentId);
		}

		private bool ShowHistory(VideoHistoryModel m)
		{
			if (m.Mode != MenuMode.Niconico) return false;

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