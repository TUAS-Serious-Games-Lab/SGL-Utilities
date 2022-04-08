using SGL.Utilities.Crypto.Keys;
using System.IO;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class KeyIdUnitTest {
		private RandomGenerator random = new RandomGenerator();

		[Theory]
		[InlineData(KeyType.RSA, 1024)]
		[InlineData(KeyType.RSA, 2048)]
		[InlineData(KeyType.RSA, 4096)]
		[InlineData(KeyType.EllipticCurves, 192)]
		[InlineData(KeyType.EllipticCurves, 224)]
		[InlineData(KeyType.EllipticCurves, 239)]
		[InlineData(KeyType.EllipticCurves, 256)]
		[InlineData(KeyType.EllipticCurves, 384)]
		[InlineData(KeyType.EllipticCurves, 521)]
		public void KeyIdStaysConsistentThroughPemSerialization(KeyType keyGenType, int keySize) {
			var keyPair = KeyPair.Generate(random, keyGenType, keySize);
			var keyId1 = keyPair.Public.CalculateId();
			using var strWriter = new StringWriter();
			keyPair.Public.StoreToPem(strWriter);
			using var strReader = new StringReader(strWriter.ToString());
			var loadedPubKey = PublicKey.LoadOneFromPem(strReader);
			var keyId2 = loadedPubKey.CalculateId();
			Assert.Equal(keyId1, keyId2);
		}

		[Theory]
		[InlineData(192)]
		[InlineData(224)]
		[InlineData(239)]
		[InlineData(256)]
		[InlineData(384)]
		[InlineData(521)]
		public void KeyIdStaysConsistentThroughECPrivateToPublicKeyDerivation(int keySize) {
			var keyPair = KeyPair.GenerateEllipticCurves(random, keySize);
			var keyId1 = keyPair.Public.CalculateId();
			var derivedPubKey = keyPair.Private.DerivePublicKey();
			var keyId2 = derivedPubKey.CalculateId();
			Assert.Equal(keyId1, keyId2);
		}

		[Theory]
		[InlineData(192)]
		[InlineData(224)]
		[InlineData(239)]
		[InlineData(256)]
		[InlineData(384)]
		[InlineData(521)]
		public void KeyIdStaysConsistentThroughECPrivateToPublicKeyDerivationAndPemSerialization(int keySize) {
			var keyPair = KeyPair.GenerateEllipticCurves(random, keySize);
			var keyId1 = keyPair.Public.CalculateId();
			var derivedPubKey = keyPair.Private.DerivePublicKey();
			using var strWriter = new StringWriter();
			derivedPubKey.StoreToPem(strWriter);
			using var strReader = new StringReader(strWriter.ToString());
			var loadedPubKey = PublicKey.LoadOneFromPem(strReader);
			var keyId2 = loadedPubKey.CalculateId();
			Assert.Equal(keyId1, keyId2);
		}

		[Fact]
		public void KeyIdsFromSameKeyAreEqual() {
			var keyPair = KeyPair.GenerateEllipticCurves(random, 521);
			var keyId1 = keyPair.Public.CalculateId();
			var keyId2 = keyPair.Public.CalculateId();
			Assert.Equal(keyId1, keyId2);
			Assert.True(keyId1 == keyId2);
		}

		[Fact]
		public void KeyIdsFromDifferentKeysAreNotEqual() {
			var keyPair1 = KeyPair.GenerateEllipticCurves(random, 521);
			var keyPair2 = KeyPair.GenerateEllipticCurves(random, 521);
			var keyId1 = keyPair1.Public.CalculateId();
			var keyId2 = keyPair2.Public.CalculateId();
			Assert.NotEqual(keyId1, keyId2);
			Assert.True(keyId1 != keyId2);
		}

	}
}
