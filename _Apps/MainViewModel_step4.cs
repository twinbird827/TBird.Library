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

			// Load Trained Model
			ITransformer predictionPipeline = mlContext.Model.Load(@"model\model.zip", out DataViewSchema predictionPipelineSchema);

			try
			{
				// Create PredictionEngines
				var predictionEngine = mlContext.Model.CreatePredictionEngine<ModelRow, ModelRowPrediction>(predictionPipeline);

				// Input Data
				var predictions = await GetPredictions();

				// Get Prediction
				foreach (var row in predictions)
				{
					row.Prediction = predictionEngine.Predict(row);

					AddLog($"Predicted:{row.Prediction.Predicted:P3} Score:{row.Prediction.Score:P3} Probability:{row.Prediction.Probability:P3}");
				}

				var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv");

				await FileUtil.WriteAsync(path,
					"Predicted,Score,Probability,ﾚｰｽID,枠番,馬番,馬ID",
					predictions.Select(x => $"{x.Prediction.Predicted},{x.Prediction.Score},{x.Prediction.Probability},{x.Source["ﾚｰｽID"]},{x.Source["枠番"]},{x.Source["馬番"]},{x.Source["馬ID"]}")
				);
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
			}
		});

		private IEnumerable<string> GetRaceIds()
		{
			var raceids = S4Text.Split('\n').Select(x => Regex.Match(x, @"\d{12}").Value).SelectMany(x => Enumerable.Range(1, 12).Select(i => x.Left(10) + i.ToString(2)));
			return raceids;
		}

		private async Task<List<ModelRow>> GetPredictions()
		{
			using (var conn = CreateSQLiteControl())
			{
				var results = new List<Dictionary<string, double>>();
				var targets = GetRaceIds().ToArray();
				await conn.BeginTransaction();
				foreach (var raceid in targets)
				{
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

				}

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
