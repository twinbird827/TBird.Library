using Jint.Parser.Ast;
using Microsoft.ML.Data;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public class STEP2Command : STEPBase
	{
		public STEP2Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var create = VM.S2Overwrite.IsChecked || !await conn.ExistsModelTableAsync();

				if (create)
				{
					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP2();

					// これまで作成した教育ﾃﾞｰﾀの削除
					AppSetting.Instance.RemoveAllRankingTrain();
				}

				// ﾃｰﾌﾞﾙ作成
				await conn.CreateModel();

				// バッチ処理で訓練データを生成・保存
				await GenerateAndSaveTrainingDataAsync(conn);
			}
		}

		/// <summary>
		/// 指定期間のレースデータから訓練データを段階的に生成・保存
		/// </summary>
		public async Task GenerateAndSaveTrainingDataAsync(SQLiteControl conn)
		{
			MessageService.Debug($"訓練データ生成開始");

			await PreviousDataSets.Initialize(conn);

			var already = conn.GetAlreadyCreatedRacesAsync().ToBlockingEnumerable().ToArray();
			var insertCount = 0;
			var batchSize = 100;

			await conn.BeginTransaction();

			foreach (var race in await conn.GetRaceAsync().ToArrayAsync())
			{
				try
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();
					var tcd = PreviousDataSets.GetTrackConditionDistances(race);

					// 過去ﾃﾞｰﾀ設定
					details.ForEach(x => x.SetHistoricalData(PreviousDataSets.GetHorses(x), details, tcd));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					if (!already.Contains(race.RaceId))
					{
						// 特徴量を生成
						var results = details.Select(x =>
						{
							var features = x.ExtractFeatures(details);

							// ラベル生成（1着=11=gain最大, 着外=0=gain最小）
							features.Label = (x.FinishPosition - 1).Run(x => 11 - Math.Min(x, 11));

							return features;
						});

						var inraces = results.CalculateInRaces();

						// ﾃﾞｰﾀﾍﾞｰｽに格納
						await conn.InsertModelAsync(inraces);
						insertCount++;

						if (insertCount % batchSize == 0)
						{
							conn.Commit();
							await conn.BeginTransaction();
						}
					}

					// 今ﾚｰｽの情報をﾒﾓﾘに格納
					details.ForEach(PreviousDataSets.AddHistory);

					MessageService.Debug($"訓練データ生成完了：{race.RaceId} {race.RaceDate}");
				}
				catch (Exception ex)
				{
					MessageService.Debug(ex.ToString());
				}
			}

			conn.Commit();

			MessageService.Debug($"訓練データ生成完了");
		}

	}
}