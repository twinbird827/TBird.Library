namespace Netkeiba.Models
{
	public static class GradeTypeExtension
	{
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

		public static float GetGradeFeatures(this GradeType grade) => grade.IsG1()
			? 1.0F
			: grade.IsG2()
			? 0.85F
			: grade.IsG3()
			? 0.70F
			: grade.IsOPEN()
			? 0.60F
			: grade switch
			{
				GradeType.勝3古 => 0.50F,
				GradeType.勝2古 => 0.45F,
				GradeType.勝2ク => 0.40F,
				GradeType.勝1古 => 0.35F,
				GradeType.勝1ク => 0.30F,
				GradeType.未勝利ク => 0.15F,
				GradeType.未勝利障 => 0.15F,
				_ => 0.10F,
			};
	}
}
