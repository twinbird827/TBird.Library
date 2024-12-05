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
		private const float RankRatePow = 0.25F;

		public static readonly Dictionary<string, float> RankRate = new Dictionary<string, float>()
		{
			{ "G1古",       8.00F.Pow(RankRatePow) },
			{ "G2古",       7.50F.Pow(RankRatePow) },
			{ "G1ク",       7.00F.Pow(RankRatePow) },
			{ "G3古",       6.50F.Pow(RankRatePow) },
			{ "G2ク",       6.00F.Pow(RankRatePow) },
			{ "オープン古", 5.50F.Pow(RankRatePow) },
			{ "G3ク",       5.00F.Pow(RankRatePow) },
			{ "オープンク", 4.50F.Pow(RankRatePow) },
			{ "3勝古",      4.00F.Pow(RankRatePow) },
			{ "2勝古",      3.50F.Pow(RankRatePow) },
			{ "2勝ク",      3.00F.Pow(RankRatePow) },
			{ "1勝古",      2.50F.Pow(RankRatePow) },
			{ "1勝ク",      2.00F.Pow(RankRatePow) },
			{ "未勝利ク",   1.50F.Pow(RankRatePow) },
			{ "新馬ク",     1.00F.Pow(RankRatePow) },
			{ "G1障",       5.00F.Pow(RankRatePow) },
			{ "G2障",       4.00F.Pow(RankRatePow) },
			{ "G3障",       3.00F.Pow(RankRatePow) },
			{ "オープン障", 2.00F.Pow(RankRatePow) },
			{ "未勝利障",   1.00F.Pow(RankRatePow) },
		};

		public static readonly string[] RankAges = new[]
		{
			"G1ク",
			"G1古",
			"G2ク",
			"G2古",
			"G3ク",
			"G3古",
			"オープンク",
			"オープン古",
			"3勝古",
			"2勝古",
			"1勝ク",
			"1勝古",
			"未勝利ク",
			"新馬ク",
			"G1障",
			"G2障",
			"G3障",
			"オープン障",
			"未勝利障",
		};

		public static string Sqlitepath { get; } = Path.Combine(@"database", "database.sqlite3");

		public static SQLiteControl CreateSQLiteControl() => new SQLiteControl(Sqlitepath, string.Empty, false, false, 1024 * 1024, true);

		public static float[] ToSingles(byte[] bytes) => Enumerable.Range(0, bytes.Length / 4).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();

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
				await Task.Delay(100);

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

		public static Task<List<string>> Getﾗﾝｸ2(SQLiteControl conn)
		{
			return Task.Run(() => new[] { "RANK5", "RANK4", "RANK3", "RANK2", "RANK1" }.ToList());
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
			"GIII", "GII", "GI", "G1)", "G2)", "G3)", "(G)", "(L)", "オープン", "３勝クラス", "3勝クラス", "(3勝)", "1600万下", "２勝クラス", "2勝クラス", "1000万下", "１勝クラス", "1勝クラス", "500万下", "未勝利", "新馬"
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
			{ "３勝クラス", "3勝" },
			{ "3勝クラス", "3勝" },
			{ "(3勝)", "3勝" },
			{ "1600万下", "3勝" },
			{ "２勝クラス", "2勝" },
			{ "2勝クラス", "2勝" },
			{ "1000万下", "2勝" },
			{ "１勝クラス", "1勝" },
			{ "1勝クラス", "1勝" },
			{ "500万下", "1勝" },
			{ "未勝利", "未勝利" },
			{ "新馬", "新馬" },
		};

		public static string Getﾗﾝｸ1(string ﾚｰｽ名, string ｸﾗｽ)
		{
			return ﾗﾝｸ1[ﾗﾝｸ.FirstOrDefault(ﾚｰｽ名.Contains) ?? ﾗﾝｸ.FirstOrDefault(ｸﾗｽ.Contains) ?? string.Empty];
		}

		public static readonly Dictionary<string, string> ﾗﾝｸ2 = new()
		{
			{ "G1", "RANK1" },
			{ "G2", "RANK1" },
			{ "G3", "RANK1" },
			{ "オープン", "RANK2" },
			{ "3勝", "RANK2" },
			{ "2勝", "RANK3" },
			{ "1勝", "RANK3" },
			{ "未勝利", "RANK4" },
			{ "新馬", "RANK5" },
		};

		public static string Getﾗﾝｸ2(string ﾗﾝｸ1)
		{
			return ﾗﾝｸ2[ﾗﾝｸ1];
		}

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
	}
}