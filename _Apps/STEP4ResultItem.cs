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
		public STEP4ResultItem(RacePrediction x, string name)
		{
			Wakuban = x.Detail.Wakuban;
			Umaban = x.Detail.Umaban;
			Name = name.Str();
			Result = x.Result.Str();
			Total = x.Total;
			Horse = x.Horse;
			TotalMedium = x.TotalMedium;
			TotalSmall = x.TotalSmall;
			Vars2 = x.Vars2;
			Vars1 = x.Vars1;
		}

		public int Wakuban { get; set; }

		public int Umaban { get; set; }

		public string Name { get; set; }

		public string Result { get; set; }

		public RaceScore Total { get; set; }

		public RaceScore Horse { get; set; }

		public RaceScore TotalMedium { get; set; }

		public RaceScore TotalSmall { get; set; }

		public RaceScore Vars2 { get; set; }

		public RaceScore Vars1 { get; set; }

	}
}