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

		private void SetResult(string header, STEP4ResultItem[] items) => MainViewModel.SetS4Result(header, items);

		private async Task ActionAsync(object dummy)
		{
			if (!IsChecked) return;

			var predictions = await RacePrediction.PredictShutsubaAsync(Value);
			if (!predictions.Any()) return;

			var race = predictions[0].Detail.Race;
			var header = $"[{race.RaceId}] [{race.Place}] [R{race.RaceId.Right(2)}] [{race.Grade}] {race.CourseName}";

			// 明細の設定
			var arr = predictions.Select(x => new STEP4ResultItem(x)).ToArray();

			// EV列のRank計算
			arr.Where(x => !float.IsNaN(x.EV.Score)).OrderByDescending(x => x.EV.Score).ForEach((x, i) => x.EV.Rank = i + 1);

			SetResult(header, arr);
		}

	}
}