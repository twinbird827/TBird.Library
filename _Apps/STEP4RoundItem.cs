using Microsoft.ML;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;

namespace Netkeiba
{
	public class STEP4RoundItem : CheckboxItemModel
	{
		private static Dictionary<string, List<RaceDetail>> _Horses = new();
		private static Dictionary<string, List<RaceDetail>> _Jockeys = new();
		private static Dictionary<string, List<RaceDetail>> _Trainers = new();
		private static Dictionary<string, List<RaceDetail>> _Sires = new();
		private static Dictionary<string, List<RaceDetail>> _DamSires = new();
		private static Dictionary<string, List<RaceDetail>> _SireDamSires = new();
		private static Dictionary<string, List<RaceDetail>> _Breeders = new();
		private static Dictionary<string, List<RaceDetail>> _JockeyTrainers = new();

		public STEP4RoundItem(string raceid) : base(raceid, $"R{raceid.Right(2)}")
		{
			AddOnPropertyChanged(this, (sender, e) =>
			{
				(_command = _command ?? RelayCommand.Create(ActionAsync, _ => true)).Execute(null);
			}, nameof(IsChecked), false);
		}

		private IRelayCommand? _command;

		private void AddLog(string message) => MainViewModel.AddLog(message);

		private void SetHeader(string header) => MainViewModel.SetS4ResultHeader(header);

		private void SetItems(IEnumerable<STEP4ResultItem> items) => MainViewModel.SetS4ResultItems(items);

		private async Task ActionAsync(object dummy)
		{
			if (!IsChecked) return;

			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var getShutsuba = false;
				var raceid = Value;

				// 該当ﾚｰｽの出馬表を取得する
				AddLog($"ﾚｰｽID：{raceid} の出馬表データを取得します。");
				await conn.BeginTransaction();
				await foreach (var racearr in GetSTEP4Racearrs(conn, raceid))
				{
					await conn.InsertShutsubaAsync(racearr);
					await conn.InsertOikiriAsync(await NetkeibaGetter.GetOikiris(raceid));
					getShutsuba = true;
					AddLog($"ﾚｰｽID：{raceid} の出馬表データが取得できました。");
				}
				conn.Commit();

				var ml = new MLContext(seed: 1);
				var mo = LoadModel(ml);

				// 出馬表からﾚｰｽﾃﾞｰﾀを作成する
				AddLog($"ﾚｰｽID：{raceid} の出馬表データをデータベースから取得します。");
				await foreach (var race in conn.GetShutsubaRaceAsync(raceid))
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					AddLog($"ﾚｰｽID：{raceid} の出馬表データがデータベースから取得できました。");

					// 関連情報を取得する
					foreach (var x in details)
					{
						async Task SetConnection(Dictionary<string, List<RaceDetail>> dic, string key, params (string Key, string Value)[] kvp)
						{
							if (!dic.ContainsKey(key)) dic[key] = await conn.GetShutsubaRaceDetailAsync(x.Race.RaceDate, kvp);
						}

						var tasks = new[]
						{
							SetConnection(_Horses, x.Horse, ("d.馬ID", x.Horse)),
							SetConnection(_Jockeys, x.Jockey, ("d.騎手ID", x.Jockey)),
							SetConnection(_Trainers, x.Trainer, ("d.調教師ID", x.Trainer)),
							SetConnection(_Breeders, x.Breeder, ("u.生産者ID", x.Breeder)),
							SetConnection(_Sires, x.Sire, ("u.父ID", x.Sire)),
							SetConnection(_DamSires, x.DamSire, ("u.母父ID", x.DamSire)),
							SetConnection(_SireDamSires, x.SireDamSire, ("u.父ID", x.Sire), ("u.母父ID", x.DamSire)),
							SetConnection(_JockeyTrainers, x.JockeyTrainer, ("d.騎手ID", x.Jockey), ("d.調教師ID", x.Trainer)),
						};

						await tasks.WhenAll();
					}

					AddLog($"ﾚｰｽID：{raceid} の関連情報を取得しました。");

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					// 特徴量を生成
					var features = details.Select(x =>
					{
						var value = x.ExtractFeatures(
							_Horses.Get(x.Horse, new List<RaceDetail>()),
							details,
							_Jockeys.Get(x.Jockey, new List<RaceDetail>()),
							_Trainers.Get(x.Trainer, new List<RaceDetail>()),
							_Breeders.Get(x.Breeder, new List<RaceDetail>()),
							_Sires.Get(x.Sire, new List<RaceDetail>()),
							_DamSires.Get(x.DamSire, new List<RaceDetail>()),
							_SireDamSires.Get(x.SireDamSire, new List<RaceDetail>()),
							_JockeyTrainers.Get(x.JockeyTrainer, new List<RaceDetail>())
						);

						// ラベル生成（難易度調整済み着順スコア）
						value.Label = 0;

						return value;
					}).ToArray().CalculateInRaces(race).ToArray();

					AddLog($"ﾚｰｽID：{raceid} の特徴量を作成しました。");

					// ｽｺｱ計算
					var predictions = RacePrediction.CalculatePrediction(ml, mo, details, features);

					AddLog($"ﾚｰｽID：{raceid} のスコアを計算しました。");

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

					// ﾀｲﾄﾙの設定
					SetHeader($"[R{race.RaceId.Right(2)}] [{race.Grade}] [{race.Place}] [{race.RaceId}]: {race.CourseName}");

					// 明細の設定
					var arr = await predictions.Select(async x =>
					{
						var name = await conn.ExecuteScalarAsync($"SELECT 馬名 FROM t_uma WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, x.Detail.Horse));

						return new STEP4ResultItem()
						{
							Wakuban = x.Detail.Wakuban,
							Umaban = x.Detail.Umaban,
							Name = name.Str(),
							Result = x.Result.Str(),
							Rank = x.Rank,
							Score = x.Score,
							Confidence = x.Confidence
						};
					}).WhenAll();
					SetItems(arr);

					AddLog($"ﾚｰｽID：{raceid} の処理が完了しました。");

				}

				if (getShutsuba)
				{
					await conn.BeginTransaction();
					await conn.DeleteOrigAsync(raceid);
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
		public static async IAsyncEnumerable<Race> GetShutsubaRaceAsync(this SQLiteControl conn, string raceid)
		{
			var sql = $@"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  h.ﾚｰｽID = ?
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql, SQLiteUtil.CreateParameter(DbType.String, raceid)))
			{
				yield return new Race(x);
			}
		}

		public static async Task<List<RaceDetail>> GetShutsubaRaceDetailAsync(this SQLiteControl conn, DateTime date, params (string Key, string Value)[] kvp)
		{
			var sql = $@"{GetRaceDetailSql()}
AND h.開催日 < ? AND {kvp.Select(x => $"{x.Key} = ?").GetString(" AND ")}
ORDER BY h.開催日 DESC, h.ﾚｰｽID ASC
LIMIT  1000
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, date.ToString("yyyy/MM/dd")),
			}.Concat(
				kvp.Select(x => SQLiteUtil.CreateParameter(DbType.String, x.Value))
			).ToArray();

			var results = new List<RaceDetail>();
			foreach (var row in await conn.GetRows(sql, parameters).RunAsync(arr => arr.Select(x => new RaceDetail(x, new Race(x))).ToList()))
			{
				results.Add(row);
			}
			return results;
		}

		public static async Task DeleteOrigAsync(this SQLiteControl conn, string raceid)
		{
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			};

			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_d WHERE ﾚｰｽID = ?", parameters);
			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_h WHERE ﾚｰｽID = ?", parameters);
		}
	}

}