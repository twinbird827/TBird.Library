using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public class Payment
	{
		public int pay { get; private set; }
		public string head { get; private set; }
		public Func<List<List<object>>, Dictionary<string, string>, int, object> func { get; private set; }

		public Payment(int p, string h, Func<List<List<object>>, Dictionary<string, string>, int, object> f)
		{
			pay = p;
			head = h;
			func = f;
		}

		public const int OrderByDescendingScoreIndex = 7;

		public static List<object> GetPredictionBase<TSrc, TDst>(Dictionary<string, object> m, PredictionFactory<TSrc, TDst> fac) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
		{
			return new List<object>()
			{
				fac.Predict((byte[])m["Features"], m["ﾚｰｽID"].GetInt64()),
				m["着順"],
				m["単勝"],
				string.Empty,
				string.Empty,
				string.Empty,
				m["馬番"],
			};
		}

		public static void AddOrderByDescendingScoreIndex(List<List<object>> racs)
		{
			var n = 1;
			racs.OrderByDescending(x => x[0].GetDouble()).ForEach(x => x.Add(n++));
		}

		public static Payment Create順(int score, int rank)
		{
			// 指定のｽｺｱがrank以内であるかの割合を確認する
			return new Payment(
				p: 100,
				h: $"ス{score}内{rank}",
				f: (arr, _, j) => arr.Any(x => x[j].GetInt32() == score && x[1].GetInt32() <= rank) ? 100 : 0
			);
		}

		public static Payment Create倍A(int score)
		{
			// 指定したｽｺｱ以内で最も倍率が高い
			return new Payment(
				p: 100,
				h: $"倍{score}A",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail, arr
					.Where(x => x[j].GetInt32() <= score)
					.OrderByDescending(x => x[2].GetSingle())
					.ThenBy(x => x[j].GetInt32())
					.Take(1)
				)
			);
		}

		public static Payment Create倍B(int score, int rank)
		{
			// 指定したｽｺｱ以内で倍率がrank番目に低い
			return new Payment(
				p: 100,
				h: $"倍{score}B{rank}",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail, arr
					.Where(x => x[j].GetInt32() <= score)
					.OrderBy(x => x[2].GetSingle())
					.ThenBy(x => x[j].GetInt32())
					.Skip(rank - 1)
					.Take(1)
				)
			);
		}

		public static Payment Create単4()
		{
			return new Payment(
				p: 400,
				h: "単4",
				f: (arr, payoutDetail, j) => Get三連単(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			);
		}

		public static Payment Create複1A(int awase)
		{
			return new Payment(
				p: 100,
				h: $"複1A{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					arr.Where(x => x[j].GetInt32() == 2),
					arr.Where(x => x[j].GetInt32() == awase)
				)
			);
		}

		public static Payment Create複1B(int awase)
		{
			return new Payment(
				p: 100,
				h: $"複1B{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					arr.Where(x => x[j].GetInt32() == 2),
					arr.Where(x => 2 < x[j].GetInt32() && x[j].GetInt32() <= awase)
						.OrderByDescending(x => x[2].GetSingle())
						.ThenBy(x => x[j].GetInt32())
						.Take(1)
				)
			);
		}

		public static Payment Create複1C(int awase)
		{
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr.Where(x => 1 < x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(2);
			return new Payment(
				p: 100,
				h: $"複1C{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					Temp(arr, j),
					Temp(arr, j)
				)
			);
		}

		public static Payment Create複2C(int awase)
		{
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr.Where(x => 2 < x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(2);
			return new Payment(
				p: 200,
				h: $"複2C{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 2),
					Temp(arr, j)
				)
			);
		}

		public static Payment Create複2()
		{
			return new Payment(
				p: 200,
				h: "複2",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			);
		}

		public static Payment Create複3()
		{
			return new Payment(
				p: 300,
				h: "複3",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					arr.Where(x => x[j].GetInt32() <= 4),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			);
		}

		public static Payment Create複4()
		{
			return new Payment(
				p: 400,
				h: "複4",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 4),
					arr.Where(x => x[j].GetInt32() <= 4),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			);
		}

		public static Payment Createワ1()
		{
			return new Payment(
				p: 100,
				h: "ワ1",
				f: (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2)
				)
			);
		}

		public static Payment Createワ1A()
		{
			return new Payment(
				p: 100,
				h: "ワ1A",
				f: (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32().Run(i => i == 1 || i == 2))
				)
			);
		}

		public static Payment Createワ1B()
		{
			return new Payment(
				p: 100,
				h: "ワ1B",
				f: (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32().Run(i => i == 1 || i == 3))
				)
			);
		}

		public static Payment Createワ1C()
		{
			return new Payment(
				p: 100,
				h: "ワ1C",
				f: (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32().Run(i => i == 2 || i == 3))
				)
			);
		}

		public static Payment Createワ3()
		{
			return new Payment(
				p: 300,
				h: "ワ3",
				f: (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 3)
				)
			);
		}

		public static Payment Create連1()
		{
			return new Payment(
				p: 100,
				h: "連1",
				f: (arr, payoutDetail, j) => Get馬連(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2)
				)
			);
		}

		public static Payment Create馬(int take, int awase)
		{
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr.Where(x => 1 < x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(take);

			return new Payment(
				p: 100 * take,
				h: "馬" + take,
				f: (arr, payoutDetail, j) => Get馬単(payoutDetail,
					Temp(arr, j),
					arr.Where(x => x[j].GetInt32() == 1)
				)
			);
		}

		public static Payment Create勝1()
		{
			return new Payment(
				p: 100,
				h: "勝1",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1)
				)
			);
		}

		public static Payment Create勝2()
		{
			return new Payment(
				p: 100,
				h: "勝2",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 2)
				)
			);
		}

		public static Payment Create勝3()
		{
			return new Payment(
				p: 100,
				h: "勝3",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 3)
				)
			);
		}

		public static Payment Create勝4()
		{
			return new Payment(
				p: 100,
				h: "勝4",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 4)
				)
			);
		}

		public static Payment Create勝5()
		{
			return new Payment(
				p: 100,
				h: "勝5",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 5)
				)
			);
		}

		public static Payment Create勝6()
		{
			return new Payment(
				p: 100,
				h: "勝6",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 6)
				)
			);
		}

		public static Payment Create勝7()
		{
			return new Payment(
				p: 100,
				h: "勝7",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 7)
				)
			);
		}

		public static Payment Create勝8()
		{
			return new Payment(
				p: 100,
				h: "勝8",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 8)
				)
			);
		}

		public static Payment Create勝9()
		{
			return new Payment(
				p: 100,
				h: "勝9",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 9)
				)
			);
		}

		public static Payment Create勝A()
		{
			return new Payment(
				p: 100,
				h: "勝A",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 10)
				)
			);
		}

		public static Payment Create勝B()
		{
			return new Payment(
				p: 100,
				h: "勝B",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 11)
				)
			);
		}

		public static Payment Create勝C()
		{
			return new Payment(
				p: 100,
				h: "勝C",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 12)
				)
			);
		}

		private static readonly int[] Zeros = new int[] { 0 };

		private static object GetResult(Dictionary<string, string> payoutDetail, string key, string[] arr)
		{
			return payoutDetail.ContainsKey(key)
				? Zeros.Concat(payoutDetail[key].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

		private static object Get三連単(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2, IEnumerable<List<object>> arr3)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());
			var karr = arr3.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j => karr.Where(k => k != i && j != k).Select(k => $"{i}-{j}-{k}"))).ToArray();

			return GetResult(payoutDetail, "三連単", arr);
		}

		private static object Get三連複(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2, IEnumerable<List<object>> arr3)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());
			var karr = arr3.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j => karr.Where(k => k != i && j != k).SelectMany(k =>
			{
				return new[]
				{
					$"{i}-{j}-{k}",
					$"{i}-{k}-{j}",
					$"{j}-{i}-{k}",
					$"{j}-{k}-{i}",
					$"{k}-{j}-{i}",
					$"{k}-{i}-{j}",
				};
			}))).ToArray();

			return GetResult(payoutDetail, "三連複", arr);
		}

		private static object Getワイド(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr1.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j =>
			{
				return new[]
				{
					$"{i}-{j}",
					$"{j}-{i}",
				};
			})).ToArray();

			return GetResult(payoutDetail, "ワイド", arr);
		}

		private static object Get馬連(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr1.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j =>
			{
				return new[]
				{
					$"{i}-{j}",
					$"{j}-{i}",
				};
			})).ToArray();

			return GetResult(payoutDetail, "馬連", arr);
		}

		private static object Get馬単(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).Select(j => $"{i}-{j}")).ToArray();

			return GetResult(payoutDetail, "馬単", arr);
		}

		private static object Get単勝(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1)
		{
			var arr = arr1.Select(x => x[6].GetInt32().ToString()).ToArray();

			return GetResult(payoutDetail, "単勝", arr);
		}

	}
}