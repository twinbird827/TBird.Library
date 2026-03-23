using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Netkeiba
{
	public class STEP4ResultEntry
	{
		public STEP4ResultEntry(string header, STEP4ResultItem[] items)
		{
			Header = header;
			Display = CreateDisplay(header);
			Items = items;
		}

		public string Header { get; }

		public string Display { get; }

		public STEP4ResultItem[] Items { get; }

		private static string CreateDisplay(string header)
		{
			// header例: "[202505040202] [東京] [R02] [新馬] 芝1600m"
			var placeMatch = Regex.Match(header, @"\] \[(.+?)\] \[R(\d+)\]");
			if (placeMatch.Success)
			{
				var place = placeMatch.Groups[1].Value;
				var round = int.Parse(placeMatch.Groups[2].Value);
				return $"{place}{round}R";
			}
			return header;
		}

		public override string ToString() => Display;
	}
}
