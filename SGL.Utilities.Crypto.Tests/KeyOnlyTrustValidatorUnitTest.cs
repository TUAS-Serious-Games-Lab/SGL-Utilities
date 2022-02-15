using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {
	public class KeyOnlyTrustValidatorUnitTest {
		private readonly ILoggerFactory loggerFactory;
		private readonly KeyOnlyTrustValidator validator;
		private const string Signer1PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAoi7fkXxIfVdhoA7CzOZN
7Ne63Pm1w6qdhtyE0FcCrW2LGLt4gk8qs+0g73KpQpYm6ufSYGx75HXMqAfTrBbc
zWg3w27fsdXrgkf/VZQgzRYKVSLgi6EeWAf5xzuyrHzBNihjWK3ljZcpk5rKyf47
o+FYy+9nFoF/FIXA+IaDXEuzku2gp7H3alGRCmepURfS+xM8+ammQglk+0Ztskz2
WYtA8V/I+J+ESQWLHaA7LpS8YpEF3lXENII/KWpy/6jnFXEVnW9swv3V3g6u/+NC
unLxPP9eFwD7aTtznZeCG5Yr2ukAkiNmORybA4qd4BH0LheSdksabrn+rkVChP1M
4wY7TYiesiL9bKyz1v+YlzNM1ZzHmLdqJHUa+nAcqgDT2ssIa76gylAazo6lcJlY
w/4y7wHKVB+XxyLm6x09kQFjEbX55CBv//+7VW8dgJCZPX01TPXL+8RFEZjPiIOR
s8/NY4m3M330Y6LacrHZvb0yMCJfoTYGQ1mYCcw89vd3j5paPucys1YlcPv/FTp+
lH1vUjoQ43lBF/ZHaDngcIjucKcS5nyQ4regyWm4toVcaL/zfctT3n/k/MKidLbq
JzuAS0pdk+L5m1mzQfi/bbqm94LhVxCrp234hJDIySgYCcMu0vpeKMT97E2ONt+O
ftGsFKmNIo2l5He9PuPV4K0CAwEAAQ==
-----END PUBLIC KEY-----";
		private const string Signer1PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
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
-----END ENCRYPTED PRIVATE KEY-----
";

		private const string UnknownSignerPrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
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
-----END ENCRYPTED PRIVATE KEY-----
";

		private const string RecipientPublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQB7hv4NRhTbZC21b5GP4uOtV0igx2l
+xuxAxHJQfNiZXw55DvFQDC0EB8+nWgj0nZm9EBqLC+pLfL0/TVjkNHRPAABL6f/
6VBW9k6verWw5u97ePqPD8C6H90Lltz+/i4NE6QwoXmapz+DPznqDJzjX6fBZNgu
g30Pr6mO6JjUxgDch8E=
-----END PUBLIC KEY-----
";

		private readonly AsymmetricCipherKeyPair signer1KeyPair;
		private readonly RsaPrivateCrtKeyParameters unknownSignerPrivateKey;
		private readonly ECPublicKeyParameters recipientPublicKey;
		private readonly SecureRandom random = new SecureRandom();

		private class PasswordFinder : IPasswordFinder {
			public char[] GetPassword() {
				return new char[] { 't', 'e', 's', 't', 'p', 'w' };
			}
		}

		public KeyOnlyTrustValidatorUnitTest(ITestOutputHelper output) {
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			validator = new KeyOnlyTrustValidator(Signer1PublicKeyPem, loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			using var rdrS1 = new StringReader(Signer1PrivateKeyPem);
			PemReader pemRdrS1 = new PemReader(rdrS1, new PasswordFinder());
			var privKeyS1 = (RsaKeyParameters)pemRdrS1.ReadObject();
			signer1KeyPair = new AsymmetricCipherKeyPair(new RsaKeyParameters(false, privKeyS1.Modulus, privKeyS1.Exponent), privKeyS1);
			using var rdrUS = new StringReader(UnknownSignerPrivateKeyPem);
			PemReader pemRdrUS = new PemReader(rdrUS, new PasswordFinder());
			unknownSignerPrivateKey = (RsaPrivateCrtKeyParameters)pemRdrUS.ReadObject();
			using var rdrR1 = new StringReader(RecipientPublicKeyPem);
			PemReader pemRdrR1 = new PemReader(rdrR1);
			recipientPublicKey = (ECPublicKeyParameters)pemRdrR1.ReadObject();
		}

		[Fact]
		public void ValidCertificateIsAccpted() {
			var certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Signer 1"));
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Valid Test Cert"));
			certGen.SetSerialNumber(new BigInteger(128, random));
			certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(1));
			certGen.SetPublicKey(recipientPublicKey);
			Asn1SignatureFactory signatureFactory = new Asn1SignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), signer1KeyPair.Private);
			var cert = certGen.Generate(signatureFactory);

			Assert.True(validator.CheckCertificate(cert));
		}

		[Fact]
		public void NotYetValidCertificateIsRejected() {
			var certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Signer 1"));
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Not Yet Valid Test Cert"));
			certGen.SetSerialNumber(new BigInteger(128, random));
			certGen.SetNotBefore(DateTime.UtcNow.AddHours(1)); // NotBefore in the future
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(2));
			certGen.SetPublicKey(recipientPublicKey);
			Asn1SignatureFactory signatureFactory = new Asn1SignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), signer1KeyPair.Private);
			var cert = certGen.Generate(signatureFactory);

			Assert.False(validator.CheckCertificate(cert));
		}

		[Fact]
		public void ExpiredCertificateIsRejected() {
			var certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Signer 1"));
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Expired Test Cert"));
			certGen.SetSerialNumber(new BigInteger(128, random));
			certGen.SetNotBefore(DateTime.UtcNow.AddHours(-2));
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(-1)); // NotAfter in the past
			certGen.SetPublicKey(recipientPublicKey);
			Asn1SignatureFactory signatureFactory = new Asn1SignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), signer1KeyPair.Private);
			var cert = certGen.Generate(signatureFactory);

			Assert.False(validator.CheckCertificate(cert));
		}
		[Fact]
		public void CertificateFromUnknownSignerIsRejected() {
			var certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Some Signer"));
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Cert From Unknown Signer"));
			certGen.SetSerialNumber(new BigInteger(128, random));
			certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(1));
			certGen.SetPublicKey(recipientPublicKey);
			Asn1SignatureFactory signatureFactory = new Asn1SignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), unknownSignerPrivateKey);
			var cert = certGen.Generate(signatureFactory);

			Assert.False(validator.CheckCertificate(cert));
		}

		[Fact]
		public void CertificateWithInvalidSignatureDueToChangedContentIsRejected() {
			var certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Signer 1"));
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Cert With Pre-Signature Corruption"));
			certGen.SetSerialNumber(new BigInteger(128, random));
			certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(1));
			certGen.SetPublicKey(recipientPublicKey);
			var signatureFactory = new CorruptingSignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), signer1KeyPair.Private, random);
			signatureFactory.CorruptPreSignature = true;
			var cert = certGen.Generate(signatureFactory);

			Assert.False(validator.CheckCertificate(cert));
		}
		[Fact]
		public void CertificateWithInvalidSignatureDueToCorruptedSignatureIsRejected() {
			var certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Signer 1"));
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Cert With Pre-Signature Corruption"));
			certGen.SetSerialNumber(new BigInteger(128, random));
			certGen.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(1));
			certGen.SetPublicKey(recipientPublicKey);
			var signatureFactory = new CorruptingSignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), signer1KeyPair.Private, random);
			signatureFactory.CorruptPostSignature = true;
			var cert = certGen.Generate(signatureFactory);

			Assert.False(validator.CheckCertificate(cert));
		}
	}
}
