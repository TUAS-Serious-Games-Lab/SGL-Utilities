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
	}
}
