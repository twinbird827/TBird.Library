using Microsoft.ML.AutoML;
using Netkeiba.Models;
using System.Collections.Generic;
using System.Linq;
using TBird.Core;

namespace Netkeiba
{
	public class AppSetting : JsonBase<AppSetting>
	{
		public static AppSetting Instance { get; } = new AppSetting();

		public AppSetting(string path) : base(path)
		{
			if (!Load())
			{
				RankingTrains = [];
				NetkeibaResult = "result";

			}
		}

		public AppSetting() : this(@"lib\app-setting.json")
		{

		}

		public string NetkeibaId
		{
			get => GetProperty(_NetkeibaId);
			set => SetProperty(ref _NetkeibaId, value);
		}
		private string _NetkeibaId = string.Empty;

		public string NetkeibaPassword
		{
			get => GetProperty(_NetkeibaPassword);
			set => SetProperty(ref _NetkeibaPassword, value);
		}
		private string _NetkeibaPassword = string.Empty;

		public string NetkeibaResult
		{
			get => GetProperty(_NetkeibaResult);
			set => SetProperty(ref _NetkeibaResult, value);
		}
		private string _NetkeibaResult = string.Empty;

		public RankingTrain[] RankingTrains
		{
			get => GetProperty(_RankingTrains);
			set => SetProperty(ref _RankingTrains, value);
		}
		public RankingTrain[] _RankingTrains = [];

		public void UpdateRankingTrains(RankingTrain now)
		{
			RankingTrains = RankingTrains.Concat(Arr(now)).ToArray();
			Save();
		}

		public IEnumerable<RankingTrain> GetRankingTrains(string grade)
		{
			return RankingTrains.Where(x => x.Grade == grade);
		}

		public RankingTrain GetRankingTrain(string grade)
		{
			return GetRankingTrains(grade).Run(arr =>
			{
				return arr.FirstOrDefault(x => x.NDCG1 == arr.Max(y => y.NDCG1)) ?? RankingTrain.Default;
			});
		}

		public void RemoveAllRankingTrain()
		{
			foreach (var x in RankingTrains)
			{
				FileUtil.Delete(x.Path);
			}
			RankingTrains = [];
			Save();
		}
	}
}