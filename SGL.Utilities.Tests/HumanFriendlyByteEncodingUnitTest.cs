using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Tests {
	public class HumanFriendlyByteEncodingUnitTest {
		public static IEnumerable<object[]> GetArraySizes() {
			yield return new object[] { 0 };
			for (int i = 1; i <= 20; ++i) {
				yield return new object[] { i };
			}
			for (int i = 21; i < 50; i += 3) {
				yield return new object[] { i };
			}
			for (int i = 50; i < 1000; i *= 2) {
				yield return new object[] { i };
			}
		}

		private ITestOutputHelper output;
		private Random random = new Random();

		public HumanFriendlyByteEncodingUnitTest(ITestOutputHelper output) {
			this.output = output;
		}

		private byte[] getBytes(int size) {
			byte[] res = new byte[size];
			random.NextBytes(res);
			return res;
		}

		[Theory]
		[MemberData(nameof(GetArraySizes))]
		public void EncodingCorrectlyRoundTripsForArbitraryByteArrays(int size) {
			var orig = getBytes(size);
			var encoded = HumanFriendlyByteEncoding.GetString(orig);
			output.WriteLine(encoded);
			var decoded = HumanFriendlyByteEncoding.GetBytes(encoded);
			Assert.Equal(orig, decoded);
		}
		[Theory]
		[MemberData(nameof(GetArraySizes))]
		public void EncodingToleratesCaseChanges(int size) {
			var orig = getBytes(size);
			var encoded = HumanFriendlyByteEncoding.GetString(orig);
			encoded = encoded.ToLower();
			output.WriteLine(encoded);
			var decoded = HumanFriendlyByteEncoding.GetBytes(encoded);
			Assert.Equal(orig, decoded);
		}
	}
}
