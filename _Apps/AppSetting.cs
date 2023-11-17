using TBird.Core;

namespace EBook2PDF
{
	public class AppSetting : JsonBase<AppSetting>
	{
		public static AppSetting Instance { get; private set; } = new AppSetting();

		public AppSetting() : base(@"app-setting.json")
		{
			if (!Load())
			{
				// ｵﾌﾟｼｮﾝ
				Option = 0;

				// 変換実行ﾌｧｲﾙ
				Calibre = @"C:\Program Files\Calibre2\ebook-convert.exe";

				// 変換結果格納ﾃﾞｨﾚｸﾄﾘ
				OutputDir = Directories.DownloadDirectory;
			}
		}

		public int Option
		{
			get => GetProperty(_Option);
			set => SetProperty(ref _Option, value);
		}
		private int _Option = 0;

		public string Calibre
		{
			get => GetProperty(_Calibre);
			set => SetProperty(ref _Calibre, value);
		}
		private string _Calibre = string.Empty;

		public string OutputDir
		{
			get => GetProperty(_OutputDir);
			set => SetProperty(ref _OutputDir, value);
		}
		private string _OutputDir = string.Empty;

	}
}