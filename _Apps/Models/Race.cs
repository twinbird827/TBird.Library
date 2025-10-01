using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class Race
	{
		public Race(Dictionary<string, object> x)
		{
			try
			{
				RaceId = x.Get("ﾚｰｽID").Str();
				CourseName = x.Get("ﾚｰｽ名").Str();
				Place = x.Get("開催場所").Str();
				Distance = x.Get("距離").Int32();
				DistanceCategory = Distance.ToDistanceCategory();
				Track = x.Get("馬場").Str();
				TrackType = Track.ToTrackType();
				TrackCondition = x.Get("馬場状態").Str();
				TrackConditionType = TrackCondition.ToTrackConditionType();
				Grade = x.Get("ﾗﾝｸ1").Str().ToGrade();
				FirstPrizeMoney = x.Get("優勝賞金").Int64();
				NumberOfHorses = x.Get("頭数").Int32();
				RaceDate = x.Get("開催日").Date();
				IsInternational = Grade.IsG1() && FirstPrizeMoney > 200000000;
				IsAgedHorseRace = Grade.IsCLASSIC() == false;
			}
			catch (Exception ex)
			{
				MessageService.Debug(ex.ToString());
				throw;
			}
		}

		public string RaceId { get; }
		public string CourseName { get; }
		public string Place { get; private set; }
		public int Distance { get; private set; }
		public DistanceCategory DistanceCategory { get; private set; }
		public string Track { get; }
		public TrackType TrackType { get; private set; }
		public string TrackCondition { get; }
		public TrackConditionType TrackConditionType { get; private set; }
		public GradeType Grade { get; private set; }
		public long FirstPrizeMoney { get; private set; }
		public int NumberOfHorses { get; private set; }
		public DateTime RaceDate { get; private set; }
		public float AverageRating { get; set; }
		public bool IsInternational { get; }
		public bool IsAgedHorseRace { get; }
	}
}