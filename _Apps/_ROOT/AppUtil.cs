using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;

namespace Netkeiba
{
	public static class AppUtil
	{
		public const float RankRateBase = 7F;

		public static string Sqlitepath { get; } = Path.Combine(@"database", "database.sqlite3");

		public static SQLiteControl CreateSQLiteControl() => new SQLiteControl(Sqlitepath, string.Empty, false, false, 1024 * 1024, true);

		public static float[] ToSingles(byte[] bytes) => Enumerable.Range(0, bytes.Length / 4).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();

		public static float[] ToSingles(byte[] bytes, string rank) => FilterFeatures(ToSingles(bytes), rank);

		public static float[] FilterFeatures(float[] features, string rank)
		{
			var filters = AppSetting.Instance.DicCor.First(x => x.Key == rank).Value.Split(',').Select(x => x.GetInt32()).ToArray();
			return features.Where((x, i) => !filters.Contains(i)).ToArray();
		}

		public static string GetInnerHtml(this AngleSharp.Dom.IElement x)
		{
			var innerhtml = x.GetElementsByTagName("span").Any()
				? x.GetElementsByTagName("span").First().InnerHtml
				: x.GetElementsByTagName("div").Any()
				? x.GetElementsByTagName("div").First().InnerHtml
				: x.InnerHtml;
			return Regex.Replace(innerhtml.Replace("&nbsp;", " "), " +", " ");
		}

		public static string GetHrefAttribute(this AngleSharp.Dom.IElement x, string attribute)
		{
			return $"{x.GetElementsByTagName("a").Select(a => a.GetAttribute(attribute)).FirstOrDefault() ?? string.Empty}";
		}

		public static string GetHrefInnerHtml(this AngleSharp.Dom.IElement x)
		{
			var innerhtml = x.GetElementsByTagName("a").Any()
				? x.GetElementsByTagName("a").First().InnerHtml
				: x.InnerHtml;
			return Regex.Replace(innerhtml.Replace("&nbsp;", " "), " +", " ");
		}

		public static string GetTryCatch(this AngleSharp.Dom.IElement x, Func<string, string> func)
		{
			try
			{
				return func(x.GetInnerHtml());
			}
			catch
			{
				return string.Empty;
			}
		}

		public static async Task<IHtmlDocument> GetDocument(bool login, string url)
		{
			if (_loginsession.AddMinutes(10) < DateTime.Now)
			{
				if (_logincontext != null) _logincontext.Dispose();
				_logincontext = null;
				if (_guestcontext != null) _guestcontext.Dispose();
				_guestcontext = null;
				_loginsession = DateTime.Now;
			}

			var config = Configuration.Default.WithDefaultLoader().WithJs().WithDefaultCookies();

			if (login)
			{
				if (_logincontext == null)
				{
					_logincontext = BrowsingContext.New(config);

					await _logincontext.OpenAsync(@"https://regist.netkeiba.com/account/?pid=login");

					if (_logincontext.Active == null) throw new ApplicationException();

					await _logincontext.Active.QuerySelectorAll<IHtmlFormElement>("form").First(x => x.GetAttribute("action") == @"https://regist.netkeiba.com/account/").SubmitAsync(new
					{
						login_id = AppSetting.Instance.NetkeibaId,
						pswd = AppSetting.Instance.NetkeibaPassword
					});
				}
			}
			else
			{
				if (_guestcontext == null)
				{
					_guestcontext = BrowsingContext.New(config);
				}
			}

			var context = login ? _logincontext : _guestcontext;

			if (context == null) throw new ApplicationException("");

			using (await Locker.LockAsync(_guid, _pararell))
			{
				await Task.Delay(1250);

				MainViewModel.AddLog($"req: {url}");

				return await context.OpenAsync(url).RunAsync(x => ((x.DocumentElement as IHtmlDocument) ?? x as IHtmlDocument).NotNull());
			}

			//if (login)
			//{
			//	var selenium = await TBirdSeleniumFactory.CreateSelenium(10);

			//	selenium.SetInitialize(driver =>
			//	{
			//		selenium.GoToUrl(@"https://regist.netkeiba.com/account/?pid=login");

			//		driver.FindElement(By.Name("login_id")).SendKeys(AppSetting.Instance.NetkeibaId);
			//		driver.FindElement(By.Name("pswd")).SendKeys(AppSetting.Instance.NetkeibaPassword);
			//		driver.FindElement(By.XPath(@"//input[@alt='ログイン']")).Click();
			//	});

			//	MainViewModel.AddLog($"req: {url}");
			//	return await selenium.Execute(async driver =>
			//	{
			//		selenium.GoToUrl(url);

			//		var res = driver.PageSource;

			//		return await _parser.ParseDocumentAsync(res);
			//	}).RunAsync(async x => await x);
			//}
			//else
			//{
			//	using (await Locker.LockAsync(_guid, _pararell))
			//	{
			//		MainViewModel.AddLog($"req: {url}");

			//		var res = await WebUtil.GetStringAsync(url, _srcenc, _dstenc);

			//		var doc = await _parser.ParseDocumentAsync(res);

			//		return doc;
			//	}
			//}
		}

		private static string _guid = Guid.NewGuid().ToString();
		private static int _pararell = 1;

		private static IBrowsingContext? _logincontext;
		private static DateTime _loginsession = DateTime.Now.AddDays(-1);
		private static IBrowsingContext? _guestcontext;
		//      private static DateTime _guestsession = DateTime.Now.AddDays(-1);

