using System.Collections.Generic;
using System.Linq;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class RacePredictionExtensionTests
	{
		// O(頭数²) の式が、着順の全順列を Plackett-Luce の確率で足し上げた値と一致すること。
		[Test]
		public void HarvilleMatchesPermutationSum()
		{
			var p1 = RacePredictionExtension.Softmax(new[] { 1.2f, 0.3f, -0.5f, 0.8f, 0f }, 2.0);
			var (p2, p3) = RacePredictionExtension.Harville(p1);

			var exp2 = new double[p1.Length];
			var exp3 = new double[p1.Length];
			foreach (var order in Permutations(Enumerable.Range(0, p1.Length).ToArray()))
			{
				var prob = 1d;
				var rest = 1d;
				foreach (var i in order)
				{
					prob *= p1[i] / rest;
					rest -= p1[i];
				}
				exp2[order[1]] += prob;
				exp3[order[2]] += prob;
			}

			Assert.That(p2, Is.EqualTo(exp2).Within(1e-6));
			Assert.That(p3, Is.EqualTo(exp3).Within(1e-6));
			Assert.That(new[] { p1.Sum(), p2.Sum(), p3.Sum() }, Is.All.EqualTo(1d).Within(1e-6));
		}

		[Test]
		public void BoundaryValueBelongsToUpperBand()
		{
			var bounds = new[] { 0.2, 0.3, 0.4, 0.5 };

			Assert.That(new[] { 0.1, 0.2, 0.3, 0.45, 0.5 }.Select(x => RacePredictionExtension.GetConfidenceBand(x, bounds)), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
		}

		private static IEnumerable<int[]> Permutations(int[] items) => items.Length <= 1
			? new[] { items }
			: items.SelectMany(x => Permutations(items.Where(y => y != x).ToArray()).Select(rest => rest.Prepend(x).ToArray()));
	}
}
