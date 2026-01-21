using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using TBird.Core;
using TBird.Wpf.Reports;

namespace Netkeiba
{
	public class ReportItemViewModel : ReportViewModel
	{
		public ReportItemViewModel()
		{
			IsGUI = false;
			Landscape = false;
		}

		public ReportItemViewModel(string title, IEnumerable<STEP4ResultItem> items) : this()
		{
			Title = title;
			Items.AddRange(items);
		}

		public string Title { get; private set; } = string.Empty;

		public ObservableCollection<STEP4ResultItem> Items { get; } = new();

		protected override string GetPdfFilename()
		{
			return $"{Title}_{base.GetPdfFilename()}";
		}

		protected override UserControl GetWindow()
		{
			return new ReportItemControl();
		}

		protected override Task Initialize()
		{
			Total = 1;
			Page = 1;

			return Task.CompletedTask;
		}

		protected override void OnPage(int index)
		{

		}
	}
}