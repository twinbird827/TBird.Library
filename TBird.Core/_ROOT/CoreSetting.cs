namespace TBird.Core
{
	public class CoreSetting : JsonBase<CoreSetting>
	{
		private const string _path = @"lib\core-setting.json";

		public static CoreSetting Instance
		{
			get => _Instance = _Instance ?? new CoreSetting();
		}
		private static CoreSetting _Instance;

		public CoreSetting() : base(_path)
		{
			if (!Load())
			{
				ApplicationKey = "6XxWLY6AE$yr";
				Language = "ja-JP";
				IsDebug = true;
			}
		}

		/// <summary>
		/// ｱﾌﾟﾘｹｰｼｮﾝｷｰ
		/// </summary>
		public string ApplicationKey
		{
			get => GetProperty(_ApplicationKey);
			set => SetProperty(ref _ApplicationKey, value);
		}
		private string _ApplicationKey;

		/// <summary>
		/// 言語
		/// </summary>
		public string Language
		{
			get => GetProperty(_Language);
			set => SetProperty(ref _Language, value);
		}
		private string _Language;

		/// <summary>
		/// ﾃﾞﾊﾞｯｸﾞﾓｰﾄﾞかどうか
		/// </summary>
		public bool IsDebug
		{
			get => GetProperty(_IsDebug);
			set => SetProperty(ref _IsDebug, value);
		}
		private bool _IsDebug;

	}
}