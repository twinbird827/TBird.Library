using Moviewer.Core;
using Moviewer.Core.Windows;
using Moviewer.Nico.Controls;
using Moviewer.Nico.Core;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Moviewer.Nico.Workspaces
{
	public class NicoFavoriteViewModel : WorkspaceViewModel, INicoSearchHistoryParentViewModel
	{
		public override MenuType Type => MenuType.NicoFavorite;

		public NicoFavoriteViewModel()
		{
			Sources = new BindableCollection<NicoVideoModel>();

			Videos = Sources
				.ToBindableSelectCollection(x => new NicoVideoViewModel(this, x))
				.ToBindableContextCollection();

			Orderby = new ComboboxViewModel(ComboUtil.GetNicos("order_by"));
			Orderby.SelectedItem = Orderby.GetItemNotNull(NicoSetting.Instance.NicoFavoriteOrderby);

			Favorites = NicoModel.Favorites
				.ToBindableSortedCollection(x => x.Date, true)
				.ToBindableSelectCollection(x => new NicoSearchHistoryViewModel(this, x))
				.ToBindableContextCollection();

			AddDisposed((sender, e) =>
			{
				NicoSetting.Instance.NicoFavoriteOrderby = Orderby.SelectedItem.Value;
				NicoSetting.Instance.Save();

				Orderby.Dispose();
				Videos.Dispose();
			});
		}

		public ComboboxViewModel Orderby { get; private set; }

		public BindableCollection<NicoVideoModel> Sources { get; private set; }

		public BindableContextCollection<NicoVideoViewModel> Videos { get; private set; }

		public BindableContextCollection<NicoSearchHistoryViewModel> Favorites { get; private set; }

		public ICommand OnSearch => _OnSearch = _OnSearch ?? RelayCommand.Create<NicoSearchHistoryViewModel>(async vm =>
		{
			await NicoUtil.GetVideoBySearchType(vm.Word, vm.Type, Orderby.SelectedItem.Value).ContinueWith(x =>
			{
				Sources.Clear();
				Sources.AddRange(x.Result);
			});
		});
		private ICommand _OnSearch;

		public void NicoSearchHistoryOnDoubleClick(NicoSearchHistoryViewModel vm)
		{
			OnSearch.Execute(vm);
		}

		public void NicoSearchHistoryOnDelete(NicoSearchHistoryViewModel vm)
		{
			NicoModel.DelFavorite(vm.Word, vm.Type);
		}
	}
}