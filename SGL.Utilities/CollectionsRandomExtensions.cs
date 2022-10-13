using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	public static class CollectionsRandomExtensions {
		public static IEnumerable<T> RandomSample<T>(this IReadOnlyCollection<T> source, int count, Random rng) {
			if (source.Count < count) {
				throw new ArgumentOutOfRangeException($"Can't draw a sample of {count} out of a collection with only {source.Count} elements.");
			}
			// See https://stackoverflow.com/questions/35065764/select-n-records-at-random-from-a-set-of-n/35065765#35065765
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
	}
}
