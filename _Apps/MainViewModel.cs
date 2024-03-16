using AngleSharp.Html.Parser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using TBird.Core;
using TBird.DB.SQLite;
using TBird.Web;
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
				.SelectMany(x => Arr(1, 2, 3, 6, 7, 8).Select(i => $"B-{x}-{i}"))
				.Select(x => new CheckboxItemModel(x, x) { IsChecked = true })
			);
			CreateModelSources.AddRange(Arr("RANK1", "RANK2", "RANK3", "RANK4", "RANK5")
				.SelectMany(x => Arr(1).Select(i => $"R-{x}-{i}"))
				.Select(x => new CheckboxItemModel(x, x) { IsChecked = true })
			);

			EYear = DateTime.Now.Year;
			SYear = EYear - 1;

			AppSetting.Instance.Save();
		}

		public ProgressViewModel Progress { get; } = new ProgressViewModel();

		private static MainViewModel _this { get; set; }

		public static void AddLog(string message)
		{
			if (_logmax < _this.LogSource.Count)
			{
                _this.LogSource.RemoveAt(_logmax);
			}
            _this.LogSource.Insert(0, new ComboboxItemModel(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), message));

			MessageService.AppendLogfile(message);
		}

		private const int _logmax = 1024;

		private readonly string _sqlitepath = Path.Combine(@"database", "database.sqlite3");

		private SQLiteControl CreateSQLiteControl() => new SQLiteControl(_sqlitepath, string.Empty, false, false, 1024 * 1024, true);

		public IRelayCommand ClickSetting { get; } = RelayCommand.Create(_ =>
		{
			using (var vm = new ModelViewModel())
			{
				vm.ShowDialog(() => new ModelWindow());
			}
		});
	}
}