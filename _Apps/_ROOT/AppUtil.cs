﻿using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Web;

namespace Netkeiba
{
	public static class AppUtil
	{
		public static string GetInnerHtml(this AngleSharp.Dom.IElement x)
		{
			var innerhtml = x.GetElementsByTagName("span").Any()
				? x.GetElementsByTagName("span").First().InnerHtml
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

		private static HtmlParser _parser = new HtmlParser();
		private static Encoding _srcenc = Encoding.GetEncoding("euc-jp");
		private static Encoding _dstenc = Encoding.UTF8;

	}
}