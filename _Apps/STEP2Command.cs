using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
		private Dictionary<string, List<RaceDetail>> _Horses = new();
		private Dictionary<string, List<RaceDetail>> _Jockeys = new();
		private Dictionary<string, List<RaceDetail>> _Trainers = new();
		private Dictionary<string, List<RaceDetail>> _Sires = new();
		private Dictionary<string, List<RaceDetail>> _DamSires = new();
		private Dictionary<string, List<RaceDetail>> _SireDamSires = new();
		private Dictionary<string, List<RaceDetail>> _Breeders = new();

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
				}

				// ﾃｰﾌﾞﾙ作成
				await conn.CreateModel();

				// バッチ処理で訓練データを生成・保存
				await GenerateAndSaveTrainingDataAsync(conn);

				_Horses.Clear();
				_Jockeys.Clear();
				_Trainers.Clear();
				_Sires.Clear();
				_DamSires.Clear();
				_SireDamSires.Clear();
				_Breeders.Clear();
			}
		}

		/// <summary>
		/// 指定期間のレースデータから訓練データを段階的に生成・保存
		/// </summary>
		public async Task GenerateAndSaveTrainingDataAsync(SQLiteControl conn)
		{
			MainViewModel.AddLog($"訓練データ生成開始");

			var already = conn.GetAlreadyCreatedRacesAsync().ToBlockingEnumerable().ToArray();

			await foreach (var race in conn.GetRaceAsync())
			{
				try
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					// 過去ﾚｰｽの結果をｾｯﾄする
					details.ForEach(x => x.Initialize(_Horses.Get(x.Horse, new List<RaceDetail>())));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					if (!already.Contains(race.RaceId))
					{
						// 特徴量を生成
						var results = details.Select(x =>
						{
							var features = x.ExtractFeatures(
								_Horses.Get(x.Horse, new List<RaceDetail>()),
								details,
								_Jockeys.Get(x.Jockey, new List<RaceDetail>()),
								_Trainers.Get(x.Trainer, new List<RaceDetail>()),
								_Breeders.Get(x.Breeder, new List<RaceDetail>()),
								_Sires.Get(x.Sire, new List<RaceDetail>()),
								_DamSires.Get(x.DamSire, new List<RaceDetail>()),
								_SireDamSires.Get(x.SireDamSire, new List<RaceDetail>())
							);

							// ラベル生成（難易度調整済み着順スコア）
							features.Label = x.CalculateAdjustedInverseScore();

							return features;
						});

						// ﾃﾞｰﾀﾍﾞｰｽに格納
						await conn.InsertModelAsync(results);
					}

					// 今ﾚｰｽの情報をﾒﾓﾘに格納
					details.ForEach(x =>
					{
						void AddHistory(Dictionary<string, List<RaceDetail>> dic, RaceDetail tgt, string key)
						{
							if (!dic.ContainsKey(key))
							{
								dic.Add(key, new List<RaceDetail>());
							}
							dic[key].Insert(0, tgt);
						}

						AddHistory(_Horses, x, x.Horse);
						AddHistory(_Jockeys, x, x.Jockey);
						AddHistory(_Trainers, x, x.Trainer);
						AddHistory(_Sires, x, x.Sire);
						AddHistory(_DamSires, x, x.DamSire);
						AddHistory(_SireDamSires, x, x.SireDamSire);
						AddHistory(_Breeders, x, x.Breeder);
					});

					MainViewModel.AddLog($"訓練データ生成完了：{race.RaceId} {race.RaceDate}");
				}
				catch (Exception ex)
				{
					MessageService.Debug(ex.ToString());
				}
			}

			MainViewModel.AddLog($"訓練データ生成完了");
		}
	}

	public static partial class SQLite3Extensions
	{
		/// <summary>
		/// 教育ﾃﾞｰﾀ作成用ﾃｰﾌﾞﾙを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task DropSTEP2(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
		}

		/// <summary>馬ﾍｯﾀﾞ</summary>
		private static readonly string[] col_model = Arr("ﾚｰｽID", "馬ID", "Features");

		public static async Task CreateModel(this SQLiteControl conn)
		{
			await conn.Create(
				"t_model",
				col_model,
				Arr("ﾚｰｽID", "馬ID")
			);

			// TODO indexの作成
		}

		public static async Task<bool> ExistsModelTableAsync(this SQLiteControl conn)
		{
			return await conn.ExistsColumn("t_model", "ﾚｰｽID");
		}

		public static async Task InsertModelAsync(this SQLiteControl conn, IEnumerable<OptimizedHorseFeaturesModel> data)
		{
			if (!data.Any()) return;

			foreach (var chunk in data.GroupBy(x => x.RaceId))
			{
				await conn.BeginTransaction();
				foreach (var x in chunk)
				{
					var parameters = new[]
					{
						SQLiteUtil.CreateParameter(DbType.String, x.RaceId),
						SQLiteUtil.CreateParameter(DbType.String, x.HorseName),
						SQLiteUtil.CreateParameter(DbType.Object, x.Serialize())
					};
					await conn.ExecuteNonQueryAsync("REPLACE INTO t_model (ﾚｰｽID, 馬ID, Features) VALUES (?, ?, ?)", parameters);
				}
				conn.Commit();
			}
		}

		public static async Task<bool> ExistsModelAsync(this SQLiteControl conn, string raceid)
		{
			var cnt = await conn.ExecuteScalarAsync(
				"SELECT COUNT(*) FROM t_orig_h WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			).RunAsync(x => x.GetInt32());
			return 0 < cnt;
		}

		public static async IAsyncEnumerable<Race> GetRaceAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new Race(x);
			}
		}

		public static async IAsyncEnumerable<RaceDetail> GetRaceDetailsAsync(this SQLiteControl conn, Race race)
		{
			var sql = @"
SELECT d.ﾚｰｽID, d.馬ID, d.騎手ID, d.調教師ID, u.父ID, u.母父ID, u.生産者ID, d.着順, d.ﾀｲﾑ変換, d.賞金, u.購入額, u.生年月日
FROM v_orig_d d, v_uma u
WHERE d.ﾚｰｽID = ? AND d.馬ID = u.馬ID
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, race.RaceId),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new RaceDetail(x, race);
			}
		}

		public static async IAsyncEnumerable<string> GetAlreadyCreatedRacesAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT DISTINCT ﾚｰｽID
FROM   t_model h
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["ﾚｰｽID"].Str();
			}
		}

	}

}