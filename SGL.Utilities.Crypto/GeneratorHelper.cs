using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace SGL.Utilities.Crypto {
	internal class GeneratorHelper {
		public static KeyPair GenerateEcKeyPair(RandomGenerator random, int keyLength, string? curveName = null) {
			ECKeyPairGenerator keyGen = new ECKeyPairGenerator();
			if (curveName != null) {
				keyGen.Init(new ECKeyGenerationParameters(ECNamedCurveTable.GetOid(curveName), random.wrapped));
			}
			else {
				keyGen.Init(new KeyGenerationParameters(random.wrapped, keyLength));
			}
			return new KeyPair(keyGen.GenerateKeyPair());
		}
		public static KeyPair GenerateRsaKeyPair(RandomGenerator random, int keyLength) {
			RsaKeyPairGenerator keyGen = new RsaKeyPairGenerator();
			keyGen.Init(new KeyGenerationParameters(random.wrapped, keyLength));
			return new KeyPair(keyGen.GenerateKeyPair());
		}
		public static KeyPair GenerateKeyPair(RandomGenerator random, KeyType type, int keyLength, string? paramSetName = null) {
			switch (type) {
				case KeyType.RSA: return GenerateRsaKeyPair(random, keyLength);
				case KeyType.EllipticCurves: return GenerateEcKeyPair(random, keyLength, paramSetName);
				default: throw new KeyException("Unsupported key type");
			}
		}
	}
}
