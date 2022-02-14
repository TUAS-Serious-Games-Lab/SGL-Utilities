﻿using Microsoft.Extensions.Logging;
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
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA5gQHKuxEKfV1AoqBsUtA
3OQG6Ig6xn2fSKXgMuIwGEDZv3auzdMR6SB0ZVcMeFEXNs8CUwn3hfGuJpMSY5Vz
RAblKWdynqEQiMNDNZnLg4BEleDBTUqueJhfjvphBjffRkHJdxoqEQ1BaKdvd/UJ
hI2WQj8fBEcqYf9NDISPXJsJUZu+j4V/xREBxfO85Rl6qwfhN+3Ua9kjGnsf4xCL
PzO77U0mvpAjvQFNBbwcXH0hGNGzuyhCX+mCbJmzLc9tVWAso7WTDGnnjmuLOWjy
C4rVROXJT9Nme+TdD9pgYY/Uru415Mk953pnhGjhZi0IPssFsyiUSs8eJNjJFoWz
jiQLH2MTXrHwl88t4ZBbmRN3G0aTUkSFHFQzWl2J0MMwIYE8KdAiUCizwsFItgxL
fugxiOSTeqdMg0g7FKIBqzlfVq5mwqNl78r59FcCVOSHRa2RVa094UDvwR++bXCa
cX/GwX9oP4HbkDJN2eoOVluoia5vvwYJLrA2CjBhEi6WcfoLlGj0zpWCTrCEzf3L
xlJ7w6GuSPoiwU1NOgr93+GkW1EtBb0prqVX9mHPugfdul4wUOICzNEQ6+2fCOxA
UJR6d+Jd+11ZEBpxX9DBcLRweppl2yhTB/diO1bboOpvJJMOt3Uokypx9vYcaxga
2abX7/COBY5yuBhZFZhDCcMCAwEAAQ==
-----END PUBLIC KEY-----";
		private const string Signer1PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
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
		private readonly ECPublicKeyParameters recipientPublicKey;
		private readonly SecureRandom random = new SecureRandom();

		public KeyOnlyTrustValidatorUnitTest(ITestOutputHelper output) {
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			validator = new KeyOnlyTrustValidator(Signer1PublicKeyPem, loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			using var rdrS1 = new StringReader(Signer1PrivateKeyPem);
			PemReader pemRdrS1 = new PemReader(rdrS1);
			var privKeyS1 = (RsaKeyParameters)pemRdrS1.ReadObject();
			signer1KeyPair = new AsymmetricCipherKeyPair(new RsaKeyParameters(false, privKeyS1.Modulus, privKeyS1.Exponent), privKeyS1);
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
	}
}
