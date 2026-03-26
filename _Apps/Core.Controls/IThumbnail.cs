using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Core.Controls
{
	public interface IThumbnailUrl : IBindable
	{
		string ThumbnailUrl { get; set; }
	}

	public interface IThumbnail : IBindable
	{
		Task SetThumbnail(string url);

		BitmapImage Thumbnail { get; set; }
	}

	public static class ThumbnailExtension
	{
		public static void SetThumbnail(this IThumbnail tgt, IThumbnailUrl m)
		{
			m.AddOnPropertyChanged(tgt, async (sender, e) =>
			{
				if (!string.IsNullOrEmpty(m.ThumbnailUrl)) await tgt.SetThumbnail(m.ThumbnailUrl);
			}, nameof(m.ThumbnailUrl), true);
		}

		public static async Task SetThumbnail(this IThumbnail m, string id, params string[] urls)
		{
			if (m.Thumbnail != null) return;

			await VideoUtil
				.GetThumnailAsync(id, urls).TryCatch()
				.ContinueWith(x => m.Thumbnail = x.Result);
		}
	}
}