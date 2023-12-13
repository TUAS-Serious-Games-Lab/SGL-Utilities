using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {

	/// <summary>
	/// An exception thrown by <see cref="HumanFriendlyByteEncoding"/> if a given character input contained a character that is not valid in the encoding at its position (in prefix or in payload).
	/// </summary>
	public class InvalidEncodingCharacterException : Exception {
		/// <summary>
		/// Creates a new exception object with the given information.
		/// </summary>
		public InvalidEncodingCharacterException(char invalidCharacter, IEnumerable<char> validCharacters, Exception? innerException = null) :
			base($"The character '{invalidCharacter}' is invalid. Allowed characters are: {string.Join(", ", validCharacters.Select(c => $"'{c}'"))}", innerException) {
			InvalidCharacter = invalidCharacter;
			ValidCharacters = validCharacters;
		}

		/// <summary>
		/// The invalid character in the input.
		/// </summary>
		public char InvalidCharacter { get; }
		/// <summary>
		/// The characters that are allowed at the position of the character.
		/// </summary>
		public IEnumerable<char> ValidCharacters { get; }
	}

	/// <summary>
	/// A tool class that provides a an encoding that is designed to be easily handled by humans, including writing it down or typing in (short) strings of information, like secrets or identifiers.
	/// It uses a 32-element alphabet, that includes letters and numbers, but omits characters that can be easily confused with others when reading a printed string,
	/// e.g. <c>O</c> is omitted because it can be confused with <c>0</c>.
	/// Those omitted confusable characters are decoded as their the character as which they may be misread, i.e. if the input contains e.g. an <c>O</c>, which can't be correct, it is decoded as a <c>0</c>.
	/// Additionally, while the encoding methods produce only digits and upper-case letters, the decoding methods also support lower-case letters.
	/// </summary>
	/// <remarks>
	/// The actual data are encoded into the following alphabet:
	/// <list type="bullet">
	/// <item><term><c>0</c></term></item> <item><term><c>1</c></term></item> <item><term><c>2</c></term></item> <item><term><c>3</c></term></item> <item><term><c>4</c></term></item>
	/// <item><term><c>5</c></term></item> <item><term><c>6</c></term></item> <item><term><c>7</c></term></item> <item><term><c>8</c></term></item> <item><term><c>9</c></term></item>
	/// <item><term><c>A</c></term></item> <item><term><c>C</c></term></item> <item><term><c>D</c></term></item> <item><term><c>E</c></term></item> <item><term><c>F</c></term></item>
	/// <item><term><c>G</c></term></item> <item><term><c>H</c></term></item> <item><term><c>J</c></term></item> <item><term><c>K</c></term></item> <item><term><c>M</c></term></item>
	/// <item><term><c>N</c></term></item> <item><term><c>P</c></term></item> <item><term><c>Q</c></term></item> <item><term><c>R</c></term></item> <item><term><c>S</c></term></item>
	/// <item><term><c>T</c></term></item> <item><term><c>U</c></term></item> <item><term><c>V</c></term></item> <item><term><c>W</c></term></item> <item><term><c>X</c></term></item>
	/// <item><term><c>Y</c></term></item> <item><term><c>Z</c></term></item>
	/// </list>
	/// For each of letter in those characters, their lower-case counter-part maps to the same value aus the listed upper-case character when decoding.
	///
	/// Additionally, the following characters are mapped to their confusable counter-parts:
	/// <list type="table">
	/// <listheader><term>Input</term><term>Mapped to</term></listheader>
	/// <item><term><c>B</c></term><term><c>8</c></term></item>
	/// <item><term><c>b</c></term><term><c>6</c></term></item>
	/// <item><term><c>I</c></term><term><c>1</c></term></item>
	/// <item><term><c>i</c></term><term><c>1</c></term></item>
	/// <item><term><c>L</c></term><term><c>1</c></term></item>
	/// <item><term><c>l</c></term><term><c>1</c></term></item>
	/// <item><term><c>O</c></term><term><c>0</c></term></item>
	/// <item><term><c>o</c></term><term><c>0</c></term></item>
	/// </list>
	///
	/// The encoded strings start with a special character that is used to inform the decoder about the number of bytes at the end of the binary data that didn't fit into a full block.
	/// These are encoded as follows:
	/// <list type="table">
	/// <listheader><term>Prefix character</term><term>Meaning</term></listheader>
	/// <item><term><c>?</c></term><description>The data could be devided completely into full blocks.</description></item>
	/// <item><term><c><![CDATA[&]]></c></term><description>There were 1 bytes at the end that form an extra block of 2 characters.</description></item>
	/// <item><term><c>%</c></term><description>There were 2 bytes at the end that form an extra block of 4 characters.</description></item>
	/// <item><term><c>*</c></term><description>There were 3 bytes at the end that form an extra block of 5 characters.</description></item>
	/// <item><term><c>#</c></term><description>There were 4 bytes at the end that form an extra block of 7 characters.</description></item>
	/// </list>
	/// </remarks>
	public static class HumanFriendlyByteEncoding {
		private static readonly char[] bitsToCharMapping = new char[] {
				'0','1','2','3','4','5','6','7','8','9',
				'A','C','D','E','F','G','H','J','K','M',
				'N','P','Q','R','S','T','U','V','W','X',
				'Y','Z'
			};
		private static readonly Dictionary<char, byte> charToBitsMapping;
		private static readonly char[] extraBlockBytesToPrefixMapping = new char[] { '?', '&', '%', '*', '#' };
		private static readonly int[] extraBlockBytesToExtraBlockCharsMapping = new int[] { 0, 2, 4, 5, 7 };
		private static readonly Dictionary<char, byte> prefixToExtraBlockBytesMapping;
		private const int blockBytes = 5;
		private const int digitBits = 5;
		private const int blockChars = 8;

		static HumanFriendlyByteEncoding() {
			charToBitsMapping = bitsToCharMapping
				.SelectMany((c, i) => new[] { new { c = char.ToLowerInvariant(c), i }, new { c = char.ToUpperInvariant(c), i } })
				.Distinct()
				.ToDictionary(e => e.c, e => (byte)e.i);
			// Translate the characters skipped in the table to the character to which they are similar and thus as which they might have been misread:
			charToBitsMapping['B'] = charToBitsMapping['8'];
			charToBitsMapping['b'] = charToBitsMapping['6'];
			charToBitsMapping['I'] = charToBitsMapping['1'];
			charToBitsMapping['i'] = charToBitsMapping['1'];
			charToBitsMapping['L'] = charToBitsMapping['1'];
			charToBitsMapping['l'] = charToBitsMapping['1'];
			charToBitsMapping['O'] = charToBitsMapping['0'];
			charToBitsMapping['o'] = charToBitsMapping['0'];
			prefixToExtraBlockBytesMapping = extraBlockBytesToPrefixMapping.Select((c, i) => new { c, i }).ToDictionary(e => e.c, e => (byte)e.i);
		}

		/// <summary>
		/// Encodes the given byte array.
		/// </summary>
		/// <param name="input">The input to encode</param>
		/// <returns>The bytes encoded to characters.</returns>
		public static char[] GetChars(byte[] input) {
			int blocks = input.Length / blockBytes;
			int extraBlockBytes = input.Length % blockBytes;
			int extraBlockChars = extraBlockBytesToExtraBlockCharsMapping[extraBlockBytes];
			char[] result = new char[blocks * blockChars + extraBlockChars + 1];
			result[0] = extraBlockBytesToPrefixMapping[extraBlockBytes];
			for (int block = 0; block < blocks; ++block) {
				long buffer = 0;
				for (int byteIndex = 0; byteIndex < blockBytes; ++byteIndex) {
					buffer |= (long)input[block * blockBytes + byteIndex] << byteIndex * 8;
				}
				for (int charIndex = 0; charIndex < blockChars; ++charIndex) {
					result[1 + block * blockChars + charIndex] = bitsToCharMapping[buffer >> charIndex * digitBits & 0x1FL];
				}
			}
			long extraBuffer = 0;
			for (int byteIndex = 0; byteIndex < extraBlockBytes; ++byteIndex) {
				extraBuffer |= (long)input[blocks * blockBytes + byteIndex] << byteIndex * 8;
			}
			for (int charIndex = 0; charIndex < extraBlockChars; ++charIndex) {
				result[1 + blocks * blockChars + charIndex] = bitsToCharMapping[extraBuffer >> charIndex * digitBits & 0x1FL];
			}
			return result;
		}

		/// <summary>
		/// Encodes the given byte array.
		/// </summary>
		/// <param name="input">The input to encode</param>
		/// <returns>The bytes encoded to characters as a string.</returns>
		public static string GetString(byte[] input) => new(GetChars(input));

		private static byte CharToBits(char c) {
			if (charToBitsMapping.TryGetValue(c, out var bits)) {
				return bits;
			}
			else {
				throw new InvalidEncodingCharacterException(c, charToBitsMapping.Keys);
			}
		}

		/// <summary>
		/// Decodes the given characters into a byte array.
		/// </summary>
		/// <param name="input">The encoded data as characters.</param>
		/// <returns>The decoded bytes.</returns>
		public static byte[] GetBytes(char[] input) {
			if (input.Length == 0) {
				return Array.Empty<byte>();
			}
			if (!prefixToExtraBlockBytesMapping.TryGetValue(input[0], out byte extraBlockBytes)) {
				throw new InvalidEncodingCharacterException(input[0], prefixToExtraBlockBytesMapping.Keys);
			}
			int extraBlockChars = extraBlockBytesToExtraBlockCharsMapping[extraBlockBytes];
			int blocks = (input.Length - 1) / blockChars;
			if ((input.Length - 1) % blockChars != extraBlockChars) {
				throw new ArgumentException("Input length doesn't match with prefix character", nameof(input));
			}
			var result = new byte[blocks * blockBytes + extraBlockBytes];
			for (int block = 0; block < blocks; ++block) {
				long buffer = 0;
				for (int charIndex = 0; charIndex < blockChars; ++charIndex) {
					buffer |= (long)CharToBits(input[1 + block * blockChars + charIndex]) << charIndex * digitBits;
				}
				for (int byteIndex = 0; byteIndex < blockBytes; ++byteIndex) {
					result[block * blockBytes + byteIndex] = (byte)(buffer >> byteIndex * 8 & 0xFFL);
				}
			}
			long extraBuffer = 0;
			for (int charIndex = 0; charIndex < extraBlockChars; ++charIndex) {
				extraBuffer |= (long)CharToBits(input[1 + blocks * blockChars + charIndex]) << charIndex * digitBits;
			}
			for (int byteIndex = 0; byteIndex < extraBlockBytes; ++byteIndex) {
				result[blocks * blockBytes + byteIndex] = (byte)(extraBuffer >> byteIndex * 8 & 0xFFL);
			}
			return result;
		}

		/// <summary>
		/// Decodes the given characters into a byte array.
		/// </summary>
		/// <param name="input">The encoded data as a string.</param>
		/// <returns>The decoded bytes.</returns>
		public static byte[] GetBytes(string input) => GetBytes(input.ToCharArray());
	}
}
