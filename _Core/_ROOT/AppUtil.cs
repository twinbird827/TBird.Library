using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Netkeiba.Models;
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
		public static string Sqlitepath { get; } = PathSetting.GetPath(@"database", "database.sqlite3");

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

			using (await _lock.LockAsync())
			{
				await Task.Delay(1250);

				// TODO MainViewModel.AddLog($"req: {url}");
				MessageService.Debug($"req: {url}");

				return await context.OpenAsync(url).RunAsync(x => ((x.DocumentElement as IHtmlDocument) ?? x as IHtmlDocument).NotNull());
			}
		}

		private static int _pararell = 1;

		/// <summary>多重起動抑止ﾛｯｸ</summary>
		private static Locker _lock = Locker.Create(_pararell);

		private static IBrowsingContext? _logincontext;
		private static DateTime _loginsession = DateTime.Now.AddDays(-1);
		private static IBrowsingContext? _guestcontext;

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
			if (float.IsNaN(val)) return float.NaN;
			var validArr = arr.Where(x => !float.IsNaN(x)).ToArray();
			if (validArr.Length == 0) return float.NaN;
			var same = validArr.Count(x => Math.Abs(x - val) < 0.01f);
			var wrse = higherIsBetter ? validArr.Count(x => x < val) : validArr.Count(x => x > val);

			return (wrse + same / 2.0f) / validArr.Length;
		}

		public static float GetRank<T>(this T detail, IEnumerable<T> src, Func<T, float> func, bool higherIsBetter)
		{
			return func(detail).GetRank(src.Select(func).ToArray(), higherIsBetter);
		}

		public static float GetZScore(this float val, float[] arr)
		{
			if (float.IsNaN(val)) return float.NaN;
			var validArr = arr.Where(x => !float.IsNaN(x)).ToArray();
			if (validArr.Length < 2) return 0f;
			var mean = validArr.Average();
			var std = CalculateStandardDeviation(validArr);
			if (std < 0.0001f) return 0f;
			return (val - mean) / std;
		}

		public static float GetZScore<T>(this T detail, IEnumerable<T> src, Func<T, float> func)
		{
			return func(detail).GetZScore(src.Select(func).ToArray());
		}

	}
}