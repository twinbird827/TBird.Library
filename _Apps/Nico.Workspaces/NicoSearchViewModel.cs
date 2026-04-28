using Moviewer.Core;
using Moviewer.Core.Windows;
using Moviewer.Nico.Controls;
using Moviewer.Nico.Core;
using System.Threading;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Moviewer.Nico.Workspaces
{
	public class NicoSearchViewModel : WorkspaceViewModel, INicoSearchHistoryParentViewModel
	{
		public override MenuType Type => MenuType.NicoSearch;

		public NicoSearchViewModel(string word, NicoSearchType type) : this()
		{
			Word = word;
			OnSearch.Execute(type);
		}

		public NicoSearchViewModel()
		{
			Sources = new BindableCollection<NicoVideoModel>();

			Videos = Sources
				.ToBindableSelectCollection(x => new NicoVideoViewModel(this, x))
				.ToBindableContextCollection();

			Orderby = new ComboboxViewModel(ComboUtil.GetNicos("order_by"));
			Orderby.SelectedItem = Orderby.GetItemNotNull(NicoSetting.Instance.NicoSearchOrderby);

			Histories = NicoModel.Searches
				.ToBindableSortedCollection(x => x.Date, true)
				.ToBindableSelectCollection(x => new NicoSearchHistoryViewModel(this, x))
				.ToBindableContextCollection();

			AddDisposed((sender, e) =>
			{
				NicoSetting.Instance.NicoSearchOrderby = Orderby.SelectedItem.Value;
				NicoSetting.Instance.Save();

				Orderby.Dispose();
				Videos.Dispose();
			});
		}

		public ComboboxViewModel Orderby { get; private set; }

		public string Word
		{
			get => _Word;
			set => SetProperty(ref _Word, value);
		}
		public string _Word;

		public BindableCollection<NicoVideoModel> Sources { get; private set; }

		public BindableContextCollection<NicoVideoViewModel> Videos { get; private set; }

		public BindableContextCollection<NicoSearchHistoryViewModel> Histories { get; private set; }

		// 連続発火対策: 後発の OnSearch が走り出したら前回のループは離脱する。
		// HTTP 自体はキャンセルしない (既存と同等の握り潰し方針)。
		private int _searchGen;

		public ICommand OnSearch => _OnSearch = _OnSearch ?? RelayCommand.Create<NicoSearchType>(async t =>
		{
			var myGen = Interlocked.Increment(ref _searchGen);
			Sources.Clear();
			try
			{
				await foreach (var item in NicoUtil.GetVideoBySearchType(Word, t, Orderby.SelectedItem.Value))
				{
					if (Volatile.Read(ref _searchGen) != myGen) return;
					Sources.Add(item);
				}
			}
			catch { }

			if (Volatile.Read(ref _searchGen) == myGen) NicoModel.AddSearch(Word, t);
		});
		private ICommand _OnSearch;

		public void NicoSearchHistoryOnDoubleClick(NicoSearchHistoryViewModel vm)
		{
			Word = vm.Word;
			OnSearch.Execute(vm.Type);
		}

		public void NicoSearchHistoryOnDelete(NicoSearchHistoryViewModel vm)
		{
			NicoModel.DelSearch(vm.Word, vm.Type);
		}
	}
}