using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Netkeiba;
using TBird.Core;

namespace Browser.Pages
{
	public class IndexModel : PageModel
	{
		private readonly ILogger<IndexModel> _logger;

		public IndexModel(ILogger<IndexModel> logger)
		{
			_logger = logger;
		}

		public async Task OnGetAsync()
		{
			// 直近の過去の開催日のレース一覧を取得
			var (date, raceIds) = await GetRecentPastRaceIds();
			RaceDate = date;

			// レースIDを開催場所ごとにグループ化
			RaceGroups = raceIds
				.Select(id => new RaceInfo
				{
					RaceId = id,
					Place = id.Substring(4, 2), // 場所コード
					RaceNum = int.Parse(id.Substring(10, 2)), // レース番号
				})
				.GroupBy(x => x.Place)
				.OrderBy(g => g.Key)
				.ToList();

			PathSetting.Instance.Save();
		}

		private async Task<(DateTime Date, IEnumerable<string> RaceIds)> GetRecentPastRaceIds()
		{
			// 昨日から遡って直近の開催日を探す
			var currentDate = DateTime.Now.AddDays(-1);

			for (int i = 0; i < 30; i++) // 最大30日遡る
			{
				var raceIds = await NetkeibaGetter.GetRaceIds(currentDate);
				if (raceIds.Any())
				{
					return (currentDate, raceIds);
				}
				currentDate = currentDate.AddDays(-1);
			}

			return (DateTime.Now, Enumerable.Empty<string>());
		}

		public IList<IGrouping<string, RaceInfo>> RaceGroups { get; set; } = new List<IGrouping<string, RaceInfo>>();
		public DateTime RaceDate { get; set; } = DateTime.Now;
	}

	public class RaceInfo
	{
		public string RaceId { get; set; } = string.Empty;
		public string Place { get; set; } = string.Empty;
		public int RaceNum { get; set; }
	}
}