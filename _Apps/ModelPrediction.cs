using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public abstract class ModelPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		public override string ToString()
		{
			return $"{Score}";
		}

		public virtual float GetScore()
		{
			return Score;
		}
	}

	public class BinaryClassificationPrediction : ModelPrediction
	{
		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public bool PredictedLabel { get; set; }

		public override float GetScore()
		{
			return base.GetScore() * Probability;
		}
	}

	public class MultiClassificationPrediction : ModelPrediction
	{
		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public bool PredictedLabel { get; set; }

		public override float GetScore()
		{
			return base.GetScore() * Probability;
		}
	}

	public class RegressionPrediction : ModelPrediction
	{
		public override float GetScore()
		{
			return 5 - base.GetScore();
		}
	}
}