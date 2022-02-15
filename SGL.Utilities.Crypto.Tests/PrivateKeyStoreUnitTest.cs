using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class PrivateKeyStoreUnitTest {
		private char[] pemPassword = { 'T', 'e', 's', 't', 'P', 'a', 's', 's', 'w', 'o', 'r', 'd' };
		private const string rsaPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
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
-----END ENCRYPTED PRIVATE KEY-----
";
		private const string ecPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
MIIBXTBXBgkqhkiG9w0BBQ0wSjApBgkqhkiG9w0BBQwwHAQI3Ya//LMF+D0CAggA
MAwGCCqGSIb3DQIJBQAwHQYJYIZIAWUDBAEqBBDt4VQX8wgpZe0GDB/+s1dABIIB
AAkktLnHLdNy+0LuasxQJnAq+c+ulqo/MthuESvJIqPIjQ9sVKx3kzBERSO/F/ij
aeKuH5b53VLJDXT3jVQcLkdEUhDlD+Rj2zsldW2TEKsSjHhmPaQ1TLWwXwoBX+/E
gVRbM1N6x7zfe2u30KtljTkz78dgIRZvuCVPrCadN1DkrFOGYcANKJ3ZTZgUJ4Bo
OTxncegeAD2dYqtwYLprPB5BzQ5jk5Luoq4q5qzi2kN+g/ZRGcII8PZ/g+gQApK/
UwyO3lzqOWpYMZ6lZEXRDHynHH9vNxaDPRm+MZgkwyG7NO2oJHNAmG/ReFwYiJUj
wXrNoe5yySoWSz6TobSuURw=
-----END ENCRYPTED PRIVATE KEY-----
";

		[Fact]
		public void RsaPrivateKeyPemFileGeneratedByOpenSslCanBeLoadedCorrectly() {
			PrivateKeyStore pks = new PrivateKeyStore();
			using var rdr = new StringReader(rsaPem);
			pks.LoadKeyPair(rdr, pemPassword);
			Assert.NotNull(pks.KeyPair);
			Assert.IsType<RsaPrivateCrtKeyParameters>(pks.KeyPair.Private);
			Assert.IsType<RsaKeyParameters>(pks.KeyPair.Public);
			// We have loaded a key pair, perform an encryption / decryption cycle to test if it is (mathematically) valid:
			SecureRandom rand = new SecureRandom();
			byte[] testData = new byte[128];
			rand.NextBytes(testData);
			var rsa = new Pkcs1Encoding(new RsaEngine());
			rsa.Init(forEncryption: true, pks.KeyPair.Public);
			var encrypted = rsa.ProcessBlock(testData, 0, testData.Length);
			rsa = new Pkcs1Encoding(new RsaEngine());
			rsa.Init(forEncryption: false, pks.KeyPair.Private);
			var decrypted = rsa.ProcessBlock(encrypted, 0, encrypted.Length);
			Assert.Equal(testData, decrypted);
		}
		[Fact]
		public void EcPrivateKeyPemFileGeneratedByOpenSslCanBeLoadedCorrectly() {
			PrivateKeyStore pks = new PrivateKeyStore();
			using var rdr = new StringReader(ecPem);
			pks.LoadKeyPair(rdr, pemPassword);
			Assert.NotNull(pks.KeyPair);
			var privKey = Assert.IsType<ECPrivateKeyParameters>(pks.KeyPair.Private);
			var pubKey = Assert.IsType<ECPublicKeyParameters>(pks.KeyPair.Public);
			// We have loaded a key pair, generate a metching one and perform ECDH to test if it is (mathematically) valid:
			SecureRandom random = new SecureRandom();
			var ecKeyGenParams = new ECKeyGenerationParameters(pubKey.PublicKeyParamSet, random);
			var ecKeyGen = new ECKeyPairGenerator();
			ecKeyGen.Init(ecKeyGenParams);
			var otherKeyPair = ecKeyGen.GenerateKeyPair();
			var ecdh = new ECDHBasicAgreement();
			ecdh.Init(privKey);
			var agreement1 = ecdh.CalculateAgreement(otherKeyPair.Public);
			ecdh = new ECDHBasicAgreement();
			ecdh.Init(otherKeyPair.Private);
			var agreement2 = ecdh.CalculateAgreement(pubKey);
			Assert.Equal(agreement1, agreement2);
		}
	}
}
