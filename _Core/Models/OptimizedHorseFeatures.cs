using Codeplex.Data;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using TBird.Core;

namespace Netkeiba.Models
{
	// [Features] 付きプロパティの宣言順が学習列・BLOB・研究（tuning.json の final.features）の列順の正本。
	// LightGBM の feature_fraction は列順で結果が変わるので、並べ替えない。
	public partial class OptimizedHorseFeatures
	{
		public OptimizedHorseFeatures()
		{
			RaceId = string.Empty;
			Horse = string.Empty;
		}

		// ｷｰ情報
		public uint Label { get; set; }
		public string RaceId { get; set; }
		public string Horse { get; set; }

		[Features(Normalization = true)]
		public float RestDays { get; set; }

		[Features]
		public float RestDaysN1Rank { get; set; }

		[Features]
		public float RestDaysN2Rank { get; set; }

		[Features]
		public float RestDaysN3Rank { get; set; }

		[Features(Normalization = true)]
		public float RestDaysN4 { get; set; }

		[Features(Normalization = true)]
		public float RestDaysN5 { get; set; }

		[Features]
		public float DaysAgoN2Rank { get; set; }

		[Features]
		public float DaysAgoN3Rank { get; set; }

		[Features]
		public float DaysAgoN4Rank { get; set; }

		[Features(Normalization = true)]
		public float DaysAgoN5 { get; set; }

		[Features(Normalization = true)]
		public float CrossRestDaysFinishPos { get; set; }

		[Features(Normalization = true)]
		public float RaceCount { get; set; }

		[Features(Normalization = true)]
		public float Age { get; set; }

		[Features]
		public float AgeRank { get; set; }

		[Features(Normalization = true)]
		public float Gender { get; set; }

		[Features]
		public float Wakuban { get; set; }

		[Features]
		public float JockeyChange { get; set; }

		[Features(Normalization = true)]
		public float JockeyElo { get; set; }

		[Features(Normalization = true)]
		public float FastJockeyElo { get; set; }

		[Features(Normalization = true)]
		public float JockeyAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float JockeyAvgRating { get; set; }

		[Features]
		public float JockeyAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float JockeyAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float JockeyAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float JockeyPlaceAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float JockeyPlaceAvgRating { get; set; }

		[Features]
		public float JockeyPlaceAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float JockeyPlaceAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float JockeyPlaceAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrackAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrackAvgRating { get; set; }

		[Features]
		public float JockeyTrackAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrackAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrackConditionAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrackConditionAvgRating { get; set; }

		[Features]
		public float JockeyTrackConditionAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrackConditionAvgTime2Top { get; set; }

		[Features]
		public float JockeyTrackConditionAvgTime2ConditionRank { get; set; }

		[Features(Normalization = true)]
		public float JockeyDistanceAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float JockeyDistanceAvgRating { get; set; }

		[Features]
		public float JockeyDistanceAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float JockeyDistanceAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float JockeyDistanceAvgTime2Condition { get; set; }

		[Features]
		public float SurfaceChange { get; set; }

		[Features(Normalization = true)]
		public float DistanceDiff { get; set; }

		[Features]
		public float DistanceDiffRank { get; set; }

		[Features(Normalization = true)]
		public float DistanceFromLast { get; set; }

		[Features]
		public float DistanceFromLastRank { get; set; }

		[Features(Normalization = true)]
		public float CrossDistanceFitFinishPos { get; set; }

		[Features]
		public float PurchasePrice { get; set; }

		[Features]
		public float JockeyWeight { get; set; }

		[Features]
		public float Weight { get; set; }

		[Features]
		public float WeightDiff { get; set; }

		[Features]
		public float WeightDiffRank { get; set; }

		[Features(Normalization = true)]
		public float AvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float MaxPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float AvgRating { get; set; }

		[Features(Normalization = true)]
		public float MaxRating { get; set; }

		[Features(Normalization = true)]
		public float GradeChange { get; set; }

		[Features]
		public float Tuka { get; set; }

		[Features]
		public float PaceAdvantage { get; set; }

		[Features]
		public float TrainerEloRank { get; set; }

		[Features(Normalization = true)]
		public float FastTrainerElo { get; set; }

		[Features(Normalization = true)]
		public float TrainerAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float TrainerAvgRating { get; set; }

		[Features]
		public float TrainerAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float TrainerAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float TrainerAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float TrainerPlaceAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float TrainerPlaceAvgRating { get; set; }

