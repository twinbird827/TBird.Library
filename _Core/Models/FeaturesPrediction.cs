namespace Netkeiba.Models
{
	public class FeaturesPrediction
	{
		public string RaceId { get; set; } = string.Empty;

		public uint ActualRank { get; set; }

		public float Score { get; set; }
	}
}
