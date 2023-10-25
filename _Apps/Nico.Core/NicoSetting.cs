using Moviewer.Nico.Controls;
using System;
using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Nico.Core
{
	public class NicoSetting : JsonBase<NicoSetting>
	{
		private const string _path = @"lib\nico-setting.json";

		public static NicoSetting Instance
		{
			get => _Instance = _Instance ?? new NicoSetting();
		}
		private static NicoSetting _Instance;

		public NicoSetting() : base(_path)
		{
			if (!Load())
			{
				Temporaries = new NicoVideoHistoryModel[] { };

				Histories = new NicoVideoHistoryModel[] { };

				SearchHistories = new NicoSearchHistoryModel[] { };

				SearchFavorites = new NicoSearchHistoryModel[] { };

				Searches = new NicoSearchHistoryModel[] { };

				Favorites = new NicoSearchHistoryModel[] { };
			}
		}

		public string NicoRankingGenre
		{
			get => GetProperty(_NicoRankingGenre);
			set => SetProperty(ref _NicoRankingGenre, value);
		}
		private string _NicoRankingGenre;

		public string NicoRankingPeriod
		{
			get => GetProperty(_NicoRankingPeriod);
			set => SetProperty(ref _NicoRankingPeriod, value);
		}
		private string _NicoRankingPeriod;

		public string NicoSearchOrderby
		{
			get => GetProperty(_NicoSearchOrderby);
			set => SetProperty(ref _NicoSearchOrderby, value);
		}
		private string _NicoSearchOrderby;

		public string NicoFavoriteOrderby
		{
			get => GetProperty(_NicoFavoriteOrderby);
			set => SetProperty(ref _NicoFavoriteOrderby, value);
		}
		private string _NicoFavoriteOrderby;

		public NicoVideoHistoryModel[] Temporaries
		{
			get => GetProperty(_Temporaries);
			set => SetProperty(ref _Temporaries, value);
		}
		private NicoVideoHistoryModel[] _Temporaries;

		public NicoVideoHistoryModel[] Histories
		{
			get => GetProperty(_Histories);
			set => SetProperty(ref _Histories, value);
		}
		private NicoVideoHistoryModel[] _Histories;

		public NicoSearchHistoryModel[] SearchHistories
		{
			get => GetProperty(_SearchHistories);
			set => SetProperty(ref _SearchHistories, value);
		}
		private NicoSearchHistoryModel[] _SearchHistories;

		public NicoSearchHistoryModel[] SearchFavorites
		{
			get => GetProperty(_SearchFavorites);
			set => SetProperty(ref _SearchFavorites, value);
		}
		private NicoSearchHistoryModel[] _SearchFavorites;

		public NicoSearchHistoryModel[] Searches
		{
			get => GetProperty(_Searches);
			set => SetProperty(ref _Searches, value);
		}
		private NicoSearchHistoryModel[] _Searches;

		public NicoSearchHistoryModel[] Favorites
		{
			get => GetProperty(_Favorites);
			set => SetProperty(ref _Favorites, value);
		}
		private NicoSearchHistoryModel[] _Favorites;

	}

	public class NicoVideoHistoryModel : BindableBase
	{
		public NicoVideoHistoryModel()
		{

		}

		public NicoVideoHistoryModel(string contentid)
		{
			ContentId = contentid;
			Date = DateTime.Now; ;
		}

		public string ContentId
		{
			get => _ContentId;
			set => SetProperty(ref _ContentId, value);
		}
		private string _ContentId = null;

		public DateTime Date
		{
			get => _Date;
			set => SetProperty(ref _Date, value);
		}
		private DateTime _Date;
	}

}