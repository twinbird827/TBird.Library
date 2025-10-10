using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Netkeiba.Models
{
	public class Oikiri
	{
		public Oikiri(Dictionary<string, object> x, RaceDetail detail)
		{
			Detail = detail;
			Course = x["コース"].Str();
			Track = x["馬場"].Str();
			Rider = x["乗り役"].Str();
			Time1 = x["時間1"].Single();
			Time2 = x["時間2"].Single();
			Time3 = x["時間3"].Single();
			Time4 = x["時間4"].Single();
			Time5 = x["時間5"].Single();
			TimeRating1 = x["時間評価1"].Str();
			TimeRating2 = x["時間評価2"].Str();
			TimeRating3 = x["時間評価3"].Str();
			TimeRating4 = x["時間評価4"].Str();
			TimeRating5 = x["時間評価5"].Str();
			Adaptation = x["脚色"].Str();
			Comment = x["一言"].Str();
			Rating = x["評価"].Str();
		}

		public RaceDetail Detail { get; }
		public string Course { get; set; }
		public string Track { get; set; }
		public string Rider { get; set; }
		public float Time1 { get; set; }
		public float Time2 { get; set; }
		public float Time3 { get; set; }
		public float Time4 { get; set; }
		public float Time5 { get; set; }

		public string TimeRating1 { get; set; }
		public string TimeRating2 { get; set; }
		public string TimeRating3 { get; set; }
		public string TimeRating4 { get; set; }
		public string TimeRating5 { get; set; }
		public string Adaptation { get; set; }
		public string Comment { get; set; }
		public string Rating { get; set; }
	}
}