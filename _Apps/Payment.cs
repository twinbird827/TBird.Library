using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using Tensorflow.Gradients;

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

		public static List<object> GetPredictionBase<TSrc, TDst>(Dictionary<string, object> m, PredictionFactory<TSrc, TDst>[] facs) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
		{
			return new List<object>()
			{
				facs.Select(fac => fac.Predict((byte[])m["Features"], m["ﾚｰｽID"].GetInt64())).Average(),
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
				h: $"ス{rank}内{score}",
				f: (arr, _, j) => arr.Any(x => x[j].GetInt32() == score && x[1].GetInt32() <= rank) ? 100 : 0
			);
		}

		public static Payment Create倍A(int score)
		{
			// 指定したｽｺｱ
			return new Payment(
				p: 100,
				h: $"倍{score}A",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail, arr
					.Where(x => x[j].GetInt32() == score)
				)
			);
		}

		public static Payment Create倍B(int score)
		{
			// 指定したｽｺｱ以内で最も倍率が高い
			return new Payment(
				p: 100,
				h: $"倍{score}B",
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail, arr
					.Where(x => x[j].GetInt32() <= score)
					.OrderByDescending(x => x[2].GetSingle())
					.ThenBy(x => x[j].GetInt32())
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
			// 1, 2固定で3=指定したｽｺｱ
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

		public static Payment Create複A(int awase, int take)
		{
			// 1, 2固定で3=指定したｽｺｱ
			return new Payment(
				p: 100 * take,
				h: $"複{take}A{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32().Run(no => 2 < no && no <= awase)).OrderBy(x => x[j].GetInt32()).Take(take)
				)
			);
		}

		public static Payment Create複B(int awase, int take)
		{
			// 1, 2固定で3=倍率が最も高い
			return new Payment(
				p: 100 * take,
				h: $"複{take}B{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => 2 < x[j].GetInt32() && x[j].GetInt32() <= awase)
						.OrderByDescending(x => x[2].GetSingle())
						.ThenBy(x => x[j].GetInt32())
						.Take(take)
				)
			);
		}

		public static Payment Create複C(int awase, int take)
		{
			// 1固定で2, 3=倍率が高い順
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr.Where(x => 1 < x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(take + 1);
			return new Payment(
				p: 100 * take * (take - 1),
				h: $"複{take}C{awase}",
				f: (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					Temp(arr, j),
					Temp(arr, j)
				)
			);
		}

		public static Payment Create単A(int awase, int take)
		{
			// 1固定で倍率が高い順
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr
				.Where(x => 1 < x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(take);

			return new Payment(
				p: 100 * take,
				h: $"単{take}A{awase}",
				f: (arr, payoutDetail, j) => Get馬単(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					Temp(arr, j)
				)
			);
		}

		public static Payment Create単B(int awase, int take)
		{
			// 2固定で倍率が高い順
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr.Where(x => 1 < x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(take);

			return new Payment(
				p: 100 * take,
				h: $"単{take}B{awase}",
				f: (arr, payoutDetail, j) => Get馬単(payoutDetail,
					Temp(arr, j),
					arr.Where(x => x[j].GetInt32() == 1)
				)
			);
		}

		public static Payment Create単C(int awase, int take)
		{
			// 2=1, 2で倍率が低い方, 1=倍率が高い順
			int Temp1(List<List<object>> arr, int j) => arr
				.OrderBy(x => x[j].GetInt32())
				.ThenBy(x => x[2].GetSingle())
				.Take(2)
				.OrderBy(x => x[2].GetSingle())
				.First()[j].GetInt32();

			IEnumerable<List<object>> Temp2(List<List<object>> arr, int i, int j) => arr.Where(x => i != x[j].GetInt32() && x[j].GetInt32() <= awase)
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(take);

			return new Payment(
				p: 100 * take,
				h: $"単{take}C{awase}",
				f: (arr, payoutDetail, j) => Get馬単(payoutDetail,
					Temp2(arr, Temp1(arr, j), j),
					arr.Where(x => x[j].GetInt32() == Temp1(arr, j))
				)
			);
		}

		public static Payment CreateワA(int awase, int take)
		{
			// 1固定, 2=倍率高い順
			IEnumerable<List<object>> Temp(List<List<object>> arr, int j) => arr
				.Where(x => x[j].GetInt32().Run(no => 1 < no && no <= awase))
				.OrderByDescending(x => x[2].GetSingle())
				.ThenBy(x => x[j].GetInt32())
				.Take(take);

			return new Payment(
				p: 100 * take,
				h: $"ワ{take}A{awase}",
				f: (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
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

		public static Payment Create勝(int index)
		{
			// 指定したｽｺｱの単勝
			return new Payment(
				p: 100,
				h: "勝" + index,
				f: (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == index)
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

		private static object Getワイド(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());

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