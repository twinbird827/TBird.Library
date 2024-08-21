using TBird.Core;

namespace ZIPConverter
{
	public class AppSetting : JsonBase<AppSetting>
	{
		public static AppSetting Instance { get; private set; } = new AppSetting();

		public AppSetting() : base(@"app-setting.json")
		{
			if (!Load())
			{
				// ｵﾌﾟｼｮﾝ
				Option = "0";

				// 並行処理数
				ParallelCount = 50;

				Width = 2400;

				Height = 1350;

				Quality = 100;

				// 除外するﾃﾞｨﾚｸﾄﾘ
				IgnoreDirectories = new[]
				{
					"単ページ"
				};

				// 除外するﾌｧｲﾙ拡張子
				IgnoreFiles = new[]
				{
					".db",
					".dll",
					".htm",
					".lnk",
					".url",
					".html",
					".shtml",
					".txt"
				};
			}
		}

		/// <summary>
		/// 処理ｵﾌﾟｼｮﾝ
		/// </summary>
		public string Option
		{
			get => GetProperty(_Option);
			set => SetProperty(ref _Option, value);
		}
		private string _Option;

		/// <summary>
		/// 並行処理数
		/// </summary>
		public int ParallelCount
		{
			get => GetProperty(_ParallelCount);
			set => SetProperty(ref _ParallelCount, value);
		}
		private int _ParallelCount;

		/// <summary>
		/// 並行処理数
		/// </summary>
		public double Width
		{
			get => GetProperty(_Width);
			set => SetProperty(ref _Width, value);
		}
		private double _Width;

		/// <summary>
		/// 並行処理数
		/// </summary>
		public double Height
		{
			get => GetProperty(_Height);
			set => SetProperty(ref _Height, value);
		}
		private double _Height;

		/// <summary>
		/// 並行処理数
		/// </summary>
		public int Quality
		{
			get => GetProperty(_Quality);
			set => SetProperty(ref _Quality, value);
		}
		private int _Quality;

		/// <summary>
		/// 除外するﾃﾞｨﾚｸﾄﾘ
		/// </summary>
		public string[] IgnoreDirectories
		{
			get => GetProperty(_IgnoreDirectories);
			set => SetProperty(ref _IgnoreDirectories, value);
		}
		private string[] _IgnoreDirectories = new string[] { };

		/// <summary>
		/// 除外するﾌｧｲﾙ拡張子
		/// </summary>
		public string[] IgnoreFiles
		{
			get => GetProperty(_IgnoreFiles);
			set => SetProperty(ref _IgnoreFiles, value);
		}
		private string[] _IgnoreFiles = new string[] { };

	}
}