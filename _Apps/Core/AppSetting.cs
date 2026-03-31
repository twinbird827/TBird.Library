using TBird.Core;

namespace Moviewer.Core
{
	public class AppSetting : JsonBase<AppSetting>
	{
		public static AppSetting Instance
		{
			get => _Instance = _Instance ?? new AppSetting();
		}
		private static AppSetting _Instance;

		public AppSetting() : base(PathSetting.Instance.GetFullPath("lib", "app-setting.json"))
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