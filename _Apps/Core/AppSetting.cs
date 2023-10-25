using TBird.Core;

namespace Moviewer.Core
{
	public class AppSetting : JsonBase<AppSetting>
	{
		private const string _path = @"lib\app-setting.json";

		public static AppSetting Instance
		{
			get => _Instance = _Instance ?? new AppSetting();
		}
		private static AppSetting _Instance;

		public AppSetting() : base(_path)
		{
			if (!Load())
			{
				DownloadDirectory = Directories.DownloadDirectory;
			}
		}

		public string DownloadDirectory
		{
			get => GetProperty(_DownloadDirectory);
			set => SetProperty(ref _DownloadDirectory, value);
		}
		private string _DownloadDirectory;

	}
}