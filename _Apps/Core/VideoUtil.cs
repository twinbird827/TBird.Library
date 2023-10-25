using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TBird.Web;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Moviewer.Core
{
	public static class VideoUtil
	{

		public static async Task<BitmapImage> GetThumnailAsync(string id, params string[] urls)
		{
			var bytes = await WebImageUtil.GetBytesAsync(id, urls);
			var image = bytes != null ? ControlUtil.GetImage(bytes) : null;
			return image;
		}

		public static void Save()
		{
			VideoSetting.Instance.Temporaries = Temporaries.ToArray();
			VideoSetting.Instance.Histories = Histories.ToArray();
			VideoSetting.Instance.Save();
		}

		// **************************************************
		// Temporaries

		public static BindableCollection<VideoHistoryModel> Temporaries
		{
			get => _Temporaries = _Temporaries ?? new BindableCollection<VideoHistoryModel>(VideoSetting.Instance.Temporaries);
		}
		private static BindableCollection<VideoHistoryModel> _Temporaries;

		public static void AddTemporary(MenuMode mode, string contentid, bool issave = true)
		{
			Temporaries.AddModel(mode, contentid);
			if (issave) Save();
		}

		public static void DelTemporary(MenuMode mode, string contentid, bool issave = true)
		{
			if (Temporaries.DelModel(mode, contentid) && issave) Save();
		}

		// **************************************************
		// Histories

		public static BindableCollection<VideoHistoryModel> Histories
		{
			get => _Histories = _Histories ?? new BindableCollection<VideoHistoryModel>(VideoSetting.Instance.Histories);
		}
		private static BindableCollection<VideoHistoryModel> _Histories;

		public static void AddHistory(MenuMode mode, string contentid, bool issave = true)
		{
			Histories.AddModel(mode, contentid);
			if (issave) Save();
		}

		public static void DelHistory(MenuMode mode, string contentid, bool issave = true)
		{
			if (Histories.DelModel(mode, contentid) && issave) Save();
		}

	}
}