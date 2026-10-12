using AngleSharp.Dom;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Netkeiba
{
	public static class IElementExtension
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
			return $"{x.GetElementsByTagName("a").Select(a => a.GetAttribute(attribute)).FirstOrDefault() ?? string.Empty}";
		}

		public static string GetHrefId(this AngleSharp.Dom.IElement x)
		{
			return x.GetHrefAttribute("href").Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
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
	}
}
