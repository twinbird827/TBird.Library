using AngleSharp.Html.Parser;
using Netkeiba;
using NUnit.Framework;
using System;

namespace Netkeiba.Core.Tests
{
	public class RaceScheduleTests
	{
		// race_list_sub.html の形式。時刻は同じレースの li の中にあり、末尾に空白が付く。
		private const string RaceList =
			"<ul>" +
			"<li class=\"RaceList_DataItem \"><a href=\"../race/shutuba.html?race_id=202605040701&rf=race_list\">" +
			"<span class=\"RaceList_Itemtime\">09:45 </span></a></li>" +
			"<li class=\"RaceList_DataItem bg_jyoken\"><a href=\"../race/shutuba.html?race_id=202605040702&rf=race_list\"></a></li>" +
			"</ul>";

		[Test]
		public void LiWithoutTimeIsSkipped()
		{
			var doc = new HtmlParser().ParseDocument(RaceList);

			var arr = NetkeibaGetter.ParseRaceSchedules(doc, new DateTime(2026, 10, 18));

			Assert.That(arr, Has.Length.EqualTo(1));
			Assert.That(arr[0].RaceId, Is.EqualTo("202605040701"));
			Assert.That(arr[0].StartTime, Is.EqualTo(new DateTime(2026, 10, 18, 9, 45, 0)));
		}
	}
}
