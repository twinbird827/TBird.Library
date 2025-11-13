using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;
using Tensorflow;

namespace Netkeiba
{
	public partial class MainViewModel : MainViewModelBase
	{
		public MainViewModel()
		{
			_this = this;

			Basyos = BasyoSources.ToBindableContextCollection();

			Logs = LogSource.ToBindableContextCollection();

			BasyoSources.Add(new CheckboxItemModel("01", "札幌"));
			BasyoSources.Add(new CheckboxItemModel("02", "函館"));
			BasyoSources.Add(new CheckboxItemModel("03", "福島"));
			BasyoSources.Add(new CheckboxItemModel("04", "新潟"));
			BasyoSources.Add(new CheckboxItemModel("05", "東京"));
			BasyoSources.Add(new CheckboxItemModel("06", "中山"));
			BasyoSources.Add(new CheckboxItemModel("07", "中京"));
			BasyoSources.Add(new CheckboxItemModel("08", "京都"));
			BasyoSources.Add(new CheckboxItemModel("09", "阪神"));
			BasyoSources.Add(new CheckboxItemModel("10", "小倉"));
			BasyoSources.ForEach(x => x.IsChecked = true);

			CreateModels = CreateModelSources.ToBindableContextCollection();

			CreateModelSources.AddRange(AppUtil.ﾗﾝｸ2Arr.Select(rank => new TreeCheckboxViewModel(new CheckboxItemModel(rank, rank))));

			EYear = DateTime.Now.Year;
			SYear = EYear;

			S4RoundItems = S4RoundItemSources.ToBindableContextCollection();

			S4ResultItems = S4ResultItemSources.ToBindableContextCollection();

			S4Dates.AddOnPropertyChanged(this, async (sender, e) =>
			{
				if (string.IsNullOrWhiteSpace(S4Dates.SelectedItem.Value)) return;

				var basyos = new Dictionary<string, string>()
				{
					{ "01", "札幌" },
					{ "02", "函館" },
					{ "03", "福島" },
					{ "04", "新潟" },
					{ "05", "東京" },
					{ "06", "中山" },
					{ "07", "中京" },
					{ "08", "京都" },
					{ "09", "阪神" },
					{ "10", "小倉" },
				};

				var arr = await NetkeibaGetter.GetCurrentRaceIds(DateTime.ParseExact(S4Dates.SelectedItem.Value, "yyyyMMdd", null));

				S4RoundHeader.TryDispose();
				S4RoundHeader = new UniformViewModel(arr.Select(x => x.Mid(4, 2)).Distinct().Select(x => new ComboboxItemModel(x, basyos[x])));

				string GetBasyoRound(string basyo, int i) => arr.First(x => x.Mid(4, 2) == basyo && x.EndsWith(i.ToString(2)));

				S4RoundItemSources.Clear();
				foreach (var i in Enumerable.Range(1, 12))
				{
					S4RoundItemSources.Add(new UniformViewModel<STEP4RoundItem>(S4RoundHeader.ColumnsSource
						.Select(x => GetBasyoRound(x.Value, i))
						.Select(x => new STEP4RoundItem(x))
					));
				}
			});

			AppSetting.Instance.Save();
		}

		public ProgressViewModel Progress { get; } = new ProgressViewModel();

		private static MainViewModel? _this { get; set; }

		public static void AddLog(string message)
		{
			if (_this == null) return;

			if (_logmax < _this.LogSource.Count)
			{
				_this.LogSource.RemoveAt(_logmax);
			}
			_this.LogSource.Insert(0, new ComboboxItemModel(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), message));

			MessageService.Debug(message);
			MessageService.AppendLogfile(message);
		}

		private const int _logmax = 1024;

		public IRelayCommand ClickSetting { get; } = RelayCommand.Create(_ =>
		{
			using (var vm = new ModelViewModel())
			{
				vm.Show(() => new ModelWindow());
			}
		});

		public BindableCollection<ComboboxItemModel> LogSource { get; } = new BindableCollection<ComboboxItemModel>();

		public BindableContextCollection<ComboboxItemModel> Logs { get; }

		public BindableCollection<CheckboxItemModel> BasyoSources { get; } = new BindableCollection<CheckboxItemModel>();

		public BindableContextCollection<CheckboxItemModel> Basyos { get; }

		public int SYear
		{
			get => _SYear;
			set => SetProperty(ref _SYear, value);
		}
		private int _SYear;

		public int EYear
		{
			get => _EYear;
			set => SetProperty(ref _EYear, value);
		}
		private int _EYear;

		public CheckboxItemModel S1Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S1EXEC => new STEP1Command(this).CreateCommand();

		public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S2EXEC => new STEP2Command(this).CreateCommand();

		public IRelayCommand S3EXEC => new STEP3Command(this).CreateCommand();

		public IRelayCommand S4EXEC => new STEP4Command(this).CreateCommand();

		public string S4Text
		{
			get => _S4Text;
			set => SetProperty(ref _S4Text, value);
		}
		private string _S4Text = string.Empty;

		public ComboboxViewModel S4Dates
		{
			get => _S4Dates;
			set => SetProperty(ref _S4Dates, value);
		}
		private ComboboxViewModel _S4Dates = new(Enumerable.Empty<ComboboxItemModel>());

		public IRelayCommand S4UPDATELIST => new STEP4UpdateListCommand(this).CreateCommand();

		public IRelayCommand S3EXECPREDICT => new STEP1OikiriCommad(this).CreateCommand();

		public UniformViewModel S4RoundHeader
		{
			get => _S4RoundHeader;
			set => SetProperty(ref _S4RoundHeader, value);
		}
		private UniformViewModel _S4RoundHeader;

		public BindableCollection<UniformViewModel<STEP4RoundItem>> S4RoundItemSources { get; } = new BindableCollection<UniformViewModel<STEP4RoundItem>>();

		public BindableContextCollection<UniformViewModel<STEP4RoundItem>> S4RoundItems { get; }

		public string S4ResultHeader
		{
			get => _S4ResultHeader;
			set => SetProperty(ref _S4ResultHeader, value);
		}
		private string _S4ResultHeader;

		public static void SetS4ResultHeader(string message)
		{
			if (_this == null) return;

			_this.S4ResultHeader = message;
		}

		public BindableCollection<STEP4ResultItem> S4ResultItemSources { get; } = new BindableCollection<STEP4ResultItem>();

		public BindableContextCollection<STEP4ResultItem> S4ResultItems { get; }

		public static void SetS4ResultItems(IEnumerable<STEP4ResultItem> items)
		{
			if (_this == null) return;

			_this.S4ResultItemSources.Clear();
			_this.S4ResultItemSources.AddRange(items);
		}

		//public IRelayCommand S3EXECPREDICT => RelayCommand.Create(async _ =>
		//{
		//	var raceparser = await AppUtil.GetDocument(false, "https://race.netkeiba.com/race/shutuba.html?race_id=202505040202");

		//	var racetable = raceparser.GetElementsByClassName("Shutuba_Table RaceTable01 ShutubaTable").FirstOrDefault() as IHtmlTableElement;

		//	if (racetable == null) return;

		//	foreach (var row in racetable.Rows.Skip(2))
		//	{
		//		if (row == null) continue;

		//		AddLog(row.ToString().NotNull());
		//	}
		//});

	}
}