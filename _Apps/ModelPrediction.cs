using Microsoft.ML.Data;

namespace Netkeiba
{
	public abstract class ModelPrediction
	{
		public override string ToString()
		{
			return $"{GetScore()}";
		}

		public abstract float GetScore();
	}

	public class BinaryClassificationPrediction : ModelPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public bool PredictedLabel { get; set; }

		public override float GetScore()
		{
			return Score * Probability;
		}
	}

	public class MultiClassificationPrediction : ModelPrediction
	{
		[ColumnName("Label")]
		public uint Label { get; set; }

		[ColumnName("Score")]
		public float[] Score { get; set; } = new float[0];

		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public uint PredictedLabel { get; set; }

		public override float GetScore()
		{
			return PredictedLabel / Score[PredictedLabel - 1];
		}
	}

	public class RegressionPrediction : ModelPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		public override float GetScore()
		{
			return 5 - Score;
		}
	}
}