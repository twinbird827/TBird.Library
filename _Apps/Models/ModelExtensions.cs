using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public static class ModelExtensions
	{
		public static GradeType ToGrade(this string grade) => EnumUtil.ToEnum<GradeType>(grade);

		public static bool IsG1(this GradeType grade) => grade switch
		{
			GradeType.G1ク => true,
			GradeType.G1古 => true,
			GradeType.G1障 => true,
			_ => false
		};

		public static bool IsG2(this GradeType grade) => grade switch
		{
			GradeType.G2ク => true,
			GradeType.G2古 => true,
			GradeType.G2障 => true,
			_ => false
		};

		public static bool IsG3(this GradeType grade) => grade switch
		{
			GradeType.G3ク => true,
			GradeType.G3古 => true,
			GradeType.G3障 => true,
			_ => false
		};

		public static bool IsOPEN(this GradeType grade) => grade switch
		{
			GradeType.オープンク => true,
			GradeType.オープン古 => true,
			GradeType.オープン障 => true,
			_ => false
		};

		public static bool IsCLASSIC(this GradeType grade) => grade switch
		{
			GradeType.G1ク => true,
			GradeType.G2ク => true,
			GradeType.G3ク => true,
			GradeType.オープンク => true,
			GradeType.勝2ク => true,
			GradeType.勝1ク => true,
			GradeType.未勝利ク => true,
			GradeType.新馬ク => true,
			_ => false,
		};

		public static DistanceCategory ToDistanceCategory(this int distance) => distance switch
		{
			<= 1400 => DistanceCategory.Sprint,
			<= 1800 => DistanceCategory.Mile,
			<= 2200 => DistanceCategory.Middle,
			_ => DistanceCategory.Long
		};

		public static TrackType ToTrackType(this string track) => track switch
		{
			"芝" => TrackType.Grass,
			"ダート" => TrackType.Dirt,
			_ => TrackType.Unknown
		};

		public static TrackConditionType ToTrackConditionType(this string condition) => condition switch
		{
			"良" => TrackConditionType.Good,
			"稍重" => TrackConditionType.SlightlyHeavy,
			"重" => TrackConditionType.Heavy,
			"不良" => TrackConditionType.Poor,
			_ => TrackConditionType.Unknown
		};

		public static float AdjustedInverseScoreAverage(this IEnumerable<RaceDetail> arr, float def = 0.1F) => arr.Aggregate(tmp => tmp.Average(x => x.CalculateAdjustedInverseScore()), def);
	}
}