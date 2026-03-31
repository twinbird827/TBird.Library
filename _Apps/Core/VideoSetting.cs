using TBird.Core;

namespace Moviewer.Core
{
	public class VideoSetting : JsonBase<VideoSetting>
	{
		public static VideoSetting Instance
		{
			get => _Instance = _Instance ?? new VideoSetting();
		}
		private static VideoSetting _Instance;

		public VideoSetting() : base(PathSetting.Instance.GetFullPath("lib", "video-setting.json"))
		{
			if (!Load())
			{
				Histories = new VideoHistoryModel[] { };
				Temporaries = new VideoHistoryModel[] { };
			}
		}

		public VideoHistoryModel[] Histories
		{
			get => GetProperty(_Histories);
			set => SetProperty(ref _Histories, value);
		}
		private VideoHistoryModel[] _Histories;

		public VideoHistoryModel[] Temporaries
		{
			get => GetProperty(_Temporaries);
			set => SetProperty(ref _Temporaries, value);
		}
		private VideoHistoryModel[] _Temporaries;

	}
}