		//      private static HtmlParser _parser = new HtmlParser();
		//private static Encoding _srcenc = Encoding.GetEncoding("euc-jp");
		//private static Encoding _dstenc = Encoding.UTF8;

		public static async Task<IEnumerable<string>> GetFileHeaders(string path, string sepa)
		{
			var csvenum = File.ReadLinesAsync(path).GetAsyncEnumerator();
			var csvheader = await csvenum.MoveNextAsync() ? csvenum.Current : string.Empty;
			return csvheader.Split(sepa);
		}

		public static async Task<IEnumerable<T>> GetFileHeaders<T>(string path, string sepa, Func<string, T> func)
		{
			return await GetFileHeaders(path, sepa).ContinueWith(x => x.Result.Select(func));
		}

		public static async Task<IEnumerable<T>> GetFileHeaders<T>(string path, string sepa, Func<string, int, T> func)
		{
			return await GetFileHeaders(path, sepa).ContinueWith(x => x.Result.Select(func));
		}

		private static Task<List<string>> GetDistinct(SQLiteControl conn, string x)
		{
			return conn.GetRows(r => r.Get<string>(0), $"SELECT DISTINCT {x} FROM t_orig ORDER BY {x}");
		}

		public static Task<List<string>> Get馬性(SQLiteControl conn)
		{
			return GetDistinct(conn, "馬性");
		}

		public static Task<List<string>> Get調教場所(SQLiteControl conn)
		{
			return GetDistinct(conn, "調教場所");
		}

		public static Task<List<string>> Get追切(SQLiteControl conn)
		{
			return Task.Run(() => new List<string>(new[] { "", "E", "D", "C", "B", "A" }));
		}

		private static readonly string[] ﾗﾝｸ = new[]
		{
			"GIII", "GII", "GI", "G1)", "G2)", "G3)", "(G)", "(L)", "オープン", "３勝クラス", "3勝クラス", "(3勝)", "1600万下", "２勝クラス", "2勝クラス", "1000万下", "１勝クラス", "1勝クラス", "500万下", "未勝利", "新馬", "OP"
		};

		private static readonly Dictionary<string, string> ﾗﾝｸ1 = new()
		{
			{ "GIII", "G3" },
			{ "GII", "G2" },
			{ "GI", "G1" },
			{ "G1)", "G1" },
			{ "G2)", "G2" },
			{ "G3)", "G3" },
			{ "(G)", "オープン" },
			{ "(L)", "オープン" },
			{ "オープン", "オープン" },
			{ "３勝クラス", "勝3" },
			{ "3勝クラス", "勝3" },
			{ "(3勝)", "勝3" },
			{ "1600万下", "勝3" },
			{ "２勝クラス", "勝2" },
			{ "2勝クラス", "勝2" },
			{ "1000万下", "勝2" },
			{ "１勝クラス", "勝1" },
			{ "1勝クラス", "勝1" },
			{ "500万下", "勝1" },
			{ "未勝利", "未勝利" },
			{ "新馬", "新馬" },
			{ "OP", "オープン" },
		};

		public static string Getﾗﾝｸ1(string ﾚｰｽ名, string ｸﾗｽ)
		{
			try
			{
				return ﾗﾝｸ1[ﾗﾝｸ.FirstOrDefault(ﾚｰｽ名.Contains) ?? ﾗﾝｸ.FirstOrDefault(ｸﾗｽ.Contains) ?? string.Empty];
			}
			catch
			{
				throw;
			}
		}

		public static readonly Dictionary<string, string> ﾗﾝｸ2 = new()
		{
			{ "勝1ク", "勝ク" },
			{ "勝1古", "勝古" },
			{ "勝2ク", "勝ク" },
			{ "勝2古", "勝古" },
			{ "勝3古", "勝古" },
			{ "G1ク", "オク" },
			{ "G1古", "オ古" },
			{ "G1障", "オ障" },
			{ "G2ク", "オク" },
			{ "G2古", "オ古" },
			{ "G2障", "オ障" },
			{ "G3ク", "オク" },
			{ "G3古", "オ古" },
			{ "G3障", "オ障" },
			{ "オープンク", "オク" },
			{ "オープン古", "オ古" },
			{ "オープン障", "オ障" },
			{ "新馬ク", "新馬" },
			{ "未勝利ク", "未勝利ク" },
			{ "未勝利障", "未勝利障" },
		};

		public static string[] ﾗﾝｸ2Arr
		{
			get => _ﾗﾝｸ2 = _ﾗﾝｸ2 ?? ﾗﾝｸ2.Values.Distinct().ToArray();
		}
		private static string[]? _ﾗﾝｸ2;

		public static int Getﾗﾝｸ2(object rank) => ﾗﾝｸ2Arr.IndexOf(rank.Str());

		public static void DeleteEndress(string path)
		{
			_ = WpfUtil.ExecuteOnBACK(async () =>
			{
				while (File.Exists(path))
				{
					await Task.Delay(1000);

					FileUtil.Delete(path);
				}
			}).ConfigureAwait(false);
		}

		public static IEnumerable<int> OrderBys => AppSetting.Instance.OrderBys.Split(',').Select(x => x.GetInt32());

		public static string[] DropKeys => ["ﾚｰｽID", "開催日数", "着順", "単勝", "人気", "距離", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID", "調教場所", "枠番", "馬番"];

		public static byte[] CreateFeatures(Dictionary<string, object> ins)
		{
			return ins.Keys.Where(x => !DropKeys.Contains(x)).ToArray().SelectMany(x => BitConverter.GetBytes(ins.SINGLE(x))).ToArray();
		}
	}
}