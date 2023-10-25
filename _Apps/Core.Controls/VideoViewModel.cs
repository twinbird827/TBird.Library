using Moviewer.Core.Windows;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TBird.Web;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Moviewer.Core.Controls
{
	public class VideoViewModel : ControlViewModel, IThumbnail
	{
		public VideoViewModel(VideoModel m) : base(m)
		{
			Source = m;
			ContentId = m.ContentId;

			Counters = m.Counters
				.ToBindableSelectCollection(CreateCounterViewModel)
				.ToBindableContextCollection();

			Tags = m.Tags
				.ToBindableDistinctCollection(x => x)
				.ToBindableSelectCollection(CreateTagViewModel)
				.ToBindableContextCollection();

			UserInfo = CreateUserInfo();
			UserInfo.SetUserInfo(m.UserInfo);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Title = m.Title;
			}, nameof(m.Title), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Description = m.Description;
			}, nameof(m.Description), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				StartTime = m.StartTime;
			}, nameof(m.StartTime), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				TempTime = m.TempTime;
			}, nameof(m.TempTime), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Duration = m.Duration;
			}, nameof(m.Duration), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Status = m.Status;
			}, nameof(m.Status), true);

			Loaded.Add(SetThumbnail);

			AddDisposed((sender, e) =>
			{
				Tags.Dispose();
				Counters.Dispose();
				UserInfo.Dispose();
				Source = null;
			});
		}

		public VideoModel Source { get; private set; }

		public string ContentUrl
		{
			get => _ContentUrl;
			set => SetProperty(ref _ContentUrl, value);
		}
		private string _ContentUrl;

		public string ContentId
		{
			get => _ContentId;
			set => SetProperty(ref _ContentId, value);
		}
		private string _ContentId;

		public string Title
		{
			get => _Title;
			set => SetProperty(ref _Title, value);
		}
		private string _Title;

		public string Description
		{
			get => _Description;
			set => SetProperty(ref _Description, value);
		}
		private string _Description;

		public BitmapImage Thumbnail
		{
			get => _Thumbnail;
			set => SetProperty(ref _Thumbnail, value);
		}
		private BitmapImage _Thumbnail;

		public BindableCollection<CounterViewModel> Counters { get; private set; }

		public DateTime StartTime
		{
			get => _StartTime;
			set => SetProperty(ref _StartTime, value);
		}
		private DateTime _StartTime;

		public DateTime TempTime
		{
			get => _TempTime;
			set => SetProperty(ref _TempTime, value);
		}
		private DateTime _TempTime;

		public TimeSpan Duration
		{
			get => _Duration;
			set => SetProperty(ref _Duration, value);
		}
		private TimeSpan _Duration;

		public UserViewModel UserInfo
		{
			get => _UserInfo;
			set => SetProperty(ref _UserInfo, value);
		}
		private UserViewModel _UserInfo;

		public BindableCollection<TagViewModel> Tags { get; private set; }

		public VideoStatus Status
		{
			get => _Status;
			set => SetProperty(ref _Status, value);
		}
		private VideoStatus _Status = VideoStatus.None;

		private void SetThumbnail()
		{
			this.SetThumbnail(Source);
		}

		public virtual Task SetThumbnail(string url)
		{
			return this.SetThumbnail(ContentId, url);
		}

		protected virtual TagViewModel CreateTagViewModel(string tag)
		{
			return new TagViewModel(tag);
		}

		protected virtual CounterViewModel CreateCounterViewModel(CounterModel m)
		{
			return new CounterViewModel(m);
		}

		public ICommand OnDoubleClick =>
			_OnDoubleClick = _OnDoubleClick ?? RelayCommand.Create(DoubleClickCommand);
		private ICommand _OnDoubleClick;

		protected virtual void DoubleClickCommand(object parameter)
		{
			// 視聴ﾘｽﾄに追加
			VideoUtil.AddHistory(Source.Mode, Source.ContentId);

			// ｽﾃｰﾀｽ更新
			Source.RefreshStatus();

			// ﾌﾞﾗｳｻﾞ起動
			WebUtil.Browse(ContentUrl);
		}

		public ICommand OnKeyDown =>
			_OnKeyDown = _OnKeyDown ?? RelayCommand.Create<KeyEventArgs>(KeyDownCommand);
		private ICommand _OnKeyDown;

		protected virtual void KeyDownCommand(KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				OnDoubleClick.Execute(null);
			}
		}

		public ICommand OnTemporaryAdd => _OnTemporaryAdd = _OnTemporaryAdd ?? RelayCommand.Create(_ =>
		{
			VideoUtil.AddTemporary(Source.Mode, Source.ContentId);
			Source.RefreshStatus();
		});
		private ICommand _OnTemporaryAdd;

		public ICommand OnTemporaryDel => _OnTemporaryDel = _OnTemporaryDel ?? RelayCommand.Create(_ =>
		{
			VideoUtil.DelTemporary(Source.Mode, Source.ContentId);
			Source.RefreshStatus();
		});
		private ICommand _OnTemporaryDel;

		public ICommand OnDownload => _OnDownload = _OnDownload ?? RelayCommand.Create(_ =>
		{
			DownloadViewModel.Download(GetDownloadModel());
		});
		private ICommand _OnDownload;

		protected virtual DownloadModel GetDownloadModel()
		{
			return null;
		}

		protected virtual UserViewModel CreateUserInfo()
		{
			return new UserViewModel();
		}
	}
}