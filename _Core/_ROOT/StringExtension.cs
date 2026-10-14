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
	}
}
