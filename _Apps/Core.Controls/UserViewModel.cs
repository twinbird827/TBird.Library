using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Core.Controls
{
	public class UserViewModel : ControlViewModel, IThumbnail
	{
		public UserViewModel() : base(null)
		{
			AddDisposed((sender, e) =>
			{
				Thumbnail = null;

				Source.TryDispose();
			});
		}

		public UserViewModel SetUserInfo(UserModel m)
		{
			if (m == null) return this;

			Source = m;

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Userid = m.Userid;
			}, nameof(m.Userid), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Username = m.Username;
			}, nameof(m.Username), true);

			Loaded.Add(SetThumbnail);

			return this;
		}

		public UserModel Source { get; private set; }

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

		public BitmapImage Thumbnail
		{
			get => _Thumbnail;
			set => SetProperty(ref _Thumbnail, value);
		}
		private BitmapImage _Thumbnail;

		public void SetThumbnail()
		{
			this.SetThumbnail(Source);
		}

		public virtual async Task SetThumbnail(string url)
		{
			await this.SetThumbnail(Userid, url);
		}

		public ICommand OnClickUsername =>
			_OnClickUsername = _OnClickUsername ?? RelayCommand.Create(OnClickUsernameCommand);
		private ICommand _OnClickUsername;

		protected virtual void OnClickUsernameCommand(object _)
		{

		}
	}
}