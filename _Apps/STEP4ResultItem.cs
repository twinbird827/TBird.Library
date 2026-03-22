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

		public RaceScore Total
		{
			get => _Total;
			set => SetProperty(ref _Total, value);
		}
		private RaceScore _Total;

		public RaceScore Horse
		{
			get => _Horse;
			set => SetProperty(ref _Horse, value);
		}
		private RaceScore _Horse;

		public RaceScore TotalMedium
		{
			get => _TotalMedium;
			set => SetProperty(ref _TotalMedium, value);
		}
		private RaceScore _TotalMedium;

		public RaceScore TotalSmall
		{
			get => _TotalSmall;
			set => SetProperty(ref _TotalSmall, value);
		}
		private RaceScore _TotalSmall;

		public RaceScore Vars2
		{
			get => _Vars2;
			set => SetProperty(ref _Vars2, value);
		}
		private RaceScore _Vars2;

		public RaceScore Vars1
		{
			get => _Vars1;
			set => SetProperty(ref _Vars1, value);
		}
		private RaceScore _Vars1;

	}
}
