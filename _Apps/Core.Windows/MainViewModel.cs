using Moviewer.Nico.Core;
using Moviewer.Nico.Workspaces;
using Moviewer.Tube.Workspaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;

namespace Moviewer.Core.Windows
{
	public class MainViewModel : MainViewModelBase
	{
		public static MainViewModel Instance { get; private set; }

		public MainViewModel()
		{
			if (Instance != null) throw new ApplicationException();

			Instance = this;

			Loaded.Add(DoLoading);

			Closing.Add(DoClosing);
		}

		public DateTime StartupTime { get; } = DateTime.Now;

		/// <summary>
		/// ｶﾚﾝﾄﾜｰｸｽﾍﾟｰｽ
		/// </summary>
		public WorkspaceViewModel Current
		{
			get => _Current;
			set => SetProperty(ref _Current, value, true);
		}
		private WorkspaceViewModel _Current;

		public MenuMode MenuMode
		{
			get => _MenuMode;
			set => SetProperty(ref _MenuMode, value);
		}
		private MenuMode _MenuMode = MenuMode.Niconico;

		public int NicoTemporaryCount => VideoUtil.Temporaries.Count(x => x.Mode == MenuMode.Niconico);

		public int TubeTemporaryCount => VideoUtil.Temporaries.Count(x => x.Mode == MenuMode.Youtube);

		/// <summary>
		/// お気に入り巡回ﾀｲﾏｰ
		/// </summary>
		public IntervalTimer FavoriteChecker { get; private set; }

		private async Task PatrolFavorites()
		{
			foreach (var m in NicoModel.Favorites)
			{
				var enumerable = await NicoUtil.GetVideoBySearchType(m.Word, m.Type, "regdate-");
				var arr = enumerable.Where(x => m.Date < x.StartTime).ToArray();

				foreach (var video in arr)
				{
					VideoUtil.AddTemporary(MenuMode.Niconico, video.ContentId, false);

					m.Date = Arr(m.Date, video.StartTime).Max();
				}
				VideoUtil.Save();
			}
		}

		public BindableChildCollection<DownloadViewModel> Downloads
		{
			get => _Downloads = _Downloads ?? DownloadSources.ToBindableContextCollection();
		}
		private BindableChildCollection<DownloadViewModel> _Downloads;

		public BindableCollection<DownloadViewModel> DownloadSources
		{
			get => _DownloadSources = _DownloadSources ?? new BindableCollection<DownloadViewModel>();
		}
		private BindableCollection<DownloadViewModel> _DownloadSources;

		private void DoLoading()
		{
			if (!NicoSetting.Instance.Searches.Any() || !NicoSetting.Instance.Favorites.Any())
			{
				NicoSetting.Instance.Searches = NicoSetting.Instance.SearchHistories;
				NicoSetting.Instance.Favorites = NicoSetting.Instance.SearchFavorites;
				NicoSetting.Instance.Save();
			}

			ComboUtil.Initialize();

			AddCollectionChanged(VideoUtil.Temporaries, (sender, e) =>
			{
				OnPropertyChanged(nameof(NicoTemporaryCount));
				OnPropertyChanged(nameof(TubeTemporaryCount));
			});

			// お気に入り巡回ﾀｲﾏｰの起動
			FavoriteChecker = new IntervalTimer(PatrolFavorites);
			FavoriteChecker.Interval = TimeSpan.FromMinutes(10);
			FavoriteChecker.Start();

			OnClickMenu.Execute(MenuType.NicoRanking);
		}

		private bool DoClosing()
		{
			if (FavoriteChecker != null)
			{
				FavoriteChecker.Stop();
				FavoriteChecker.Dispose();
			}

			NicoModel.Save();

			return true;
		}

		public ICommand OnClickMode => _OnClickMode = _OnClickMode ?? RelayCommand.Create<MenuMode>(mode =>
		{
			MenuMode = mode;
		});
		private ICommand _OnClickMode;

		public ICommand OnClickMenu => _OnClickMenu = _OnClickMenu ?? RelayCommand.Create<MenuType>(menu =>
		{
			var newtype = _menu[menu];
			if (Current != null && newtype == Current.GetType())
			{
				return;
			}
			else
			{
				Current = Activator.CreateInstance(newtype) as WorkspaceViewModel;
			}

		});
		private ICommand _OnClickMenu;

		private Dictionary<MenuType, Type> _menu = new Dictionary<MenuType, Type>()
		{
			[MenuType.NicoRanking] = typeof(NicoRankingViewModel),
			[MenuType.NicoTemporary] = typeof(NicoTemporaryViewModel),
			[MenuType.NicoFavorite] = typeof(NicoFavoriteViewModel),
			[MenuType.NicoHistory] = typeof(NicoHistoryViewModel),
			[MenuType.NicoSearch] = typeof(NicoSearchViewModel),
			[MenuType.TubePopular] = typeof(TubePopularViewModel),
			[MenuType.TubeHome] = typeof(TubeHomeViewModel),
			[MenuType.TubeTemporary] = typeof(TubeTemporaryViewModel),
			[MenuType.TubeHistory] = typeof(TubeHistoryViewModel),
		};

	}
}