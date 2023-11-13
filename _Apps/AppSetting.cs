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
				Calibre = @"C:\Program Files\Calibre2\ebook-convert.exe";
			}
		}

		public string Calibre
		{
			get => GetProperty(_Calibre);
			set => SetProperty(ref _Calibre, value);
		}
		private string _Calibre = string.Empty;

	}
}