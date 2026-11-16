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
			Top2Prob = x.Top2Prob;
			Top3Prob = x.Top3Prob;
			Odds = x.Detail.Odds;
			EV = x.EV;
		}

		public int Wakuban { get; set; }

		public int Umaban { get; set; }

		public string Name { get; set; }

		public string Result { get; set; }

		public RaceScore Win { get; set; }

		public float Top2Prob { get; set; }

		public float Top3Prob { get; set; }

		public float Odds { get; set; }

		public RaceScore EV { get; set; }

	}
}
