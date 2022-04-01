using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SGL.Utilities.Validation {
	/// <summary>
	/// Validates that an <see cref="IEnumerable{T}"/>-typed property has a specified minimum number of elements.
	/// </summary>
	public class MinElementsAttribute : ValidationAttribute {

		/// <summary>
		/// The minimum number of elements that the property value must have to be valid.
		/// </summary>
		public int Count { get; }
		/// <summary>
		/// Constructs the attribute object with the given minimum number of elements.
		/// </summary>
		/// <param name="count">The minimum number of elements.</param>
		public MinElementsAttribute(int count = 1) : base(() => $"{{0}} must be a sequence and contain at least {count} elements.") {
			Count = count;
		}

		/// <inheritdoc/>
		protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
			if (value == null) return ValidationResult.Success;
			if (!(value is IEnumerable<object> seq)) return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
			if (seq.Count() < Count) return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
			return ValidationResult.Success;
		}
	}
}
