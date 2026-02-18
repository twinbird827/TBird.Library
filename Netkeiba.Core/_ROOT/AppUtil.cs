using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Netkeiba.Models;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Web;

namespace Netkeiba
{
	public static class AppUtil
	{
		public static string Sqlitepath { get; } = Path.Combine(Path.Combine(PathSetting.Instance.RootDirectory, @"database"), "database.sqlite3");

		public static SQLiteControl CreateSQLiteControl() => new SQLiteControl(Sqlitepath, string.Empty, false, false, 1024 * 1024, true);

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

				// TODO MainViewModel.AddLog($"req: {url}");
				Console.WriteLine($"req: {url}");

				return await context.OpenAsync(url).RunAsync(x => ((x.DocumentElement as IHtmlDocument) ?? x as IHtmlDocument).NotNull());
			}

			//var selenium = await TBirdSeleniumFactory.CreateSelenium(1);

			//if (login)
			//{
			//	selenium.SetInitialize(driver =>
			//	{
			//		selenium.GoToUrl(@"https://regist.netkeiba.com/account/?pid=login");

			//		driver.FindElement(By.Name("login_id")).SendKeys(AppSetting.Instance.NetkeibaId);
			//		driver.FindElement(By.Name("pswd")).SendKeys(AppSetting.Instance.NetkeibaPassword);
			//		driver.FindElement(By.XPath(@"//input[@alt='ログイン']")).Click();
			//	});

			//}
			//else
			//{
			//	//using (await Locker.LockAsync(_guid, _pararell))
			//	//{
			//	//	MainViewModel.AddLog($"req: {url}");

			//	//	var res = await WebUtil.GetStringAsync(url, _srcenc, _dstenc);

			//	//	var doc = await _parser.ParseDocumentAsync(res);

			//	//	return doc;
			//	//}
			//}

			//MainViewModel.AddLog($"req: {url}");
			//return await selenium.Execute(async driver =>
			//{
			//	selenium.GoToUrl(url);

			//	var res = driver.PageSource;

			//	return await _parser.ParseDocumentAsync(res);
			//}).RunAsync(async x => await x);
		}

		private static string _guid = Guid.NewGuid().ToString();
		private static int _pararell = 1;

		private static IBrowsingContext? _logincontext;
		private static DateTime _loginsession = DateTime.Now.AddDays(-1);
		private static IBrowsingContext? _guestcontext;
		//      private static DateTime _guestsession = DateTime.Now.AddDays(-1);

		private static HtmlParser _parser = new HtmlParser();
		private static Encoding _srcenc = Encoding.GetEncoding("euc-jp");
		private static Encoding _dstenc = Encoding.UTF8;

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

		public static void DeleteEndress(string path)
		{
			_ = Task.Run(async () =>
			{
				while (File.Exists(path))
				{
					await Task.Delay(1000);

					FileUtil.Delete(path);
				}
			}).ConfigureAwait(false);
		}

		public static int ToTotalDays(this DateTime date) => (date - DateTime.Parse("1990/01/01")).TotalDays.Int32();

		public static float CalculateStandardDeviation(float[] values)
		{
			if (values.Length < 2) return 1.0f;
			var mean = values.Average();
			var variance = values.Select(v => (v - mean) * (v - mean)).Average();
			return (float)Math.Sqrt(variance);
		}

		public static float GetRank(this float val, float[] arr, bool higherIsBetter)
		{
			var same = arr.Count(x => Math.Abs(x - val) < 0.01f);
			var wrse = higherIsBetter ? arr.Count(x => x < val) : arr.Count(x => x > val);

			return (wrse + same / 2.0f) / arr.Length; ;
		}

		public static float GetRank<T>(this T detail, IEnumerable<T> src, Func<T, float> func, bool higherIsBetter)
		{
			return func(detail).GetRank(src.Select(func).ToArray(), higherIsBetter);
		}

	}
}