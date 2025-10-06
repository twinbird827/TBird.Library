using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	public enum GradeType
	{
		// G1
		G1古 = 20,

		G1ク = 19,

		G1障 = 18,

		// G2
		G2古 = 17,

		G2ク = 16,

		G2障 = 15,

		// G3
		G3古 = 14,

		G3ク = 13,

		G3障 = 12,

		// オープン
		オープン古 = 11,

		オープンク = 10,

		オープン障 = 9,

		// 条件戦
		勝3古 = 8,

		勝2古 = 7,

		勝2ク = 6,

		勝1古 = 5,

		勝1ク = 4,

		// 未勝利・新馬
		未勝利ク = 3,

		未勝利障 = 2,

		新馬ク = 1,

	}

	public static class GradeTypeExtensions
	{
		public static float GetGradeFeatures(this GradeType grade) => (float)(int)grade / 20F;
	}
}