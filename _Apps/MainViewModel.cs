using System;
using System.Linq;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;

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

			CreateModelSources.AddRange(Arr("RANK1", "RANK2", "RANK3", "RANK4", "RANK5")
				.SelectMany(x => Arr(1, 2, 6, 7).Select(i => $"B-{x}-{i}"))
				.Select(x => new CheckboxItemModel(x, x) { IsChecked = true })
			);
			CreateModelSources.AddRange(Arr("RANK1", "RANK2", "RANK3", "RANK4", "RANK5")
				.SelectMany(x => Arr(1).Select(i => $"R-{x}-{i}"))
				.Select(x => new CheckboxItemModel(x, x) { IsChecked = true })
			);
			//CreateModelSources.AddRange(Arr("RANK1", "RANK2", "RANK3", "RANK4", "RANK5")
			//	.SelectMany(x => Arr(1).Select(i => $"M-{x}-{i}"))
			//	.Select(x => new CheckboxItemModel(x, x) { IsChecked = true })
			//);

			EYear = DateTime.Now.Year;
			SYear = EYear;

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
	}
}