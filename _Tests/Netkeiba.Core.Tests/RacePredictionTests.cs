using System;
using System.Collections.Generic;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class RacePredictionTests
	{
		// 発走後に開いたレースの確定オッズを、予想時のオッズとして t_odds に残さない
		[TestCase("15:40", "2026/11/15 15:30", true)]
		[TestCase("15:40", "2026/11/15 15:40", false)]
		[TestCase("9:50", "2026/11/15 9:40", true)]
		[TestCase("15:40", "2026/11/17 10:00", false)]
		[TestCase("", "2026/11/15 15:30", false)]
		public void IsBeforeStartComparesWithStartTime(string start, string now, bool expected)
		{
			var row = new Dictionary<string, string> { ["開催日"] = "2026/11/15", ["発走"] = start };

			Assert.That(RacePrediction.IsBeforeStart(row, DateTime.Parse(now)), Is.EqualTo(expected));
		}
	}
}
