using Netkeiba.Models;
using TBird.Core;

namespace Netkeiba
{
	public static class StringExtension
	{
		public static GradeType ToGrade(this string grade) => EnumUtil.ToEnum<GradeType>(grade);

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
	}
}
