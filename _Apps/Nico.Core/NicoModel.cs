using Moviewer.Nico.Controls;
using System;
using System.Linq;
using TBird.Wpf.Collections;

namespace Moviewer.Nico.Core
{
	public class NicoModel
	{
		private NicoModel()
		{

		}

		private static NicoModel Instance
		{
			get => _Instance = _Instance ?? new NicoModel();
		}
		private static NicoModel _Instance;

		public static void Save()
		{
			NicoSetting.Instance.Searches = Searches.ToArray();
			NicoSetting.Instance.Favorites = Favorites.ToArray();
			NicoSetting.Instance.Save();
		}

		// **************************************************
		// Searches

		public static BindableCollection<NicoSearchHistoryModel> Searches
		{
			get => Instance._SearchHistories = Instance._SearchHistories ?? new BindableCollection<NicoSearchHistoryModel>(NicoSetting.Instance.Searches);
		}
		private BindableCollection<NicoSearchHistoryModel> _SearchHistories;

		public static void AddSearch(string word, NicoSearchType type, bool issave = true)
		{
			var tmp = Searches.FirstOrDefault(x => x.Word == word && x.Type == type);
			if (tmp != null)
			{
				tmp.Date = DateTime.Now;
			}
			else
			{
				Searches.Add(new NicoSearchHistoryModel(word, type));
			}
			if (issave) Save();
		}

		public static void DelSearch(string word, NicoSearchType type, bool issave = true)
		{
			var tmp = Searches.FirstOrDefault(x => x.Word == word && x.Type == type);
			if (tmp != null)
			{
				Searches.Remove(tmp);
				if (issave) Save();
			}
		}

		// **************************************************
		// Favorites

		public static BindableCollection<NicoSearchHistoryModel> Favorites
		{
			get => Instance._SearchFavorites = Instance._SearchFavorites ?? new BindableCollection<NicoSearchHistoryModel>(NicoSetting.Instance.Favorites);
		}
		private BindableCollection<NicoSearchHistoryModel> _SearchFavorites;

		public static void AddFavorite(string word, NicoSearchType type, bool issave = true)
		{
			var tmp = Favorites.FirstOrDefault(x => x.Word == word && x.Type == type);
			if (tmp != null)
			{
				tmp.Date = DateTime.Now;
			}
			else
			{
				Favorites.Add(new NicoSearchHistoryModel(word, type));
			}
			if (issave) Save();
		}

		public static void DelFavorite(string word, NicoSearchType type, bool issave = true)
		{
			var tmp = Favorites.FirstOrDefault(x => x.Word == word && x.Type == type);
			if (tmp != null)
			{
				Favorites.Remove(tmp);
				if (issave) Save();
			}
		}
	}
}