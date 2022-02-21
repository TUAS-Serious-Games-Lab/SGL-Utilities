using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.EntityFrameworkCore {
	/// <summary>
	/// Provides EFCore conversion functionality as extension methods.
	/// </summary>
	public static class KeyIdConversionExtensions {
		/// <summary>
		/// Sets up the KeyId-typed property represented by <paramref name="property"/> to be stored as its raw id bytes in the database.
		/// </summary>
		/// <param name="property">The property for which to setup the converion.</param>
		/// <returns>A reference to <paramref name="property"/> for chaining.</returns>
		public static PropertyBuilder<KeyId> IsStoredAsByteArray(this PropertyBuilder<KeyId> property) {
			return property.HasConversion(keyId => keyId.Id, raw => new KeyId(raw));
		}
	}
}
