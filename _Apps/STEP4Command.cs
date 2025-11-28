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
		private PreviousDataSets _PDS = new();

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

				// 出馬表からﾚｰｽﾃﾞｰﾀを作成する
				foreach (var race in await conn.GetShutsubaRaceAsync(racebases).ToArrayAsync())
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					// 関連情報を取得する
					foreach (var x in details)
					{
						await _PDS.AddConnection(conn, x);
					}

					// 過去ﾃﾞｰﾀ設定
					details.ForEach(x => x.SetHistoricalData(_PDS.GetHorses(x)));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					// 特徴量を生成
					var features = details.Select(x =>
					{
						var value = x.ExtractFeatures(_PDS, details);

						// ラベル生成（難易度調整済み着順スコア）
						value.Label = 0;

						return value;
					}).ToArray().CalculateInRaces(race).ToArray();

					// ｽｺｱ計算
					var predictions = RacePrediction.CalculatePrediction(ml, mo, details, features);

					if (race.RaceDate < DateTime.Now)
					{
						var tya = await NetkeibaGetter.GetTyakujun(race.RaceId);

						predictions.ForEach(p =>
						{
							p.Result = tya
								.Where(x => x["馬番"].Int32() == p.Detail.Umaban)
								.Select(x => x["着順"].Int32())
								.FirstOrDefault();
						});
					}
					// ﾃﾞﾊﾞｯｸﾞｺﾒﾝﾄで出力
					AddLog("---------------------------------");
					AddLog($"[R{race.RaceId.Right(2)}] [{race.Grade}] [{race.Place}] [{race.RaceId}]: {race.CourseName}");
					foreach (var pre in predictions.Where(x => x.Rank < 6))
					{
						var name = await conn.ExecuteScalarAsync($"SELECT 馬名 FROM t_uma WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, pre.Detail.Horse));
						AddLog($"Umaban:{pre.Detail.Umaban:D2} Rank:{pre.Rank:D2} Result:{pre.Result:D2} Score:{pre.Score:F4} Confidence:{pre.Confidence:F4}: {name}");
					}
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

	public static partial class SQLite3Extensions
	{
		public static async IAsyncEnumerable<Race> GetShutsubaRaceAsync(this SQLiteControl conn, IEnumerable<string> raceids)
		{
			var sql = $@"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  h.ﾚｰｽID IN ({raceids.Select(_ => "?").GetString(",")})
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql, raceids.Select(x => SQLiteUtil.CreateParameter(DbType.String, x)).ToArray()))
			{
				yield return new Race(x);
			}
		}

		//		public static async Task<List<RaceDetail>> GetShutsubaRaceDetailAsync(this SQLiteControl conn, DateTime date, params (string Key, string Value)[] kvp)
		//		{
		//			var sql = $@"
		//SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数, d.馬番, d.馬ID, d.騎手ID, d.調教師ID, u.父ID, u.母父ID, u.生産者ID, d.着順, d.ﾀｲﾑ変換, d.賞金, u.評価額, u.生年月日, d.斤量, d.通過, d.上り, d.馬性, d.ﾀｲﾑ指数, d.着差, o.コース, o.馬場, o.乗り役, CAST(o.時間1 AS REAL) 時間1, CAST(o.時間2 AS REAL) 時間2, CAST(o.時間3 AS REAL) 時間3, CAST(o.時間4 AS REAL) 時間4, CAST(o.時間5 AS REAL) 時間5, o.時間評価1, o.時間評価2, o.時間評価3, o.時間評価4, o.時間評価5, o.脚色, o.一言, o.評価, CAST(d.体重 AS REAL) 体重, CAST(d.増減 AS REAL) 増減
		//FROM   t_orig_h h, v_orig_d d, t_uma u, t_oikiri o
		//WHERE  h.ﾚｰｽID = d.ﾚｰｽID AND d.馬ID = u.馬ID AND h.開催日 < ? AND {kvp.Select(x => $"{x.Key} = ?").GetString(" AND ")} AND d.ﾚｰｽID = o.ﾚｰｽID AND d.馬ID = o.馬ID
		//ORDER BY h.開催日 ASC, h.ﾚｰｽID ASC
		//";
		//			var parameters = new[]
		//			{
		//				SQLiteUtil.CreateParameter(DbType.String, date.ToString("yyyy/MM/dd")),
		//			}.Concat(
		//				kvp.Select(x => SQLiteUtil.CreateParameter(DbType.String, x.Value))
		//			).ToArray();

		//			var results = new List<RaceDetail>();
		//			foreach (var row in await conn.GetRows(sql, parameters).RunAsync(arr => arr.Select(x => new RaceDetail(x, new Race(x))).ToList()))
		//			{
		//				row.Initialize(results);
		//				results.Insert(0, row);
		//			}
		//			return results;
		//		}

		public static async Task DeleteOrigAsync(this SQLiteControl conn, string[] raceids)
		{
			var parameters = raceids.Select(x => SQLiteUtil.CreateParameter(DbType.String, x)).ToArray();

			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_d WHERE ﾚｰｽID IN ({raceids.Select(x => "?").GetString(",")})", parameters);
			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_h WHERE ﾚｰｽID IN ({raceids.Select(x => "?").GetString(",")})", parameters);
		}
	}
}