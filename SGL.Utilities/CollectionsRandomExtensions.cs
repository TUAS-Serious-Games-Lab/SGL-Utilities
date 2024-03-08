using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides extension methods for randomness-based operations on collections.
	/// </summary>
	public static class CollectionsRandomExtensions {
		/// <summary>
		/// Takes a random sample of size <paramref name="count"/> from the elements of <paramref name="source"/> without repetition.
		/// </summary>
		/// <typeparam name="T">The type of the collection elements to sample.</typeparam>
		/// <param name="source">The collection from which to draw the sample.</param>
		/// <param name="count">The number of elements in the sample.</param>
		/// <param name="rng">The random generator used as the randomness source.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> containing the <paramref name="count"/> sample elements.</returns>
		/// <exception cref="ArgumentOutOfRangeException">When <paramref name="count"/> is greater than the number of elements in <paramref name="source"/>, as then there aren't enough elements to provide the desired sample size.</exception>
		/// <remarks>
		/// Based on the algorithm discussed here: <see href="https://stackoverflow.com/questions/35065764/select-n-records-at-random-from-a-set-of-n/35065765#35065765"/>
		/// </remarks>
		public static IEnumerable<T> RandomSample<T>(this IReadOnlyCollection<T> source, int count, Random rng) {
			if (source.Count < count) {
				throw new ArgumentOutOfRangeException($"Can't draw a sample of {count} out of a collection with only {source.Count} elements.");
			}
			int N = source.Count;
			int n = count;
			foreach (var item in source) {
				if (n <= 0) break;
				if (N <= 0) break;
				if (rng.Next(N) < n) {
					yield return item;
					--n;
				}
				--N;
			}
		}
		/// <summary>
		/// Returns a randomly drawn element from <paramref name="source"/>.
		/// </summary>
		/// <typeparam name="T">The type of the collection elements.</typeparam>
		/// <param name="source">A non-empty collection to draw from.</param>
		/// <param name="rng">The random generator used as the randomness source.</param>
		/// <returns>A single randomly selected element from <paramref name="source"/>.</returns>
		/// <exception cref="ArgumentException">When <paramref name="source"/> has no elements.</exception>
		public static T RandomElement<T>(this IReadOnlyList<T> source, System.Random rng) {
			if (source.Count == 0) throw new ArgumentException("Can't draw random item from empty collection.", nameof(source));
			var index = rng.Next(source.Count);
			return source[index];
		}
		/// <summary>
		/// Returns a randomly drawn element from <paramref name="source"/>.
		/// </summary>
		/// <typeparam name="T">The type of the collection elements.</typeparam>
		/// <param name="source">A non-empty collection to draw from.</param>
		/// <param name="rng">The random generator used as the randomness source.</param>
		/// <returns>A single randomly selected element from <paramref name="source"/>.</returns>
		/// <exception cref="ArgumentException">When <paramref name="source"/> has no elements.</exception>
		public static T RandomElement<T>(this IReadOnlyCollection<T> source, System.Random rng) {
			return source.RandomSample(1, rng).First();
		}

		public static T RandomElementWeighted<T>(this IReadOnlyCollection<T> source, System.Random rng, Func<T, double> weight) =>
			source.RandomElementWeighted(rng, source.Select(weight).ToList());
		public static T RandomElementWeighted<T>(this IReadOnlyCollection<T> source, System.Random rng, Func<T, int> weight) =>
			source.RandomElementWeighted(rng, source.Select(weight).ToList());

		public static T RandomElementWeighted<T>(this IReadOnlyCollection<T> source, System.Random rng, IReadOnlyCollection<double> weights) =>
			RandomElementWeighted(source, totalWeight => rng.NextDouble() * totalWeight, weights);
		public static T RandomElementWeighted<T>(this IReadOnlyCollection<T> source, System.Random rng, IReadOnlyCollection<int> weights) =>
			RandomElementWeighted(source, rng.Next, weights);

		public static T RandomElementWeighted<T>(this IReadOnlyCollection<T> source, Func<double, double> rng, IReadOnlyCollection<double> weights) {
			if (source.Count == 0) throw new ArgumentException("Can't draw random item from empty collection.", nameof(source));
			if (weights.Count != source.Count) throw new ArgumentException("Length of weights collection needs to be the same as length of source collection.", nameof(weights));
			var totalWeight = weights.Sum();
			var selectedWeight = rng(totalWeight);
			double accumulated = 0;
			int index = 0;
			foreach (var weight in weights) {
				accumulated += weight;
				if (accumulated > selectedWeight) {
					return source.Skip(index).First();
				}
				++index;
			}
			return source.Last();
		}
		public static T RandomElementWeighted<T>(this IReadOnlyCollection<T> source, Func<int, int> rng, IReadOnlyCollection<int> weights) {
			if (source.Count == 0) throw new ArgumentException("Can't draw random item from empty collection.", nameof(source));
			if (weights.Count != source.Count) throw new ArgumentException("Length of weights collection needs to be the same as length of source collection.", nameof(weights));
			var totalWeight = weights.Sum();
			var selectedWeight = rng(totalWeight);
			int accumulated = 0;
			int index = 0;
			foreach (var weight in weights) {
				accumulated += weight;
				if (accumulated > selectedWeight) {
					return source.Skip(index).First();
				}
				++index;
			}
			return source.Last();
		}

		/// <summary>
		/// Returns the elements from <paramref name="source"/> in random order.
		/// </summary>
		/// <typeparam name="T">The type of the collection elements.</typeparam>
		/// <param name="source">An enumeration containing the elements to shuffle.</param>
		/// <param name="rng">The random generator used as the randomness source.</param>
		/// <returns>A <see cref="List{T}"/> containing the elements from <paramref name="source"/> in random order.</returns>
		public static List<T> Shuffle<T>(this IEnumerable<T> source, System.Random rng) {
			return source.Select(elem => (r: rng.Next(int.MinValue, int.MaxValue), elem: elem))
				.OrderBy(e => e.r).Select(e => e.elem).ToList();
		}
	}
}
