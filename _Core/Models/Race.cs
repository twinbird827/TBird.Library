using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class Race
	{
		private Race(string raceId, string courseName, string place, int distance, string track, string trackCondition, string grade, long firstPrizeMoney, DateTime raceDate, int numberOfHorses)
		{
			RaceId = raceId;
			CourseName = courseName;
			Place = place;
			Distance = distance;
			DistanceCategory = Distance.ToDistanceCategory();
			Track = track;
			TrackType = Track.ToTrackType();
			TrackCondition = trackCondition;
			TrackConditionType = TrackCondition.ToTrackConditionType();
			Grade = grade.ToGrade();
			FirstPrizeMoney = firstPrizeMoney;
			RaceDate = raceDate;
			NumberOfHorses = numberOfHorses;
			IsInternational = Grade.IsG1() && FirstPrizeMoney > 200000000;
			IsAgedHorseRace = Grade.IsCLASSIC() == false;
		}

		public Race(DbDataReader r, int offset = 0) : this(
			r.GetValue(offset + 0).Str(),       // ﾚｰｽID
			r.GetValue(offset + 1).Str(),       // ﾚｰｽ名
			r.GetValue(offset + 2).Str(),       // 開催場所
			r.GetValue(offset + 3).Int32(),     // 距離
			r.GetValue(offset + 4).Str(),       // 馬場
			r.GetValue(offset + 5).Str(),       // 馬場状態
			r.GetValue(offset + 6).Str(),       // ﾗﾝｸ1
			r.GetValue(offset + 7).Int64(),     // 優勝賞金
			r.GetValue(offset + 8).Date(),      // 開催日
			r.GetValue(offset + 9).Int32()      // 頭数
		)
		{ }

		public Race(Dictionary<string, object> x) : this(
			x.Get("ﾚｰｽID").Str(),
			x.Get("ﾚｰｽ名").Str(),
			x.Get("開催場所").Str(),
			x.Get("距離").Int32(),
			x.Get("馬場").Str(),
			x.Get("馬場状態").Str(),
			x.Get("ﾗﾝｸ1").Str(),
			x.Get("優勝賞金").Int64(),
			x.Get("開催日").Date(),
			x.Get("頭数").Int32()
		)
		{ }

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
		public string TrackDistance => $"{Track}-{Distance}";
		public string TrackConditionDistance => $"{Track}-{TrackCondition}-{Distance}";

		public override bool Equals(object? obj)
		{
			return obj is Race x && x.RaceId == RaceId;
		}

		public override int GetHashCode()
		{
			return RaceId.GetHashCode();
		}
	}
}