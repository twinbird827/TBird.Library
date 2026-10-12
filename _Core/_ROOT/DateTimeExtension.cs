using System;
using TBird.Core;

namespace Netkeiba
{
	public static class DateTimeExtension
	{
		public static int ToTotalDays(this DateTime date) => (date - DateTime.Parse("1990/01/01")).TotalDays.Int32();
	}
}
