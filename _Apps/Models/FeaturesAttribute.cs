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
		All = 65536,

		Horse = 1,

		Oikiri = 2,

		Sire = 4,

		DamSire = 8,

		SireBros = 16,

		DamSireBros = 32,

		SireDamSireBros = 64,

		Jockey = 128,

		JockeyPlace = 256,

		JockeyTrack = 512,

		Trainer = 1024,

		Breeder = 2048,

		TrainerBreeder = 4096,

		Blood = 8192,

		Connection = 16384,

		Bros = 32768,
	}
}