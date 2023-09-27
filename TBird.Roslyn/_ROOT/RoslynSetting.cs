using TBird.Core;

namespace TBird.Roslyn
{
	public class RoslynSetting : JsonBase<RoslynSetting>
	{
		private const string _path = @"lib\roslyn-setting.json";

		public static RoslynSetting Instance
		{
			get => _Instance = _Instance ?? new RoslynSetting();
		}
		private static RoslynSetting _Instance;

		public RoslynSetting() : base(_path)
		{
			if (!Load())
			{
				Interval = 1000;

				Imports = new string[]
				{
					"System",
					"System.Text",
					"System.Linq",
				};
			}
		}

		/// <summary>
		/// 処理間隔
		/// </summary>
		public int Interval
		{
			get => GetProperty(_Interval);
			set => SetProperty(ref _Interval, value);
		}
		private int _Interval;

		/// <summary>
		/// ｽｸﾘﾌﾟﾄが暗黙的にｲﾝﾎﾟｰﾄ(Using)する名前空間ﾘｽﾄ
		/// </summary>
		public string[] Imports
		{
			get => GetProperty(_Imports);
			set => SetProperty(ref _Imports, value);
		}
		private string[] _Imports;

	}
}