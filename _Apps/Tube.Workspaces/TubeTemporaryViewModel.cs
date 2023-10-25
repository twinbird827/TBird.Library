using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Core.Windows;
using Moviewer.Tube.Controls;
using Moviewer.Tube.Core;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;
using WinCopies.Util;

namespace Moviewer.Tube.Workspaces
{
	public class TubeTemporaryViewModel : WorkspaceViewModel, IVideoParentViewModel
	{
		public override MenuType Type => MenuType.TubeTemporary;

		public TubeTemporaryViewModel()
		{
			//VideoType = new ComboboxViewModel(NicoUtil.GetCombos("video_type"));
			//VideoType.SelectedItem = null;

			Loaded.Add(async () =>
			{
				Sources = VideoUtil.Temporaries
					.ToBindableWhereCollection(x => x.Mode == MenuMode.Youtube)
					.ToBindableSelectCollection(TubeVideoModel.FromHistory)
					.ToBindableSelectCollection(x => new TubeVideoViewModel(this, x));

				var sources = await TubeUtil.GetVideosByIds(Sources.Select(x => x.ContentId).ToArray());

				Sources
					.Select(x => (TubeVideoModel)x.Source)
					.ForEach(x => x.SetModel(sources.FirstOrDefault(y => y.ContentId == x.ContentId)));

				Users = Sources
					.ToBindableSelectCollection(x => x.UserInfo)
					.ToBindableDistinctCollection(x => x.Userid, nameof(UserModel.Userid))
					.ToBindableContextCollection();

				Videos = Sources
					.ToBindableWhereCollection(x => SelectedUser == null || x.UserInfo.Userid == SelectedUser.Userid)
					.AddOnRefreshCollection(this, nameof(SelectedUser))
					//.ToBindableWhereCollection(x => VideoType.SelectedItem == null || x.ContentId.StartsWith(VideoType.SelectedItem.Value))
					//.AddOnRefreshCollection(VideoType, nameof(VideoType.SelectedItem))
					.ToBindableSortedCollection(x => x.TempTime, true)
					.ToBindableContextCollection();
			});

			AddDisposed((sender, e) =>
			{
				Videos.Dispose();
				Users.Dispose();
				Sources.Dispose();
			});
		}

		//public ComboboxViewModel VideoType { get; private set; }

		//public ICommand OnDeleteVideoType => _OnDeleteVideoType = _OnDeleteVideoType ?? RelayCommand.Create(_ =>
		//{
		//    VideoType.SelectedItem = null;
		//});
		//private ICommand _OnDeleteVideoType;

		public BindableChildCollection<UserViewModel> Users
		{
			get => _Users;
			set => SetProperty(ref _Users, value);
		}
		public BindableChildCollection<UserViewModel> _Users;

		public UserViewModel SelectedUser
		{
			get => _SelectedUser;
			set => SetProperty(ref _SelectedUser, value);
		}
		public UserViewModel _SelectedUser;

		public BindableCollection<TubeVideoViewModel> Sources
		{
			get => _Sources;
			set => SetProperty(ref _Sources, value);
		}
		public BindableCollection<TubeVideoViewModel> _Sources;

		public BindableContextCollection<TubeVideoViewModel> Videos
		{
			get => _Videos;
			set => SetProperty(ref _Videos, value);
		}
		public BindableContextCollection<TubeVideoViewModel> _Videos;

		public ICommand OnDeleteSelectedUser => _OnDeleteSelectedUser = _OnDeleteSelectedUser ?? RelayCommand.Create(_ =>
		{
			SelectedUser = null;
		});
		private ICommand _OnDeleteSelectedUser;

		public ICommand OnTemporaryAdd => _OnTemporaryAdd = _OnTemporaryAdd ?? RelayCommand.Create(async _ =>
		{
			using (var vm = new WpfMessageInputViewModel(AppConst.L_AddTemporary, AppConst.M_AddTemporary, AppConst.L_UrlOrId, true))
			{
				if (!vm.ShowDialog(() => new WpfMessageInputWindow()))
				{
					return;
				}

				await AddTemporary(vm.Value);
			}
		});
		private ICommand _OnTemporaryAdd;

		protected override async void OnDropProcess(string droptxt)
		{
			await AddTemporary(droptxt);
		}

		private async Task AddTemporary(string url)
		{
			var video = await TubeUtil.GetVideo(TubeUtil.Url2Id(url));
			if (video.Status != VideoStatus.Delete)
			{
				VideoUtil.AddTemporary(MenuMode.Youtube, video.ContentId);
			}
		}

		public void DeleteOnVideo(VideoViewModel vm)
		{
			VideoUtil.DelTemporary(MenuMode.Youtube, vm.Source.ContentId);
		}
	}
}