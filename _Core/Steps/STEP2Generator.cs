using Netkeiba.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public static class STEP2Generator
	{
		/// <returns>例外で生成できなかったレースの件数</returns>
		public static async Task<int> GenerateAsync(bool overwrite)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var create = overwrite || !await conn.ExistsModelTableAsync();

				if (create)
				{
					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP2();

					AppSetting.Instance.RemoveAllRankingTrain();
				}

				await conn.CreateModel();

				return await GenerateAndSaveTrainingDataAsync(conn);
			}
		}

		/// <summary>
		/// 指定期間のレースデータから訓練データを段階的に生成・保存
		/// </summary>
		private static async Task<int> GenerateAndSaveTrainingDataAsync(SQLiteControl conn)
		{
			MessageService.Debug($"訓練データ生成開始");

			await PreviousDataSets.Initialize(conn);

			var already = conn.GetAlreadyCreatedRacesAsync().ToBlockingEnumerable().ToArray();
			var insertCount = 0;
			var failedCount = 0;
			var batchSize = 100;

			await conn.BeginTransaction();

			foreach (var race in await conn.GetRaceAsync().ToArrayAsync())
			{
				try
				{
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();
					var tcd = PreviousDataSets.GetTrackConditionDistances(race);

					details.ForEach(x => x.SetHistoricalData(PreviousDataSets.GetHorses(x), details, tcd));

					if (!already.Contains(race.RaceId))
					{
						var results = details.Select(x =>
						{
							var features = x.ExtractFeatures(details);

							// ラベル生成（1着=11=gain最大, 着外=0=gain最小）
							features.Label = (x.FinishPosition - 1).Run(x => 11 - Math.Min(x, 11));

							return features;
						});

						var inraces = results.CalculateInRaces();

						await conn.InsertModelAsync(inraces);
						insertCount++;

						if (insertCount % batchSize == 0)
						{
							conn.Commit();
							await conn.BeginTransaction();
						}
					}

					details.ForEach(PreviousDataSets.AddHistory);
					PreviousDataSets.UpdateElo(details);

					MessageService.Debug($"訓練データ生成完了：{race.RaceId} {race.RaceDate}");
				}
				catch (Exception ex)
				{
					MessageService.Debug(ex.ToString());
					failedCount++;
				}
			}

			conn.Commit();

			MessageService.Debug($"訓練データ生成完了");
			return failedCount;
		}
	}
}
