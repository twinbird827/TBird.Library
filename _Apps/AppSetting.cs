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
				Option = 0;

				// 並行処理数
				ParallelCount = 10;

				// NConvert ﾌｧｲﾙﾊﾟｽ
				NConvertPath = "nconvert.exe";

				/*
				 * NConvert Option
				 * -D           元のﾌｧｲﾙを削除する
				 * -q 80        圧縮率[0 - 100]を指定する
				 * -opthuff     ﾊﾌﾏﾝﾃｰﾌﾞﾙを最適化する。
				 * -ratio       元のｲﾒｰｼﾞの比率を保持する
				 * -rflag decr  減少方向でﾘｻｲｽﾞする
				 * -resize h w  ﾘｻｲｽﾞpx h = 高さ, w = 幅
				 * -out jpeg    変換後の画像形式
				 * https://geolog.mydns.jp/www.geocities.co.jp/xnviewja/nconvert.html
				 **/
				NConvertOption = "-D -q 80 -opthuff -ratio -rflag decr -resize 1350px 2400px -out jpeg";

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
		public int Option
		{
			get => GetProperty(_Option);
			set => SetProperty(ref _Option, value);
		}
		private int _Option;

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
		/// NConvertﾌｧｲﾙﾊﾟｽ
		/// </summary>
		public string NConvertPath
		{
			get => GetProperty(_NConvertPath);
			set => SetProperty(ref _NConvertPath, value);
		}
		private string _NConvertPath = string.Empty;

		/// <summary>
		/// NConvertｵﾌﾟｼｮﾝ
		/// </summary>
		public string NConvertOption
		{
			get => GetProperty(_NConvertOption);
			set => SetProperty(ref _NConvertOption, value);
		}
		private string _NConvertOption = string.Empty;

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