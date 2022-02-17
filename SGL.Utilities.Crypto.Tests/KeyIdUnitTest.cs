using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.IO;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class KeyIdUnitTest {
		private SecureRandom random = new SecureRandom();

		[Theory]
		[InlineData(typeof(RsaKeyPairGenerator), 1024)]
		[InlineData(typeof(RsaKeyPairGenerator), 2048)]
		[InlineData(typeof(RsaKeyPairGenerator), 4096)]
		[InlineData(typeof(ECKeyPairGenerator), 192)]
		[InlineData(typeof(ECKeyPairGenerator), 224)]
		[InlineData(typeof(ECKeyPairGenerator), 239)]
		[InlineData(typeof(ECKeyPairGenerator), 256)]
		[InlineData(typeof(ECKeyPairGenerator), 384)]
		[InlineData(typeof(ECKeyPairGenerator), 521)]
		public void KeyIdStaysConsistentThroughPemSerialization(Type keyGenType, int keySize) {
			var keyGen = (IAsymmetricCipherKeyPairGenerator)(keyGenType.GetConstructor(new Type[] { })?.Invoke(new object[] { }) ?? throw new Exception("Couldn't create key pair generator."));
			keyGen.Init(new KeyGenerationParameters(random, keySize));
			var keyPair = keyGen.GenerateKeyPair();
			var keyId1 = KeyId.CalculateId(new PublicKey(keyPair.Public));
			using var strWriter = new StringWriter();
			var pemWriter = new PemWriter(strWriter);
			pemWriter.WriteObject(keyPair.Public);
			using var strReader = new StringReader(strWriter.ToString());
			var pemReader = new PemReader(strReader);
			var loadedPubKey = (AsymmetricKeyParameter)pemReader.ReadObject();
			var keyId2 = KeyId.CalculateId(new PublicKey(loadedPubKey));
			Assert.Equal(keyId1, keyId2);
		}

		[Theory]
		[InlineData(typeof(ECKeyPairGenerator), 192)]
		[InlineData(typeof(ECKeyPairGenerator), 224)]
		[InlineData(typeof(ECKeyPairGenerator), 239)]
		[InlineData(typeof(ECKeyPairGenerator), 256)]
		[InlineData(typeof(ECKeyPairGenerator), 384)]
		[InlineData(typeof(ECKeyPairGenerator), 521)]
		public void KeyIdStaysConsistentThroughECPrivateToPublicKeyDerivation(Type keyGenType, int keySize) {
			var keyGen = (IAsymmetricCipherKeyPairGenerator)(keyGenType.GetConstructor(new Type[] { })?.Invoke(new object[] { }) ?? throw new Exception("Couldn't create key pair generator."));
			keyGen.Init(new KeyGenerationParameters(random, keySize));
			var keyPair = keyGen.GenerateKeyPair();
			var keyId1 = KeyId.CalculateId(new PublicKey(keyPair.Public));
			var privKey = ((ECPrivateKeyParameters)keyPair.Private);
			var q = privKey.Parameters.G.Multiply(privKey.D);
			var derivedPubKey = new ECPublicKeyParameters(privKey.AlgorithmName, q, privKey.PublicKeyParamSet);
			var keyId2 = KeyId.CalculateId(new PublicKey(derivedPubKey));
			Assert.Equal(keyId1, keyId2);
		}

		[Theory]
		[InlineData(typeof(ECKeyPairGenerator), 192)]
		[InlineData(typeof(ECKeyPairGenerator), 224)]
		[InlineData(typeof(ECKeyPairGenerator), 239)]
		[InlineData(typeof(ECKeyPairGenerator), 256)]
		[InlineData(typeof(ECKeyPairGenerator), 384)]
		[InlineData(typeof(ECKeyPairGenerator), 521)]
		public void KeyIdStaysConsistentThroughECPrivateToPublicKeyDerivationAndPemSerialization(Type keyGenType, int keySize) {
			var keyGen = (IAsymmetricCipherKeyPairGenerator)(keyGenType.GetConstructor(new Type[] { })?.Invoke(new object[] { }) ?? throw new Exception("Couldn't create key pair generator."));
			keyGen.Init(new KeyGenerationParameters(random, keySize));
			var keyPair = keyGen.GenerateKeyPair();
			var keyId1 = KeyId.CalculateId(new PublicKey(keyPair.Public));
			var privKey = ((ECPrivateKeyParameters)keyPair.Private);
			var q = privKey.Parameters.G.Multiply(privKey.D);
			var derivedPubKey = new ECPublicKeyParameters(privKey.AlgorithmName, q, privKey.PublicKeyParamSet);
			using var strWriter = new StringWriter();
			var pemWriter = new PemWriter(strWriter);
			pemWriter.WriteObject(derivedPubKey);
			using var strReader = new StringReader(strWriter.ToString());
			var pemReader = new PemReader(strReader);
			var loadedPubKey = (AsymmetricKeyParameter)pemReader.ReadObject();
			var keyId2 = KeyId.CalculateId(new PublicKey(loadedPubKey));
			Assert.Equal(keyId1, keyId2);
		}
	}
}
