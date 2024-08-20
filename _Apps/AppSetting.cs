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

				// PDFをJPGに変換する自作ｱﾌﾟﾘのﾊﾟｽ
				PDF2JPG = @"..\_Tools\PDF2JPG\PDF2JPG.exe";

				// ﾌｫﾙﾀﾞをZIP圧縮する自作ｱﾌﾟﾘのﾊﾟｽ
				ZIPCONV = @"..\_Tools\ZIPConverter\ZIPConverter.exe";
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

		public string PDF2JPG
		{
			get => GetProperty(_PDF2JPG);
			set => SetProperty(ref _PDF2JPG, value);
		}
		private string _PDF2JPG = string.Empty;

		public string ZIPCONV
		{
			get => GetProperty(_ZIPCONV);
			set => SetProperty(ref _ZIPCONV, value);
		}
		private string _ZIPCONV = string.Empty;

	}
}