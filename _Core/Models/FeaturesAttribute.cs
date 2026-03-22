using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	[AttributeUsage(AttributeTargets.Property)]
	public class FeaturesAttribute : Attribute
	{
		public FeaturesAttribute()
		{
			Normalization = false;
		}

		public FeaturesType Type { get; set; }

		public bool Normalization { get; set; }

		public static FeaturesType[] GetTargetTypes() => new[]
		{
			FeaturesType.Horse,
			FeaturesType.Jockey,
			FeaturesType.Blood,
			FeaturesType.Connection,
		};
	}

	[Flags]
	public enum FeaturesType
	{
		Total = 1,

		Horse = 2,

		Jockey = 4,

		Blood = 8,

		Connection = 16,

		TotalOther = 32,

		HorseOther = 64,

		JockeyOther = 128,

		BloodOther = 256,

		ConnectionOther = 512,

		TotalLarge = 1024,

		TotalMedium = 2048,

		TotalSmall = 4096,

		TotalRaw = 8192,

		TotalRank = 16384,

	}
}