using System.IO;
using System.Threading.Tasks;
using TBird.Wpf;

namespace Moviewer.Core.Windows
{
	public abstract class DownloadModel : BindableBase
	{
		public string Title
		{
			get => _Title;
			set => SetProperty(ref _Title, value);
		}
		private string _Title;

		public double Maximum
		{
			get => _Maximum;
			set => SetProperty(ref _Maximum, value);
		}
		private double _Maximum;

		public double Minimum
		{
			get => _Minimum;
			set => SetProperty(ref _Minimum, value);
		}
		private double _Minimum;

		public double Value
		{
			get => _Value;
			set => SetProperty(ref _Value, value);
		}
		private double _Value;

		public string FilePath
		{
			get => _FilePath;
			set => SetProperty(ref _FilePath, value);
		}
		private string _FilePath;

		public virtual Task Initialize()
		{
			Minimum = 0;
			Maximum = 100;
			Value = 0;

			return Task.CompletedTask;
		}

		public virtual string GetDownloadPath()
		{
			const string dialogfilter = "動画ファイル|*.mp4|全ファイル|*.*";

			// 保存先を決める
			var filepath = WpfDialog.ShowSaveFile(Path.Combine(AppSetting.Instance.DownloadDirectory, Title), dialogfilter);
			if (string.IsNullOrEmpty(filepath)) return string.Empty;

			AppSetting.Instance.DownloadDirectory = Path.GetDirectoryName(filepath);
			AppSetting.Instance.Save();

			return FilePath = filepath;
		}

		public virtual Task<bool> Execute()
		{
			return Task.Run(() => true);
		}
	}
}