using Moviewer.Core.Controls;
using Moviewer.Nico.Core;
using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Moviewer.Nico.Controls
{
	public class NicoMylistViewModel : ControlViewModel, IThumbnail
	{
		public NicoMylistViewModel(NicoMylistModel m) : base(m)
		{
			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				MylistId = m.MylistId;
			}, nameof(MylistId), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				MylistTitle = m.MylistTitle;
			}, nameof(MylistTitle), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				MylistDescription = m.MylistDescription;
			}, nameof(MylistDescription), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				MylistDate = m.MylistDate;
			}, nameof(MylistDate), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				SetThumbnail(m.ThumbnailUrl);
			}, nameof(m.ThumbnailUrl), true);

			m.UserInfo.AddOnPropertyChanged(this, (sender, e) =>
			{
				MylistUsername = m.UserInfo.Username;
			}, nameof(m.UserInfo.Username), true);

			Loaded.Add(SetThumbnail);

			AddDisposed((sender, e) =>
			{
				Source = null;
			});
		}

		public NicoMylistModel Source { get; private set; }

		public string MylistId
		{
			get => _MylistId;
			set => SetProperty(ref _MylistId, value);
		}
		private string _MylistId;

		public string MylistTitle
		{
			get => _MylistTitle;
			set => SetProperty(ref _MylistTitle, value);
		}
		private string _MylistTitle;

		public string MylistDescription
		{
			get => _MylistDescription;
			set => SetProperty(ref _MylistDescription, value);
		}
		private string _MylistDescription = null;

		public string MylistUsername
		{
			get => _MylistUsername;
			set => SetProperty(ref _MylistUsername, value);
		}
		private string _MylistUsername = null;

		public DateTime MylistDate
		{
			get => _MylistDate;
			set => SetProperty(ref _MylistDate, value);
		}
		private DateTime _MylistDate;

		public BitmapImage Thumbnail
		{
			get => _Thumbnail;
			set => SetProperty(ref _Thumbnail, value);
		}
		private BitmapImage _Thumbnail;

		private void SetThumbnail()
		{
			this.SetThumbnail(Source);
		}

		public virtual Task SetThumbnail(string url)
		{
			return this.SetThumbnail(MylistId, url, NicoUtil.NicoBlankUserUrl);
		}
	}
}