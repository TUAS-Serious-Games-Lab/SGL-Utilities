using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class KeyDerivationUnitTestTheoryData : IEnumerable<object[]> {
		public IEnumerable<string> Inputs { get; }
		public IEnumerable<string> Salts { get; }
		public IEnumerable<int> Lengths { get; }

		public KeyDerivationUnitTestTheoryData(IEnumerable<string> inputs, IEnumerable<string> saltss, IEnumerable<int> lengths) {
			Inputs = inputs;
			Salts = saltss;
			Lengths = lengths;
		}

		public IEnumerator<object[]> GetEnumerator() {
			foreach (var input in Inputs) {
				foreach (var salt in Salts) {
					foreach (var length in Lengths) {
						yield return new object[] { input, salt, length };
					}
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			foreach (var item in (this as IEnumerable<object[]>)) {
				yield return item;
			}
		}
	}

	public class KeyDerivationUnitTest {
		public static string[] Inputs = { StringGenerator.GenerateRandomString(2), StringGenerator.GenerateRandomString(8), StringGenerator.GenerateRandomString(15), StringGenerator.GenerateRandomString(30), StringGenerator.GenerateRandomString(100) };
		public static string[] Salts = { "Salt", "This is a test", "KeyDerivationUnitTest", StringGenerator.GenerateRandomString(8), StringGenerator.GenerateRandomString(15), StringGenerator.GenerateRandomString(30) };
		public static int[] Lengths = { 7, 8, 13, 16, 32, 42, 64, 1024 };
		public static KeyDerivationUnitTestTheoryData TestData = new KeyDerivationUnitTestTheoryData(Inputs, Salts, Lengths);
		[Theory]
		[MemberData(nameof(TestData))]
		public void DifferentInputSecretStringsProduceDifferentOutput(string input1, string salt, int length) {
			var tmp = input1.ToCharArray();
			tmp[1] ^= (char)2;
			var input2 = new string(tmp);
			var output1 = KeyDerivation.DeriveBytes(input1, salt, length);
			var output2 = KeyDerivation.DeriveBytes(input2, salt, length);
			Assert.NotEqual(output1, output2);
		}
		[Theory]
		[MemberData(nameof(TestData))]
		public void SaltStringsProduceDifferentOutput(string input, string salt1, int length) {
			var tmp = salt1.ToCharArray();
			tmp[1] ^= (char)2;
			var salt2 = new string(tmp);
			var output1 = KeyDerivation.DeriveBytes(input, salt1, length);
			var output2 = KeyDerivation.DeriveBytes(input, salt2, length);
			Assert.NotEqual(output1, output2);
		}
		[Theory]
		[MemberData(nameof(TestData))]
		public void IdenticalInputProducesIdenticalOutput(string input, string salt, int length) {
			var output1 = KeyDerivation.DeriveBytes(input, salt, length);
			var output2 = KeyDerivation.DeriveBytes(input, salt, length);
			Assert.Equal(output1, output2);
		}
	}
}
