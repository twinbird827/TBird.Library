using AngleSharp.Html.Parser;
using Netkeiba;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class IElementExtensionTests
	{
		// 202609030501 のレース結果ページの形式。h1 と span の中身の前後に改行・タブが入る。
		private const string RaceHeader =
			"<dd>\n" +
			"\t\t<h1>\n" +
			"\t\t    障害3歳以上OP\n" +
			"\t\t</h1>\n" +
			"\t\t<p>\n" +
			"\t\t    <span>\n" +
			"\t\t\t障芝 ダート3110m&nbsp;/&nbsp;\n" +
			"\t\t\t天候 : 小雨&nbsp;/&nbsp;\n" +
			"\t\t\t芝 : 稍重&nbsp;&nbsp;/&nbsp;\n" +
			"\t\t\t発走 : 09:55\n" +
			"\t\t    </span><br />\n" +
			"\t\t</p>\n" +
			"</dd>";

		[Test]
		public void H1IsTrimmed()
		{
			var doc = new HtmlParser().ParseDocument(RaceHeader);

			Assert.That(doc.QuerySelector("h1")!.GetInnerHtml(), Is.EqualTo("障害3歳以上OP"));
		}

		// GetRaceResults は先頭 1 文字で回りを決める。
		[Test]
		public void SpanStartsWithCourseType()
		{
			var doc = new HtmlParser().ParseDocument(RaceHeader);

			Assert.That(doc.QuerySelector("span")!.GetInnerHtml(), Does.StartWith("障芝"));
		}

		// STEP1 は string.IsNullOrEmpty(ﾀｲﾑ指数) で未確定のレースを飛ばす。td は表の外ではパーサに捨てられる。
		[Test]
		public void WhitespaceOnlyCellIsEmpty()
		{
			var doc = new HtmlParser().ParseDocument("<table><tr><td>\n\t\t \n\t\t</td></tr></table>");

			Assert.That(doc.QuerySelector("td")!.GetInnerHtml(), Is.Empty);
		}
	}
}
