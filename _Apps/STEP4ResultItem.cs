using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Wpf;

namespace Netkeiba
{
	public class STEP4ResultItem : BindableBase
	{
		public int Wakuban
		{
			get => _Wakuban;
			set => SetProperty(ref _Wakuban, value);
		}
		private int _Wakuban;

		public int Umaban
		{
			get => _Umaban;
			set => SetProperty(ref _Umaban, value);
		}
		private int _Umaban;

		public string Name
		{
			get => _Name;
			set => SetProperty(ref _Name, value);
		}
		private string _Name = string.Empty;

		public string Result
		{
			get => _Result;
			set => SetProperty(ref _Result, value);
		}
		private string _Result = string.Empty;

		public int Rank
		{
			get => _Rank;
			set => SetProperty(ref _Rank, value);
		}
		private int _Rank;

		public RaceScore All
		{
			get => _All;
			set => SetProperty(ref _All, value);
		}
		private RaceScore _All;

		public RaceScore Horse
		{
			get => _Horse;
			set => SetProperty(ref _Horse, value);
		}
		private RaceScore _Horse;

		public RaceScore Jockey
		{
			get => _Jockey;
			set => SetProperty(ref _Jockey, value);
		}
		private RaceScore _Jockey;

		public RaceScore Blood
		{
			get => _Blood;
			set => SetProperty(ref _Blood, value);
		}
		private RaceScore _Blood;

		public RaceScore Connection
		{
			get => _Connection;
			set => SetProperty(ref _Connection, value);
		}
		private RaceScore _Connection;

		public RaceScore Total
		{
			get => _Total;
			set => SetProperty(ref _Total, value);
		}
		private RaceScore _Total;

	}
}