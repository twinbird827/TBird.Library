using TBird.Core;

namespace PDF2ZIP
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

				// 並列処理数
				NumberOfParallel = 100;

				// 一度に処理するﾍﾟｰｼﾞ数
				Dpi = 96 * 3;

				// 品質
				Quality = 100;
			}
		}

		/// <summary>
		/// 処理ｵﾌﾟｼｮﾝ
		/// </summary>
		public int Option
		{
			get => GetProperty(_Option);
			set => SetProperty(ref _Option, value);
		}
		private int _Option;

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
		/// 処理数
		/// </summary>
		public double Dpi
		{
			get => GetProperty(_Dpi);
			set => SetProperty(ref _Dpi, value);
		}
		private double _Dpi;

		/// <summary>
		/// 処理数
		/// </summary>
		public int Quality
		{
			get => GetProperty(_Quality);
			set => SetProperty(ref _Quality, value);
		}
		private int _Quality;

	}
}