		[Features]
		public float TrainerPlaceAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float TrainerPlaceAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float TrainerPlaceAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float FastHorseElo { get; set; }

		[Features]
		public float OikiriAdjustedTime5Rank { get; set; }

		[Features]
		public float OikiriRating { get; set; }

		[Features]
		public float OikiriAdaptationRank { get; set; }

		[Features]
		public float OikiriTimeRatingRank { get; set; }

		[Features]
		public float OikiriTotalScoreZScore { get; set; }

		[Features(Normalization = true)]
		public float CrossOikiriFinishPos { get; set; }

		[Features(Normalization = true)]
		public float CrossOikiriTime2Top { get; set; }

		[Features(Normalization = true)]
		public float CrossRatingOikiri { get; set; }

		[Features(Normalization = true)]
		public float CrossOikiriJockey { get; set; }

		[Features]
		public float AvgFinishPosition3 { get; set; }

		[Features]
		public float MaxFinishPosition3 { get; set; }

		[Features]
		public float AvgFinishPosition5 { get; set; }

		[Features]
		public float MaxFinishPosition5 { get; set; }

		[Features]
		public float FinishPositionN1 { get; set; }

		[Features]
		public float FinishPositionN2Rank { get; set; }

		[Features]
		public float FinishPositionN3Rank { get; set; }

		[Features]
		public float FinishPositionN4Rank { get; set; }

		[Features]
		public float FinishPositionN5 { get; set; }

		[Features]
		public float FinishPositionTrendZScore { get; set; }

		[Features(Normalization = true)]
		public float AvgTime2Top3 { get; set; }

		[Features(Normalization = true)]
		public float MaxTime2Top3 { get; set; }

		[Features(Normalization = true)]
		public float AvgTime2Top5 { get; set; }

		[Features(Normalization = true)]
		public float MaxTime2Top5 { get; set; }

		[Features(Normalization = true)]
		public float Time2TopN1 { get; set; }

		[Features(Normalization = true)]
		public float Time2TopN2 { get; set; }

		[Features]
		public float Time2TopN3Rank { get; set; }

		[Features]
		public float Time2TopN4Rank { get; set; }

		[Features(Normalization = true)]
		public float Time2TopN5 { get; set; }

		[Features(Normalization = true)]
		public float AvgTime2Condition1 { get; set; }

		[Features]
		public float AvgTime2Condition3Rank { get; set; }

		[Features(Normalization = true)]
		public float MaxTime2Condition3 { get; set; }

		[Features]
		public float AvgTime2Condition5Rank { get; set; }

		[Features(Normalization = true)]
		public float MaxTime2Condition5 { get; set; }

		[Features]
		public float Time2ConditionN2Rank { get; set; }

		[Features]
		public float Time2ConditionN3Rank { get; set; }

		[Features]
		public float Time2ConditionN4Rank { get; set; }

		[Features(Normalization = true)]
		public float Time2ConditionN5 { get; set; }

		[Features(Normalization = true)]
		public float AvgLastThreeFurlongs2Top1 { get; set; }

		[Features(Normalization = true)]
		public float AvgLastThreeFurlongs2Top3 { get; set; }

		[Features(Normalization = true)]
		public float MaxLastThreeFurlongs2Top3 { get; set; }

		[Features(Normalization = true)]
		public float AvgLastThreeFurlongs2Top5 { get; set; }

		[Features(Normalization = true)]
		public float MaxLastThreeFurlongs2Top5 { get; set; }

		[Features(Normalization = true)]
		public float AvgLastThreeFurlongs2Condition1 { get; set; }

		[Features]
		public float AvgLastThreeFurlongs2Condition3Rank { get; set; }

		[Features(Normalization = true)]
		public float MaxLastThreeFurlongs2Condition3 { get; set; }

		[Features]
		public float AvgLastThreeFurlongs2Condition5Rank { get; set; }

		[Features(Normalization = true)]
		public float MaxLastThreeFurlongs2Condition5 { get; set; }

		[Features(Normalization = true)]
		public float SireBrosAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float SireBrosAvgRating { get; set; }

		[Features(Normalization = true)]
		public float SireBrosDistanceDiff { get; set; }

		[Features]
		public float SireBrosAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float SireBrosAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float SireBrosAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float SireBrosAvgLastThreeFurlongs2Top { get; set; }

		[Features(Normalization = true)]
		public float SireBrosAvgLastThreeFurlongs2Condition { get; set; }

