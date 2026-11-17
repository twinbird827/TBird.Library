using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Wpf;

namespace Netkeiba
{
	public class STEP4ResultItem : BindableBase
	{
		public STEP4ResultItem(RacePrediction x)
		{
			Wakuban = x.Detail.Wakuban;
			Umaban = x.Detail.Umaban;
			Name = x.Name;
			Result = x.Result.Str();
			Win = x.Win;
			Top2 = x.Top2;
			Top3 = x.Top3;
			Odds = x.Detail.Odds;
			EV = x.EV;
			PlaceBreakEven = x.PlaceBreakEven;
		}

		public int Wakuban { get; set; }

		public int Umaban { get; set; }

		public string Name { get; set; }

		public string Result { get; set; }

		public RaceScore Win { get; set; }

		public RaceScore Top2 { get; set; }

		public RaceScore Top3 { get; set; }

		public float Odds { get; set; }

		public RaceScore EV { get; set; }

		public float PlaceBreakEven { get; set; }

	}
}
