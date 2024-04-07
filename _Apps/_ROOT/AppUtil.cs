using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
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
using TBird.Wpf;

namespace Netkeiba
{
	public static class AppUtil
	{
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

		public static async Task<IHtmlDocument> GetDocument(string url)
		{
			using (await Locker.LockAsync(_guid))
			{
				MainViewModel.AddLog($"req: {url}");
				var res = await WebUtil.GetStringAsync(url, _srcenc, _dstenc);

				var doc = await _parser.ParseDocumentAsync(res);

				return doc;
			}
		}

		private static string _guid = Guid.NewGuid().ToString();

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
			"G1)", "G2)", "G3)", "(G)", "(L)", "オープン", "３勝クラス", "1600万下", "２勝クラス", "1000万下", "１勝クラス", "500万下", "未勝利", "新馬"
		};

		private static readonly Dictionary<string, string> ﾗﾝｸ1 = new()
		{
			{ "G1)", "G1" },
			{ "G2)", "G2" },
			{ "G3)", "G3" },
			{ "(G)", "オープン" },
			{ "(L)", "オープン" },
			{ "オープン", "オープン" },
			{ "３勝クラス", "3勝" },
			{ "1600万下", "3勝" },
			{ "２勝クラス", "2勝" },
			{ "1000万下", "2勝" },
			{ "１勝クラス", "1勝" },
			{ "500万下", "1勝" },
			{ "未勝利", "未勝利" },
			{ "新馬", "新馬" },
			{ "", "2勝" },
		};

		public static string Getﾗﾝｸ1(string ﾚｰｽ名, string ｸﾗｽ)
		{
			return ﾗﾝｸ1[ﾗﾝｸ.FirstOrDefault(ﾚｰｽ名.Contains) ?? ﾗﾝｸ.FirstOrDefault(ｸﾗｽ.Contains) ?? string.Empty];
		}

		private static readonly Dictionary<string, string> ﾗﾝｸ2 = new()
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
			_ = WpfUtil.BackgroundAsync(async () =>
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