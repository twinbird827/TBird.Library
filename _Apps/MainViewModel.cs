using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TBird.Core;
using TBird.Web;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;

namespace Netkeiba
{
	public class MainViewModel : MainViewModelBase
	{
		public MainViewModel()
		{
			Basyos = BasyoSources.ToBindableContextCollection();

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

			EYear = DateTime.Now.Year;
			SYear = EYear - 10;
		}

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

		public CheckboxItemModel Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };

		public IRelayCommand S1EXEC => RelayCommand.Create(async _ =>
		{
			var years = Enumerable.Range(SYear, EYear - SYear).Select(i => i.ToString(2)).ToArray();
			var basyos = BasyoSources.Where(x => x.IsChecked).ToArray();
			var counts = Enumerable.Range(1, 6).Select(i => i.ToString(2)).ToArray();
			var races = Enumerable.Range(1, 12).Select(i => i.ToString(2)).ToArray();

			var targets = years.SelectMany(y => basyos.SelectMany(b => counts.SelectMany(c => races.Select(r => new { y, b, c, r })))).ToArray();

			foreach (var t in targets)
			{
				// raceid = year + basyo + count + race
				var raceid = $"{t.y}{t.b.Value}{t.c}{t.r}";

				var raceurl = $"https://db.netkeiba.com/race/{raceid}";

				var raceurlres = await WebUtil.GetStringAsync(raceurl);

				Clipboard.SetText(raceurlres);
			}

			MessageService.Info("Push!!");
		});
	}
}