		[Features]
		public float SireDamSireBrosAvgPrizeMoneyRank { get; set; }

		[Features]
		public float SireDamSireBrosAvgRatingRank { get; set; }

		[Features]
		public float SireDamSireBrosDistanceDiffRank { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireBrosAvgLastThreeFurlongs2Top { get; set; }

		[Features]
		public float SireDamSireBrosAvgFinishPositionZScore { get; set; }

		[Features]
		public float SireDamSireBrosAvgTime2TopZScore { get; set; }

		[Features]
		public float SireDamSireBrosAvgTime2ConditionZScore { get; set; }

		[Features]
		public float SireDamSireBrosAvgLastThreeFurlongs2ConditionZScore { get; set; }

		[Features(Normalization = true)]
		public float SireTrackAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float SireTrackAvgRating { get; set; }

		[Features]
		public float SireTrackAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float SireTrackAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float SireTrackAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float SireTrackConditionAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float SireTrackConditionAvgRating { get; set; }

		[Features]
		public float SireTrackConditionAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float SireTrackConditionAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float SireTrackConditionAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float SireDistanceAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float SireDistanceAvgRating { get; set; }

		[Features]
		public float SireDistanceAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float SireDistanceAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float SireDistanceAvgTime2Condition { get; set; }

		[Features]
		public float SireDamSireTrackAvgPrizeMoneyRank { get; set; }

		[Features]
		public float SireDamSireTrackAvgRatingRank { get; set; }

		[Features]
		public float SireDamSireTrackAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireTrackAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireTrackAvgTime2Condition { get; set; }

		[Features]
		public float SireDamSireTrackConditionAvgPrizeMoneyRank { get; set; }

		[Features]
		public float SireDamSireTrackConditionAvgRatingRank { get; set; }

		[Features]
		public float SireDamSireTrackConditionAvgFinishPosition { get; set; }

		[Features]
		public float SireDamSireTrackConditionAvgTime2TopRank { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireTrackConditionAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireDistanceAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireDistanceAvgRating { get; set; }

		[Features]
		public float SireDamSireDistanceAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float SireDamSireDistanceAvgTime2Top { get; set; }

		[Features]
		public float SireDamSireDistanceAvgTime2ConditionRank { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrainerAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrainerAvgRating { get; set; }

		[Features]
		public float JockeyTrainerAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrainerAvgTime2Top { get; set; }

		[Features(Normalization = true)]
		public float JockeyTrainerAvgTime2Condition { get; set; }

		[Features(Normalization = true)]
		public float BreederAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float BreederAvgRating { get; set; }

		[Features]
		public float BreederAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float BreederAvgTime2Top { get; set; }

		[Features]
		public float BreederAvgTime2ConditionRank { get; set; }

		[Features(Normalization = true)]
		public float TrainerBreederAvgPrizeMoney { get; set; }

		[Features(Normalization = true)]
		public float TrainerBreederAvgRating { get; set; }

		[Features]
		public float TrainerBreederAvgFinishPosition { get; set; }

		[Features(Normalization = true)]
		public float TrainerBreederAvgTime2Top { get; set; }

		[Features]
		public float TrainerBreederAvgTime2ConditionRank { get; set; }

		// 過去 5 走の相手（自分以外の出走馬）の FastHorseElo 平均の平均
		[Features]
		public float OppElo5 { get; set; }

		[Features]
		public float OppElo5Rank { get; set; }

		// 過去 5 走の（着順パーセンタイル − FastHorseElo 順位パーセンタイル）の平均
		[Features]
		public float Excess5 { get; set; }

		[Features]
		public float Excess5Rank { get; set; }

		// 自分以外で Tuka < 0.25 の頭数
		[Features]
		public float FrontCount { get; set; }

		[Features]
		public float TukaOrder { get; set; }

		// 単騎逃げ（Tuka が最小で 2 番手と 0.1 以上離れる）なら 1
		[Features]
		public float LoneFront { get; set; }

		// レース内 Tuka 平均 × 自分の Tuka
		[Features]
		public float TukaPace { get; set; }

		// Rank / ZScore の計算元（特徴量にしない生値。BLOB に保存し研究の再分析で Rank / ZScore を確かめる）
		public float RestDaysN1 { get; set; }

		public float RestDaysN2 { get; set; }

		public float RestDaysN3 { get; set; }

		public float DaysAgoN2 { get; set; }

		public float DaysAgoN3 { get; set; }

		public float DaysAgoN4 { get; set; }

		public float TrainerElo { get; set; }

		public float OikiriAdjustedTime5 { get; set; }

		public float OikiriAdaptation { get; set; }

		public float OikiriTimeRating { get; set; }

		public float OikiriTotalScore { get; set; }

		public float FinishPositionN2 { get; set; }

		public float FinishPositionN3 { get; set; }

		public float FinishPositionN4 { get; set; }

		public float FinishPositionTrend { get; set; }

		public float Time2TopN3 { get; set; }

		public float Time2TopN4 { get; set; }

		public float AvgTime2Condition3 { get; set; }

		public float AvgTime2Condition5 { get; set; }

		public float Time2ConditionN2 { get; set; }

		public float Time2ConditionN3 { get; set; }

		public float Time2ConditionN4 { get; set; }

		public float AvgLastThreeFurlongs2Condition3 { get; set; }

		public float AvgLastThreeFurlongs2Condition5 { get; set; }

		public float SireDamSireBrosAvgPrizeMoney { get; set; }

		public float SireDamSireBrosAvgRating { get; set; }

		public float SireDamSireBrosDistanceDiff { get; set; }

		public float SireDamSireBrosAvgFinishPosition { get; set; }

		public float SireDamSireBrosAvgTime2Top { get; set; }

		public float SireDamSireBrosAvgTime2Condition { get; set; }

		public float SireDamSireBrosAvgLastThreeFurlongs2Condition { get; set; }

		public float SireDamSireTrackAvgPrizeMoney { get; set; }

		public float SireDamSireTrackAvgRating { get; set; }

		public float SireDamSireTrackConditionAvgPrizeMoney { get; set; }

		public float SireDamSireTrackConditionAvgRating { get; set; }

		public float SireDamSireTrackConditionAvgTime2Top { get; set; }

		public float SireDamSireDistanceAvgTime2Condition { get; set; }

		public float JockeyTrackConditionAvgTime2Condition { get; set; }

		public float BreederAvgTime2Condition { get; set; }

		public float TrainerBreederAvgTime2Condition { get; set; }

	}

	public partial class OptimizedHorseFeatures
	{
		private static CustomProperty[]? _databaseProperties;

		private static CustomProperty[] GetDatabaseProperties() => _databaseProperties = _databaseProperties ?? GetProperties()
			.Where(x => !new[] { "Label", "RaceId", "Horse" }.Contains(x.Name))
			.ToArray();

		/// <summary>t_model の Features BLOB の長さ（バイト）</summary>
		public static int BlobLength => GetDatabaseProperties().Length * sizeof(float);

		public static OptimizedHorseFeatures? Deserialize(Dictionary<string, object> dic)
		{
			var instance = new OptimizedHorseFeatures()
			{
				Label = dic["Label"].GetUInt32(),
				RaceId = dic["RaceId"].Str(),
				Horse = dic["Horse"].Str()
			};

			byte[] bytes = (byte[])dic["Features"];
			float[] floats = new float[bytes.Length / sizeof(float)];
			Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);

			GetDatabaseProperties().ForEach((x, i) => x.Property.SetValue(instance, floats[i]));

			return instance;
		}

		public byte[] Serialize()
		{
			float[] floats = GetDatabaseProperties()
				.Select(x => x.Property.GetValue(this).GetSingle())
				.ToArray();
			byte[] bytes = new byte[floats.Length * sizeof(float)];
			Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

			return bytes;
		}

		private static CustomProperty[] GetProperties(Type type)
		{
			// 既定の GetProperties は static も返し、BlobLength が BLOB の列に混ざる
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Select(p => new CustomProperty(
					p,
					p.Name,
					p.PropertyType,
					p.GetCustomAttribute<FeaturesAttribute>()
				)).ToArray();
		}

		public static CustomProperty[] GetProperties() => _properties = _properties ?? GetProperties(typeof(OptimizedHorseFeatures));

		public static CustomProperty[]? _properties;

		public static string[] GetNormalizationNames()
		{
			return GetProperties()
				.Where(x => x.Attribute != null && x.Attribute.Normalization)
				.Select(x => x.Name)
				.ToArray();
		}

		/// <summary>学習列（[Features] 付きプロパティ）の名前を宣言順で返す</summary>
		public static string[] GetFeaturesNames()
		{
			return GetProperties()
				.Where(x => x.Attribute != null)
				.Select(x => x.Name)
				.ToArray();
		}

	}
}
