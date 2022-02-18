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
			var keyId1 = KeyId.CalculateId(keyPair.Public);
			using var strWriter = new StringWriter();
			keyPair.Public.StoreToPem(strWriter);
			using var strReader = new StringReader(strWriter.ToString());
			var loadedPubKey = PublicKey.LoadOneFromPem(strReader);
			var keyId2 = KeyId.CalculateId(loadedPubKey);
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
			var keyId1 = KeyId.CalculateId(keyPair.Public);
			var derivedPubKey = keyPair.Private.DerivePublicKey();
			var keyId2 = KeyId.CalculateId(derivedPubKey);
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
			var keyId1 = KeyId.CalculateId(keyPair.Public);
			var derivedPubKey = keyPair.Private.DerivePublicKey();
			using var strWriter = new StringWriter();
			derivedPubKey.StoreToPem(strWriter);
			using var strReader = new StringReader(strWriter.ToString());
			var loadedPubKey = PublicKey.LoadOneFromPem(strReader);
			var keyId2 = KeyId.CalculateId(loadedPubKey);
			Assert.Equal(keyId1, keyId2);
		}
	}
}
