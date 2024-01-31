using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Core;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using TBird.DB;
using System.IO;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public string S4Text
		{
			get => _S4Text;
			set => SetProperty(ref _S4Text, value);
		}
		private string _S4Text;

		public IRelayCommand S4EXEC => RelayCommand.Create(async _ =>
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			//// Load Trained Model
			//ITransformer predictionPipeline = mlContext.Model.Load(@"model\model.zip", out DataViewSchema predictionPipelineSchema);

			//try
			//{
			//	// Create PredictionEngines
			//	var predictionEngine = mlContext.Model.CreatePredictionEngine<ModelRow, ModelRowPrediction>(predictionPipeline);

			//	// Input Data
			//	var predictions = await GetPredictions();

			//	// Get Prediction
			//	foreach (var row in predictions)
			//	{
			//		row.Prediction = predictionEngine.Predict(row);

			//		AddLog($"Predicted:{row.Prediction.Predicted:P3} Score:{row.Prediction.Score:P3} Probability:{row.Prediction.Probability:P3}");
			//	}

			//	var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv");

			//	await FileUtil.WriteAsync(path,
			//		"Predicted,Score,Probability,ﾚｰｽID,枠番,馬番,馬ID",
			//		predictions.Select(x => $"{x.Prediction.Predicted},{x.Prediction.Score},{x.Prediction.Probability},{x.Source["ﾚｰｽID"]},{x.Source["枠番"]},{x.Source["馬番"]},{x.Source["馬ID"]}")
			//	);
			//}
			//catch (Exception ex)
			//{
			//	MessageService.Exception(ex);
			//}

			var BinaryClassification1 = mlContext.Model.Load(@"model\BinaryClassification01.zip", out DataViewSchema BinaryClassification1Schema);
			var BinaryClassification2 = mlContext.Model.Load(@"model\BinaryClassification02.zip", out DataViewSchema BinaryClassification2Schema);
			var BinaryClassification3 = mlContext.Model.Load(@"model\BinaryClassification03.zip", out DataViewSchema BinaryClassification3Schema);
			var BinaryClassification4 = mlContext.Model.Load(@"model\BinaryClassification04.zip", out DataViewSchema BinaryClassification4Schema);
			var BinaryClassification5 = mlContext.Model.Load(@"model\BinaryClassification05.zip", out DataViewSchema BinaryClassification5Schema);
			var BinaryClassification6 = mlContext.Model.Load(@"model\BinaryClassification06.zip", out DataViewSchema BinaryClassification6Schema);
			var BinaryClassification7 = mlContext.Model.Load(@"model\BinaryClassification07.zip", out DataViewSchema BinaryClassification7Schema);
			var BinaryClassification8 = mlContext.Model.Load(@"model\BinaryClassification08.zip", out DataViewSchema BinaryClassification8Schema);
			var Regression = mlContext.Model.Load(@"model\Regression.zip", out DataViewSchema RegressionSchema);

			await CreatePredictionFile(
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification1),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification2),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification3),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification4),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification5),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification6),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification7),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification8),
				mlContext.Model.CreatePredictionEngine<RegressionSource, RegressionPrediction>(Regression)
			).TryCatch();
		});

		private IEnumerable<string> GetRaceIds()
		{
			var raceids = S4Text.Split('\n').Select(x => Regex.Match(x, @"\d{12}").Value).SelectMany(x => Enumerable.Range(1, 12).Select(i => x.Left(10) + i.ToString(2)));
			return raceids.OrderBy(s => s);
		}

		private async Task CreatePredictionFile(
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内1,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内2,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内3,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内4,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内5,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外3,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外5,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外7,
				PredictionEngine<RegressionSource, RegressionPrediction> 着順
			)
		{
			var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv");

			FileUtil.BeforeCreate(path);

			using (var conn = CreateSQLiteControl())
			{
				Func<string, Task<IEnumerable<string>>> get_distinct = async x => (await conn.GetRows($"SELECT DISTINCT {x} FROM t_orig ORDER BY {x}")).Select(y => $"{y[x]}");

				//var ｸﾗｽ = new List<string>(await get_distinct("ｸﾗｽ"));
				var ﾗﾝｸ1 = new List<string>(await get_distinct("ﾗﾝｸ1"));
				var ﾗﾝｸ2 = new List<string>(await get_distinct("ﾗﾝｸ2"));
				//var 回り = new List<string>(await get_distinct("回り"));
				//var 天候 = new List<string>(await get_distinct("天候"));
				//var 馬場 = new List<string>(await get_distinct("馬場"));
				//var 馬場状態 = new List<string>(await get_distinct("馬場状態"));
				var 馬性 = new List<string>(await get_distinct("馬性"));
				var 調教場所 = new List<string>(await get_distinct("調教場所"));
				var 一言 = new List<string>(await get_distinct("一言"));
				var 追切 = new List<string>(await get_distinct("追切"));

				var list = new List<string>();

				// 各列のﾍｯﾀﾞを挿入
				list.Add(new[] { "ﾗﾝｸ1", "開催場所", "ﾚｰｽID", "R", "ﾚｰｽ名", "枠番", "馬番", "馬名", "単勝" }
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内1)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内2)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内3)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内4)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内5)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外3)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外5)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外7)))
					.Concat(RegressionPrediction.GetHeaders(nameof(着順)))
					.Concat(new[] { "ｽｺｱ", "予想順" })
					.GetString(",")
				);

				foreach (var raceid in GetRaceIds().ToArray())
				{

					await conn.BeginTransaction();

					// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

					var arr = new List<List<object>>();

					// ﾓﾃﾞﾙﾃﾞｰﾀ作成
					foreach (var m in await CreateRaceModel(conn, raceid, ﾗﾝｸ1, ﾗﾝｸ2, 馬性, 調教場所, 一言, 追切))
					{
						// 不要なﾃﾞｰﾀ
						var drops = new[] { "着順", "単勝", "人気" };

						var tmp = new List<object>();
						var src = racearr.First(x => x["馬ID"].GetDouble() == m["馬ID"]);
						var binaryClassificationSource = new BinaryClassificationSource()
						{
							Features = m.Keys.Where(x => !drops.Contains(x)).Select(x => (float)m[x]).ToArray()
						};
						var regressionSource = new RegressionSource()
						{
							Features = m.Keys.Where(x => !drops.Contains(x)).Select(x => (float)m[x]).ToArray()
						};

						tmp.Add(src["ﾗﾝｸ1"]);
						tmp.Add(src["開催場所"]);
						tmp.Add(src["ﾚｰｽID"]);
						tmp.Add(src["ﾚｰｽID"].Right(2));
						tmp.Add(src["ﾚｰｽ名"]);
						tmp.Add(m["枠番"]);
						tmp.Add(m["馬番"]);
						tmp.Add(src["馬名"]);
						tmp.Add(src["単勝"]);
						tmp.AddRange(以内1.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(以内2.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(以内3.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(以内4.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(以内5.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(着外3.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(着外5.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(着外7.Predict(binaryClassificationSource).GetResults());
						tmp.AddRange(着順.Predict(regressionSource).GetResults());
						tmp.Add(
							new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.Sum(x => x.GetDouble()) -
							new[] { tmp[14], tmp[15], tmp[16] }.Sum(x => x.GetDouble()) +
							(1 / tmp[17].GetDouble() * 10)
						);

						arr.Add(tmp);
					}

					var scoreindex = arr.First().Count - 1;
					var scores = arr.Select(x => x[scoreindex].GetDouble()).OrderByDescending(x => x).ToArray();
					arr.ForEach(x => x.Add(scores.IndexOf(x[scoreindex].GetDouble()) + 1));

					list.AddRange(arr.Select(x => x.GetString(",")));

					// 挿入したﾃﾞｰﾀは確定情報じゃないのでﾛｰﾙﾊﾞｯｸする
					conn.Rollback();
				}

				// ﾌｧｲﾙ書き込み
				await File.AppendAllLinesAsync(path, list, Encoding.GetEncoding("Shift_JIS")); ;
			}

		}

		private async Task<List<ModelRow>> GetPredictions()
		{
			using (var conn = CreateSQLiteControl())
			{
				Func<string, Task<IEnumerable<string>>> get_distinct = async x => (await conn.GetRows($"SELECT DISTINCT {x} FROM t_orig ORDER BY {x}")).Select(y => $"{y[x]}");

				//var ｸﾗｽ = new List<string>(await get_distinct("ｸﾗｽ"));
				var ﾗﾝｸ1 = new List<string>(await get_distinct("ﾗﾝｸ1"));
				var ﾗﾝｸ2 = new List<string>(await get_distinct("ﾗﾝｸ2"));
				//var 回り = new List<string>(await get_distinct("回り"));
				//var 天候 = new List<string>(await get_distinct("天候"));
				//var 馬場 = new List<string>(await get_distinct("馬場"));
				//var 馬場状態 = new List<string>(await get_distinct("馬場状態"));
				var 馬性 = new List<string>(await get_distinct("馬性"));
				var 調教場所 = new List<string>(await get_distinct("調教場所"));
				var 一言 = new List<string>(await get_distinct("一言"));
				var 追切 = new List<string>(await get_distinct("追切"));

				foreach (var raceid in GetRaceIds().ToArray())
				{
					await conn.BeginTransaction();

					// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

					// ﾓﾃﾞﾙﾃﾞｰﾀ作成
					var aaa = await CreateRaceModel(conn, raceid, ﾗﾝｸ1, ﾗﾝｸ2, 馬性, 調教場所, 一言, 追切);

					// 挿入したﾃﾞｰﾀは確定情報じゃないのでﾛｰﾙﾊﾞｯｸする
					conn.Rollback();

				}
				var results = new List<Dictionary<string, double>>();
				var targets = GetRaceIds().ToArray();
				foreach (var raceid in targets)
				{
					// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

				}

				foreach (var raceid in targets)
				{
					results.AddRange(await CreateRaceModel(conn, raceid, ﾗﾝｸ1, ﾗﾝｸ2, 馬性, 調教場所, 一言, 追切));
				}
				conn.Rollback();

				// 不要なﾃﾞｰﾀ
				var drops = new[] { "着順", "単勝", "人気" };

				var models = new List<ModelRow>(results.Select(dic =>
				{
					var m = new ModelRow();

					m.Source = dic;
					m.Features = dic.Keys.Where(x => !drops.Contains(x)).Select(x => (float)dic[x]).ToArray();

					return m;
				}));

				return models;
			}
		}

	}
}