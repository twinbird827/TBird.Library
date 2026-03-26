namespace Moviewer.Tube.Core
{
	public class TubeModel
	{
		private TubeModel()
		{

		}

		private static TubeModel Instance
		{
			get => _Instance = _Instance ?? new TubeModel();
		}
		private static TubeModel _Instance;

		public static void Save()
		{
			TubeSetting.Instance.Save();
		}

	}
}