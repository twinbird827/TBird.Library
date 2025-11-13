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
		private string _Name;

		public string Result
		{
			get => _Result;
			set => SetProperty(ref _Result, value);
		}
		private string _Result;

		public int Rank
		{
			get => _Rank;
			set => SetProperty(ref _Rank, value);
		}
		private int _Rank;

		public float Score
		{
			get => _Score;
			set => SetProperty(ref _Score, value);
		}
		private float _Score;

		public float Confidence
		{
			get => _Confidence;
			set => SetProperty(ref _Confidence, value);
		}
		private float _Confidence;

	}
}