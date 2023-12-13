using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Linq;

namespace SGL.Utilities.EntityFrameworkCore {
	/// <summary>
	/// Implements a recursive <see cref="ValueComparer{T}"/> for arrays of element type <typeparamref name="T"/>.
	/// </summary>
	public class ArrayValueComparer<T> : ValueComparer<T[]> {
		/// <summary>
		/// Constructs an <see cref="ArrayValueComparer{T}"/> that uses the given comparer for the elements.
		/// </summary>
		/// <param name="elementValueComparer">The <see cref="ValueComparer{T}"/> to use on the elements.</param>
		public ArrayValueComparer(ValueComparer elementValueComparer) :
			base(
				(a, b) => checkEqual(elementValueComparer, a, b),
				(e) => computeHashCode(elementValueComparer, e),
				(e) => makeSnapshot(elementValueComparer, e)) { }

		private static bool checkEqual(ValueComparer elementValueComparer, T[]? a, T[]? b) {
			if (a == null || b == null) return a == b;
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; ++i) {
				if (elementValueComparer.Equals(a[i], b[i])) {
					return false;
				}
			}
			return true;
		}

		private static int ComputeHashCode(ValueComparer elementValueComparer, T[] e) {
			return e.Aggregate(104179, (h, e) => HashCode.Combine(h, elementValueComparer.GetHashCode(e)));
		}

		private static T[] makeSnapshot(ValueComparer elementValueComparer, T[] e) {
			var res = new T[e.Length];
			for (int i = 0; i < e.Length; ++i) {
				res[i] = (T)elementValueComparer.Snapshot(e[i])!;
			}
			return res;
		}
	}
}
