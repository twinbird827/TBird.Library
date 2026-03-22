using Codeplex.Data;
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
using Tensorflow;

namespace Netkeiba
{
	public class STEP4RoundItem : CheckboxItemModel
	{
		public STEP4RoundItem(string raceid) : base(raceid, $"R{raceid.Right(2)}")
		{
			AddOnPropertyChanged(this, (sender, e) =>
			{
				(_command = _command ?? RelayCommand.Create(ActionAsync, _ => true)).Execute(null);
			}, nameof(IsChecked), false);
		}

		private IRelayCommand? _command;

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
				MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データを取得します。");
				await conn.BeginTransaction();
				foreach (var racearr in await GetSTEP4Racearrs(conn, raceid).ToArrayAsync())
				{
					await conn.InsertShutsubaAsync(racearr);
					await conn.InsertOikiriAsync(raceid);
					getShutsuba = true;
					MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データが取得できました。");
				}
				conn.Commit();

				var ml = new MLContext(seed: 1);

				RacePrediction.Initialize(ml);

				// 出馬表からﾚｰｽﾃﾞｰﾀを作成する
				MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データをデータベースから取得します。");
				foreach (var race in await conn.GetShutsubaRaceAsync(raceid).ToArrayAsync())
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();
					var tcd = PreviousDataSets.GetTrackConditionDistances(race);

					MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データがデータベースから取得できました。");

					// 過去ﾃﾞｰﾀ設定
					details.ForEach(x => x.SetHistoricalData(PreviousDataSets.GetHorses(x), details, tcd));

					MessageService.Debug($"ﾚｰｽID：{raceid} の関連情報を取得しました。");

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					// 特徴量を生成
					var features = details.Select(x =>
					{
						var value = x.ExtractFeatures(details);

						// ラベル生成（難易度調整済み着順スコア）
						value.Label = 0;

						return value;
					});

					var inraces = features.CalculateInRaces();

					MessageService.Debug($"ﾚｰｽID：{raceid} の特徴量を作成しました。");

					// ｽｺｱ計算
					var predictions = RacePrediction.CalculatePrediction(ml, details, inraces);

					MessageService.Debug($"ﾚｰｽID：{raceid} のスコアを計算しました。");

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

					var header = $"[{race.RaceId}] [{race.Place}] [R{race.RaceId.Right(2)}] [{race.Grade}] {race.CourseName}";

					// ﾀｲﾄﾙの設定
					SetHeader(header);

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
							Total = x.Total,
							Horse = x.Horse,
							TotalMedium = x.TotalMedium,
							TotalSmall = x.TotalSmall,
							Vars2 = x.Vars2,
							Vars1 = x.Vars1,
						};
					}).WhenAll();
					SetItems(arr);

					MessageService.Debug($"ﾚｰｽID：{raceid} の処理が完了しました。");

					using (var vm = new ReportItemViewModel(header, arr))
					{
						await vm.PrintAsync();
					}

					var groups = inraces
	.SelectInParallel(x => OptimizedHorseFeatures.GetProperties()
		.SelectInParallel(p => SQLiteUtil.CreateParameter(p.GetDBType(), p.Name, p.Property.GetValue(x)))
	).ToArray();

					var groupsstr = groups.Select(arr => arr.Select(x => x.Value.Str()).GetString(",")).GetString("\r\n");

					File.WriteAllText(
						Path.Combine(Directories.DocumentsDirectory, $"{header}_{DateTime.Now.ToString("yyyyMMdd-HHmmss")}.csv"),
						Arr(
							Arr(groups.First().Select(x => x.ParameterName).GetString(",")),
							groups.Select(arr => arr.Select(x => x.Value.Str()).GetString(","))
						).SelectMany(x => x).GetString("\r\n")
					);
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
}