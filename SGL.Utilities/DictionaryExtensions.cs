using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides utility extension methods for working with dictionaries.
	/// </summary>
	public static class DictionaryExtensions {
		/// <summary>
		/// Takes a dictionary and generates a new dictionary with the same keys as the input and values that are projected from the original values.
		/// </summary>
		/// <typeparam name="TKey">The type of the keys.</typeparam>
		/// <typeparam name="TInValue">The type of the projected values.</typeparam>
		/// <typeparam name="TOutValue">The type of the original values.</typeparam>
		/// <param name="dict">The input dictionary.</param>
		/// <param name="projection">The projection function to apply to the input values.</param>
		/// <returns>The projected dictionary.</returns>
		public static Dictionary<TKey, TOutValue> ToProjectedDictionary<TKey, TInValue, TOutValue>(this IReadOnlyDictionary<TKey, TInValue> dict, Func<TInValue, TOutValue> projection) where TKey : notnull {
			return dict.ToDictionary(kvp => kvp.Key, kvp => projection(kvp.Value));
		}
	}
}
