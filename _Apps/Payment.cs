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

		public static Payment[] GetDefaults()
		{
			return new[]
			{
				Payment.Create複2(),
				Payment.Createワ1(),
				Payment.Create勝1(),
				Payment.Create連1(),
			};
		}

		public static Payment Create単4()
		{
			return new Payment()
			{
				pay = 400,
				head = "単4",
				func = (arr, payoutDetail, j) => Get三連単(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					arr.Where(x => x[j].GetInt32() == 2),
					arr.Where(x => x[j].GetInt32() == 3)
				)
			};
		}

		public static Payment Create複2()
		{
			return new Payment()
			{
				pay = 200,
				head = "複2",
				func = (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 2),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			};
		}

		public static Payment Create複3()
		{
			return new Payment()
			{
				pay = 300,
				head = "複3",
				func = (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1),
					arr.Where(x => x[j].GetInt32() <= 4),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			};
		}

		public static Payment Create複4()
		{
			return new Payment()
			{
				pay = 400,
				head = "複4",
				func = (arr, payoutDetail, j) => Get三連複(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 4),
					arr.Where(x => x[j].GetInt32() <= 4),
					arr.Where(x => x[j].GetInt32() <= 4)
				)
			};
		}

		public static Payment Createワ1()
		{
			return new Payment()
			{
				pay = 100,
				head = "ワ1",
				func = (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2)
				)
			};
		}

		public static Payment Createワ3()
		{
			return new Payment()
			{
				pay = 300,
				head = "ワ3",
				func = (arr, payoutDetail, j) => Getワイド(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 3)
				)
			};
		}

		public static Payment Create連1()
		{
			return new Payment()
			{
				pay = 100,
				head = "連1",
				func = (arr, payoutDetail, j) => Get馬連(payoutDetail,
					arr.Where(x => x[j].GetInt32() <= 2)
				)
			};
		}

		public static Payment Create勝1()
		{
			return new Payment()
			{
				pay = 100,
				head = "勝1",
				func = (arr, payoutDetail, j) => Get単勝(payoutDetail,
					arr.Where(x => x[j].GetInt32() == 1)
				)
			};
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