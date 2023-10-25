using TBird.Core;

namespace Moviewer.Core.Controls
{
	public class UserModel : ControlModel, IThumbnailUrl
	{
		public string Userid
		{
			get => _Userid;
			set => SetProperty(ref _Userid, value);
		}
		private string _Userid = null;

		public string Username
		{
			get => _Username;
			set => SetProperty(ref _Username, value);
		}
		private string _Username = null;

		public string ThumbnailUrl
		{
			get => _ThumbnailUrl;
			set => SetProperty(ref _ThumbnailUrl, value);
		}
		private string _ThumbnailUrl;

		public void SetUserInfo(UserModel m)
		{
			SetUserInfo(m.Userid, m.Username, m.ThumbnailUrl);
		}

		public virtual void SetUserInfo(string id, string name = null, string url = null)
		{
			Userid = CoreUtil.Nvl(id, Userid);
			Username = CoreUtil.Nvl(name, Username);
			ThumbnailUrl = CoreUtil.Nvl(url, ThumbnailUrl);
		}
	}
}