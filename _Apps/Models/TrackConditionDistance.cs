using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tensorflow;
using TBird.Core;

namespace Netkeiba.Models
{
	public class TrackConditionDistance
	{
		public TrackConditionDistance(Dictionary<string, object> x)
		{
			Track = x.Get("馬場").Str();
			TrackCondition = x.Get("馬場状態").Str();
			Distance = x.Get("距離").Str();
			Time = x.Get("ﾀｲﾑ変換").Single();
			LastThreeFurlongs = x.Get("上り").Single();
		}

		public string Track { get; private set; }

		public string Distance { get; private set; }

		public float Time { get; private set; }

		public float LastThreeFurlongs { get; private set; }

		public string TrackCondition { get; private set; }

		public static TrackConditionDistance Default { get; } = new TrackConditionDistance(new Dictionary<string, object>());
	}
}