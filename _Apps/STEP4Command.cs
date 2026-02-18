using Microsoft.ML;
using Microsoft.ML.Data;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public class STEP4Command : STEPBase
	{
		public STEP4Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			var racebases = VM.S4Text.Split('\n')
				.Select(x => Regex.Match(x, @"\d{12}").Value.Left(10))
				.SelectMany(x => Enumerable.Range(1, 12).Select(i => $"{x}{i.ToString(2)}"))
				.OrderBy(x => x)
				.ToArray();

			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var getShutsuba = false;

				// 全ﾚｰｽの出馬表を取得する
				foreach (var raceid in racebases)
				{
					await conn.BeginTransaction();
					foreach (var racearr in await GetSTEP4Racearrs(conn, raceid).ToArrayAsync())
					{
						await conn.InsertShutsubaAsync(racearr);
						await conn.InsertOikiriAsync(raceid);
						getShutsuba = true;
					}
					conn.Commit();
				}

				var ml = new MLContext(seed: 1);
				var mo = LoadModel(ml);

				await PreviousDataSets.Initialize(conn, MainViewModel.GetS4SelectedDate().AddDays(-3));

				// 出馬表からﾚｰｽﾃﾞｰﾀを作成する
				foreach (var race in await conn.GetShutsubaRaceAsync(racebases).ToArrayAsync())
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					// 過去ﾃﾞｰﾀ設定
					details.ForEach(x => x.SetHistoricalData(PreviousDataSets.GetHorses(x), details, PreviousDataSets.GetTrackConditionDistances(x)));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					// 特徴量を生成
					var features = details.Select(x =>
					{
						var value = x.ExtractFeatures(details);

						// ラベル生成（難易度調整済み着順スコア）
						value.Label = 0;

						return value;
					}).CalculateInRaces();

					//// ｽｺｱ計算
					//var predictions = RacePrediction.CalculatePrediction(ml, mo, details, features);

					//if (race.RaceDate < DateTime.Now)
					//{
					//	var tya = await NetkeibaGetter.GetTyakujun(race.RaceId);

					//	predictions.ForEach(p =>
					//	{
					//		p.Result = tya
					//			.Where(x => x["馬番"].Int32() == p.Detail.Umaban)
					//			.Select(x => x["着順"].Int32())
					//			.FirstOrDefault();
					//	});
					//}
					//// ﾃﾞﾊﾞｯｸﾞｺﾒﾝﾄで出力
					//AddLog("---------------------------------");
					//AddLog($"[R{race.RaceId.Right(2)}] [{race.Grade}] [{race.Place}] [{race.RaceId}]: {race.CourseName}");
					//foreach (var pre in predictions.Where(x => x.Rank < 6))
					//{
					//	var name = await conn.ExecuteScalarAsync($"SELECT 馬名 FROM t_uma WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, pre.Detail.Horse));
					//	AddLog($"Umaban:{pre.Detail.Umaban:D2} Rank:{pre.Rank:D2} Result:{pre.Result:D2} Score:{pre.Score:F4} Confidence:{pre.Confidence:F4}: {name}");
					//}
				}

				if (getShutsuba)
				{
					await conn.BeginTransaction();
					await conn.DeleteOrigAsync(racebases);
					conn.Commit();
				}
			}
		}

		private async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP4Racearrs(SQLiteControl conn, string raceid)
		{
			if (!await conn.ExistsOrigAsync(raceid))
			{
				var arr = await NetkeibaGetter.GetRaceShutubas(raceid);

				if (arr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) yield break;

				yield return arr;
			}
		}

		private ITransformer LoadModel(MLContext ml)
		{
			using var stream = new FileStream(AppSetting.Instance.RankingTrains.First().Path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ml.Model.Load(stream, out var schema);
		}
	}
}