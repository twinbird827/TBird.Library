using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	public class RankingTrain
	{
		public static readonly RankingTrain Default = new RankingTrain(DateTime.Now, "", 0, 0, 0);

		public RankingTrain()
		{
			Date = DateTime.MinValue;
			Path = string.Empty;
			Grade = string.Empty;
			NDCG1 = 0;
			NDCG3 = 0;
			NDCG5 = 0;
		}

		public RankingTrain(DateTime date, string grade, double ndcg1, double ndcg3, double ndcg5)
		{
			Date = date;
			Path = $@"model\Ranking_{grade}_{date.ToString("yyyyMMdd-HHmmss")}.model";
			Grade = grade;
			NDCG1 = ndcg1;
			NDCG3 = ndcg3;
			NDCG5 = ndcg5;
		}

		public DateTime Date { get; set; }

		public string Path { get; set; }

		public string Grade { get; set; }

		public double NDCG1 { get; set; }

		public double NDCG3 { get; set; }

		public double NDCG5 { get; set; }
	}
}