using System;
using System.Linq;

namespace SGL.Utilities {
	/// <summary>
	/// A simple utility class to generate pseudo-random strings.
	/// Note: The randomness used by this class is NOT cryptographically secure true randomness but just from a regular PRNG.
	/// If true randomness is needed, see <see cref="SecretGenerator"/>.
	///
	/// For simplicity, this class provides static methods for generating random strings / words.
	/// As the underlying random generator is not thread-safe, these do however operate under a lock internally.
	/// If many random strings are needed, using code can instead instantiate the generator on a per-thread basis and use the non-static methods.
	/// </summary>
	public class StringGenerator {
		private Random rnd = new Random();
		private static readonly char[] wordCharacters = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Select(c => (char)c).ToArray();
		private static readonly char[] charactersWithSpace = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Append(' ').Select(c => (char)c).ToArray();
		private static StringGenerator sharedInstance = new StringGenerator();

		/// <summary>
		/// Generates a random string of the given length, consisting of lower-case letters, upper-case letters, digits and spaces.
		/// </summary>
		/// <param name="length">The number of characters to generate.</param>
		/// <returns>The generated string.</returns>
		public static string GenerateRandomString(int length) {
			lock (sharedInstance) {
				return sharedInstance.ProduceRandomString(length);
			}
		}
		/// <summary>
		/// Generates a random string of the given length, consisting of lower-case letters, upper-case letters, and digits, but no spaces.
		/// </summary>
		/// <param name="length">The number of characters to generate.</param>
		/// <returns>The generated string.</returns>
		public static string GenerateRandomWord(int length) {
			lock (sharedInstance) {
				return sharedInstance.ProduceRandomWord(length);
			}
		}

		/// <summary>
		/// Generates a random string of the given length, consisting of lower-case letters, upper-case letters, digits and spaces.
		/// </summary>
		/// <param name="length">The number of characters to generate.</param>
		/// <returns>The generated string.</returns>
		public string ProduceRandomString(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => charactersWithSpace[rnd.Next(charactersWithSpace.Length)]).ToArray());
		}
		/// <summary>
		/// Generates a random string of the given length, consisting of lower-case letters, upper-case letters, and digits, but no spaces.
		/// </summary>
		/// <param name="length">The number of characters to generate.</param>
		/// <returns>The generated string.</returns>
		public string ProduceRandomWord(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => wordCharacters[rnd.Next(wordCharacters.Length)]).ToArray());
		}
	}
}
