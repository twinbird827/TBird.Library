using Microsoft.ML;
using System;

namespace Netkeiba
{
	public static class IEstimatorExtension
	{
		public static IEstimator<ITransformer> NormalizeMeanVarianceMultiple(this IEstimator<ITransformer> pipeline, MLContext _ml, params string[] featureNames)
		{
			if (featureNames.Length == 0)
				throw new ArgumentException("At least one feature name is required");

			foreach (var feature in featureNames)
			{
				pipeline = pipeline.Append(_ml.Transforms.NormalizeMeanVariance(feature));
			}

			return pipeline;
		}

	}
}
