using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SGL.Utilities.TestUtilities.XUnit {
	/// <summary>
	/// Provides a variation of the regular <see cref="FactAttribute"/> that allows specifying a predicate in the form of a <see cref="Type"/> and the name of a static method on that type.
	/// This method is expected to return a boolean indicating whether the marked test should run.
	/// This can be used to mark e.g. higher-level integration tests that require some dependencies in the environment, e.g. a database container.
	/// The predicate can then e.g. check for (an) environment variable(s) that the test code also uses to find the dependency and only run the test if the variable is present.
	/// If the test is not run, it is skipped with either a fixed or a customizable skip reason.
	/// </summary>
	public class ConditionallyTestedFactAttribute : FactAttribute {
		/// <summary>
		/// Constructs an attribute object with the given data.
		/// </summary>
		/// <param name="predicateType">The type that contains the static predicate method.</param>
		/// <param name="predicateMethod">The name of the prdicate method within <paramref name="predicateType"/>.</param>
		public ConditionallyTestedFactAttribute(Type predicateType, string predicateMethod) : this(predicateType, predicateMethod, "Test run predicate returned false.") { }
		/// <summary>
		/// Constructs an attribute object with the given data.
		/// </summary>
		/// <param name="predicateType">The type that contains the static predicate method.</param>
		/// <param name="predicateMethod">The name of the prdicate method within <paramref name="predicateType"/>.</param>
		/// <param name="skipReason">The reason to give when the test is skipped.</param>
		public ConditionallyTestedFactAttribute(Type predicateType, string predicateMethod, string skipReason) {
			var method = predicateType.GetMethod(predicateMethod, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			var predicateResult = method?.Invoke(null, Array.Empty<object>());
			if (predicateResult is bool predRes) {
				if (!predRes) {
					Skip = skipReason;
				}
			}
			else if (method == null) {
				throw new ArgumentException("Given predicate method not found.");
			}
			else {
				throw new ArgumentException("Given predicate method returns incorrect type. Expecting bool.");
			}
		}
	}
}