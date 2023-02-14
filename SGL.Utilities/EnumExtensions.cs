using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides utility extension methods for working with instances of enum types.
	/// </summary>
	public static class EnumExtensions {
		/// <summary>
		/// Returns the flag enum values present in <paramref name="value"/> as an array.
		/// </summary>
		/// <typeparam name="TEnum">The enum type. For this method to be usefull, the type should be a flags enum, as indicated by <see cref="FlagsAttribute"/>.</typeparam>
		/// <param name="value">The enum instance to analyse.</param>
		/// <returns>
		/// An array with all enum values from <typeparamref name="TEnum"/> that are present in <paramref name="value"/>.
		/// </returns>
		public static TEnum[] GetPresentValues<TEnum>(this TEnum value) where TEnum : Enum =>
			Enum.GetValues(typeof(TEnum)).OfType<TEnum>().Where(flag => value.HasFlag(flag)).ToArray();

		/// <summary>
		/// Returns whether any enum flags defined in <typeparamref name="TEnum"/> are set in <paramref name="value"/>.
		/// This is similar to a check for inequality to 0, but ignores bits in <paramref name="value"/> that don't belong to any defined flag.
		/// </summary>
		/// <typeparam name="TEnum">The type of the enum to operate on.</typeparam>
		/// <param name="value">The enum value potentially containing valid flag bits.</param>
		/// <returns><see langword="true"/> if <paramref name="value"/> contains any valid flag, <see langword="false"/> otherwise.</returns>
		public static bool HasAnyDefinedFlagsPresent<TEnum>(this TEnum value) where TEnum : Enum =>
			Enum.GetValues(typeof(TEnum)).OfType<TEnum>().Any(flag => value.HasFlag(flag));
	}
}
