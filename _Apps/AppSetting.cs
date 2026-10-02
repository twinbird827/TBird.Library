using TBird.Core;

namespace PDF2JPG
{
	public class AppSetting : JsonBase<AppSetting>
	{
		public static AppSetting Instance { get; private set; } = new AppSetting();

		public AppSetting() : base(@"app-setting.json")
		{
			if (!Load())
			{
				// ｵﾌﾟｼｮﾝ
				Option = "1";

				// 一度に処理するﾌｧｲﾙ数
				NumberOfParallel = 10;

				// 解像度
				Dpi = 384;
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
		private string _Option = "1";

		/// <summary>
		/// 並列処理数
		/// </summary>
		public int NumberOfParallel
		{
			get => GetProperty(_NumberOfParallel);
			set => SetProperty(ref _NumberOfParallel, value);
		}
		private int _NumberOfParallel;

		/// <summary>
		/// 解像度
		/// </summary>
		public int Dpi
		{
			get => GetProperty(_Dpi);
			set => SetProperty(ref _Dpi, value);
		}
		private int _Dpi;

	}
}