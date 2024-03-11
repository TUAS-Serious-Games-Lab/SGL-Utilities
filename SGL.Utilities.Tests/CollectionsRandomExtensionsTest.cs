using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Tests {
	public class CollectionsRandomExtensionsTest {
		[InlineData(101)]
		[InlineData(102)]
		[InlineData(1001)]
		[InlineData(2002)]
		[InlineData(3003)]
		[Theory]
		public void RandomSampleReturnsExactlyTheRequestedNumberOfSamples(int popSize) {
			var rng = new Random();
			var population = Enumerable.Range(0, popSize).Select(_ => rng.Next()).ToArray();
			Assert.Empty(population.RandomSample(0, rng).ToList());
			for (int sampleSize = 1; sampleSize < 100; ++sampleSize) {
				for (int run = 0; run < 100; ++run) {
					var sample = population.RandomSample(sampleSize, rng).ToList();
					Assert.Equal(sampleSize, sample.Count);
					Assert.All(sample, s => population.Contains(s));
				}
			}
			var allSample = population.RandomSample(population.Length, rng).ToList();
			Assert.Equal(population.Length, allSample.Count);
			Assert.All(allSample, s => population.Contains(s));
		}

		[InlineData(101)]
		[InlineData(102)]
		[InlineData(1001)]
		[InlineData(2002)]
		[InlineData(3003)]
		[Theory]
		public void RandomSampleThrowsIfRequestedSampleIsLargerThanPopulation(int popSize) {
			var rng = new Random();
			var population = Enumerable.Range(0, popSize).Select(_ => rng.Next()).ToArray();
			for (int sampleSize = popSize + 1; sampleSize < popSize + 100; ++sampleSize) {
				Assert.Throws<ArgumentOutOfRangeException>(() => {
					population.RandomSample(sampleSize, rng).ToList();
				});
			}
		}

		[InlineData(new int[] { 2, 5, 1, 3 }, new int[] { 0, 0, 1, 1, 1, 1, 1, 2, 3, 3, 3 })]
		[InlineData(new int[] { 1, 1, 1, 1 }, new int[] { 0, 1, 2, 3 })]
		[InlineData(new int[] { 5, 1, 4 }, new int[] { 0, 0, 0, 0, 0, 1, 2, 2, 2, 2 })]
		[Theory]
		public void IntegralRandomElementWeightedReturnsExpectedSamples(int[] weights, int[] expectedValues) {
			Assert.Equal(expectedValues.Length, weights.Sum());
			var population = Enumerable.Range(0, weights.Length).ToList();
			for (int r = 0; r < expectedValues.Length; r++) {
				var sample = population.RandomElementWeighted((Func<int, int>)(totalWeight => {
					Assert.InRange(r, 0, totalWeight);
					return r;
				}), weights);
				Assert.Equal(expectedValues[r], sample);
			}
		}
		[InlineData(new double[] { 2, 5, 1, 3 }, new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, new int[] { 0, 0, 1, 1, 1, 1, 1, 2, 3, 3, 3 })]
		[InlineData(new double[] { 1, 1, 1, 1 }, new double[] { 0, 1, 2, 3 }, new int[] { 0, 1, 2, 3 })]
		[InlineData(new double[] { 5, 1, 4 }, new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, new int[] { 0, 0, 0, 0, 0, 1, 2, 2, 2, 2 })]
		[InlineData(new double[] { 0.5, 0.75, 0.3, 0.00001 }, new double[] { 0.0, 0.0001, 0.1, 0.499999999, 0.5, 0.6, 0.75, 1.0, 1.24, 1.25, 1.3, 1.54999 }, new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2 })]
		[InlineData(new double[] { 0.00001, 0.001, 0.01 }, new double[] { 0.0, 0.000001, 0.00001, 0.00002, 0.001, 0.002, 0.01, 0.011009999 }, new int[] { 0, 0, 1, 1, 1, 2, 2, 2 })]
		[Theory]
		public void FloatingPointRandomElementWeightedReturnsExpectedSamples(double[] weights, double[] rndValues, int[] expectedValues) {
			Assert.Equal(expectedValues.Length, rndValues.Length);
			var population = Enumerable.Range(0, weights.Length).ToList();
			for (int i = 0; i < rndValues.Length; i++) {
				var sample = population.RandomElementWeighted(totalWeight => {
					var r = rndValues[i];
					Assert.InRange(r, 0, totalWeight);
					return r;
				}, weights);
				Assert.Equal(expectedValues[i], sample);
			}
		}
	}
}
