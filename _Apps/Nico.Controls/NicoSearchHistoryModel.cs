using Moviewer.Nico.Core;
using System;
using TBird.Wpf;

namespace Moviewer.Nico.Controls
{
	public class NicoSearchHistoryModel : BindableBase
	{
		public NicoSearchHistoryModel()
		{

		}

		public NicoSearchHistoryModel(string word, NicoSearchType type)
		{
			Word = word;
			Type = type;
			Date = DateTime.Now;
		}

		public string Word
		{
			get => _Word;
			set => SetProperty(ref _Word, value);
		}
		private string _Word;

		public NicoSearchType Type
		{
			get => _Type;
			set => SetProperty(ref _Type, value);
		}
		private NicoSearchType _Type;

		public DateTime Date
		{
			get => _Date;
			set => SetProperty(ref _Date, value);
		}
		private DateTime _Date;

	}
}