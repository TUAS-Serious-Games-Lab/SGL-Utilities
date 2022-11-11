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
	}
}
