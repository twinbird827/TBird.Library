﻿using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using TBird.Web;
using TBird.DB;
using System.Windows.Forms;
using TBird.Wpf;

namespace Netkeiba
{
	public static class AppUtil
	{
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
			return $"{x.GetElementsByTagName("a").Select(a => a.GetAttribute(attribute)).First()}";
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
			var res = await WebUtil.GetStringAsync(url, _srcenc, _dstenc);

			var doc = await _parser.ParseDocumentAsync(res);

			return doc;
		}

		public static async Task<IHtmlDocument> GetDocument(bool overwrite, string path, string url)
		{
			if (overwrite || !File.Exists(path)) FileUtil.BeforeCreate(path);

			if (File.Exists(path))
			{
				return await _parser.ParseDocumentAsync(File.ReadAllText(path));
			}
			else
			{
				var res = await WebUtil.GetStringAsync(url, _srcenc, _dstenc);

				await File.WriteAllTextAsync(path, res);

				var doc = await _parser.ParseDocumentAsync(res);

				return doc;
			}
		}

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
			return GetDistinct(conn, "ﾗﾝｸ2");
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

		public static void DeleteEndress(string path)
		{
			_ = WpfUtil.ExecuteOnBackground(async () =>
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