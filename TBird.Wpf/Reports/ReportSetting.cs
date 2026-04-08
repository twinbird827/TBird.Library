using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf.Reports
{
	public class ReportSetting : JsonBase<ReportSetting>
	{
		public static ReportSetting Instance { get; } = new ReportSetting();

		public ReportSetting(string path) : base(path)
		{
			if (!Load())
			{
				PrinterName = "Microsoft Print to PDF";
				PdfSaveDirectory = Directories.DocumentsDirectory;
			}
		}

		public ReportSetting() : this(@"lib\report-setting.json")
		{

		}

		public string PrinterName
		{
			get => GetProperty(_PrinterName);
			set => SetProperty(ref _PrinterName, value);
		}
		private string _PrinterName = string.Empty;

		public string PdfSaveDirectory
		{
			get => GetProperty(_PdfSaveDirectory);
			set => SetProperty(ref _PdfSaveDirectory, value);
		}
		private string _PdfSaveDirectory = string.Empty;

	}
}
