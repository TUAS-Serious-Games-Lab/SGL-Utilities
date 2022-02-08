using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Tests {
	public class E2ECryptoFixture {
		private readonly SecureRandom random;
		private readonly AsymmetricCipherKeyPair signerKeyPair;
		private readonly AsymmetricCipherKeyPair rsaKeyPair1;
		private readonly AsymmetricCipherKeyPair rsaKeyPair2;
		private readonly AsymmetricCipherKeyPair ecKeyPair1;
		private readonly AsymmetricCipherKeyPair ecKeyPair2;

		private readonly AsymmetricCipherKeyPair rsaKeyPairAttacker;
		private readonly AsymmetricCipherKeyPair ecKeyPairAttacker;

		private readonly X509Certificate rsaCert1;
		private readonly X509Certificate rsaCert2;
		private readonly X509Certificate ecCert1;
		private readonly X509Certificate ecCert2;

		private readonly AsymmetricCipherKeyPair attackerSigningKeyPair;
		private readonly X509Certificate rsaCertAttacker;
		private readonly X509Certificate ecCertAttacker;

		private readonly byte[] signerPubPem;
		private readonly byte[] rsaPub1Pem;
		private readonly byte[] rsaPub2Pem;
		private readonly byte[] ecPub1Pem;
		private readonly byte[] ecPub2Pem;

		private readonly byte[] rsaPubAttackerPem;
		private readonly byte[] ecPubAttackerPem;

		private readonly char[] privKeyPassword;
		private readonly byte[] signerPrivPem;
		private readonly byte[] rsaPriv1Pem;
		private readonly byte[] rsaPriv2Pem;
		private readonly byte[] ecPriv1Pem;
		private readonly byte[] ecPriv2Pem;

		private readonly byte[] rsaPrivAttackerPem;
		private readonly byte[] ecPrivAttackerPem;

		private readonly byte[] rsaCert1Pem;
		private readonly byte[] rsaCert2Pem;
		private readonly byte[] ecCert1Pem;
		private readonly byte[] ecCert2Pem;

		private readonly byte[] rsaCertAttackerPem;
		private readonly byte[] ecCertAttackerPem;

		private Task<AsymmetricCipherKeyPair> GenerateRsaKeyPairAsync(int length) {
			var rnd = SecureRandom.GetInstance("SHA256PRNG", false);
			rnd.SetSeed(random.GenerateSeed(1024));
			return Task.Run(() => {
				KeyGenerationParameters rsaKeyParams = new KeyGenerationParameters(random, length);
				RsaKeyPairGenerator rsaGen = new RsaKeyPairGenerator();
				rsaGen.Init(rsaKeyParams);
				return rsaGen.GenerateKeyPair();
			});
		}
		private Task<AsymmetricCipherKeyPair> GenerateEcKeyPairAsync(int length) {
			var rnd = SecureRandom.GetInstance("SHA256PRNG", false);
			rnd.SetSeed(random.GenerateSeed(1024));
			return Task.Run(() => {
				KeyGenerationParameters ecKeyParams = new KeyGenerationParameters(random, length);
				ECKeyPairGenerator ecGen = new ECKeyPairGenerator();
				ecGen.Init(ecKeyParams);
				return ecGen.GenerateKeyPair();
			});
		}

		public E2ECryptoFixture() {
			random = new SecureRandom();

			var rsaGeneratorTasks = Enumerable.Range(0, 5).Select(_ => GenerateRsaKeyPairAsync(4096)).ToArray();
			var ecGeneratorTasks = Enumerable.Range(0, 3).Select(_ => GenerateEcKeyPairAsync(521)).ToArray();

			Task.WaitAll(rsaGeneratorTasks.Concat(ecGeneratorTasks).ToArray());

			signerKeyPair = rsaGeneratorTasks.ElementAt(0).Result;
			attackerSigningKeyPair = rsaGeneratorTasks.ElementAt(1).Result;
			rsaKeyPair1 = rsaGeneratorTasks.ElementAt(2).Result;
			rsaKeyPair2 = rsaGeneratorTasks.ElementAt(3).Result;
			rsaKeyPairAttacker = rsaGeneratorTasks.ElementAt(4).Result;
			ecKeyPair1 = ecGeneratorTasks.ElementAt(0).Result;
			ecKeyPair2 = ecGeneratorTasks.ElementAt(1).Result;
			ecKeyPairAttacker = ecGeneratorTasks.ElementAt(2).Result;

			Asn1SignatureFactory signatureFactory = new Asn1SignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), signerKeyPair.Private);
			Asn1SignatureFactory attackerSignatureFactory = new Asn1SignatureFactory(PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(), attackerSigningKeyPair.Private);
			X509V3CertificateGenerator certGen = new X509V3CertificateGenerator();
			certGen.SetIssuerDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Test Signer"));
			certGen.SetNotBefore(DateTime.UtcNow);
			certGen.SetNotAfter(DateTime.UtcNow.AddHours(1));
			BigInteger serialBase = new BigInteger(128, random);

			certGen.SetPublicKey(rsaKeyPair1.Public);
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Test 1"));
			certGen.SetSerialNumber(serialBase.Add(BigInteger.ValueOf(1)));
			rsaCert1 = certGen.Generate(signatureFactory);
			certGen.SetPublicKey(rsaKeyPair2.Public);
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Test 2"));
			certGen.SetSerialNumber(serialBase.Add(BigInteger.ValueOf(2)));
			rsaCert2 = certGen.Generate(signatureFactory);
			certGen.SetPublicKey(ecKeyPair1.Public);
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Test 3"));
			certGen.SetSerialNumber(serialBase.Add(BigInteger.ValueOf(3)));
			ecCert1 = certGen.Generate(signatureFactory);
			certGen.SetPublicKey(ecKeyPair2.Public);
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Test 4"));
			certGen.SetSerialNumber(serialBase.Add(BigInteger.ValueOf(4)));
			ecCert2 = certGen.Generate(signatureFactory);

			certGen.SetPublicKey(rsaKeyPairAttacker.Public);
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Attacker 1"));
			certGen.SetSerialNumber(serialBase.Add(BigInteger.ValueOf(5)));
			rsaCertAttacker = certGen.Generate(attackerSignatureFactory);
			certGen.SetPublicKey(ecKeyPairAttacker.Public);
			certGen.SetSubjectDN(new X509Name("o=SGL,ou=Utility,ou=Tests,cn=Attacker 2"));
			certGen.SetSerialNumber(serialBase.Add(BigInteger.ValueOf(6)));
			ecCertAttacker = certGen.Generate(attackerSignatureFactory);

			privKeyPassword = SecretGenerator.Instance.GenerateSecret(16).ToCharArray();

			signerPubPem = WritePem(signerKeyPair.Public);
			signerPrivPem = WritePem(signerKeyPair.Private, privKeyPassword);

			rsaPub1Pem = WritePem(rsaKeyPair1.Public);
			rsaCert1Pem = WritePem(rsaCert1);
			rsaPriv1Pem = WritePem(rsaKeyPair1.Private, privKeyPassword);

			rsaPub2Pem = WritePem(rsaKeyPair2.Public);
			rsaCert2Pem = WritePem(rsaCert2);
			rsaPriv2Pem = WritePem(rsaKeyPair2.Private, privKeyPassword);

			ecPub1Pem = WritePem(ecKeyPair1.Public);
			ecCert1Pem = WritePem(ecCert1);
			ecPriv1Pem = WritePem(ecKeyPair1.Private, privKeyPassword);

			ecPub2Pem = WritePem(ecKeyPair2.Public);
			ecCert2Pem = WritePem(ecCert2);
			ecPriv2Pem = WritePem(ecKeyPair2.Private, privKeyPassword);

			rsaPubAttackerPem = WritePem(rsaKeyPairAttacker.Public);
			rsaCertAttackerPem = WritePem(rsaCertAttacker);
			rsaPrivAttackerPem = WritePem(rsaKeyPairAttacker.Private, privKeyPassword);

			ecPubAttackerPem = WritePem(ecKeyPairAttacker.Public);
			ecCertAttackerPem = WritePem(ecCertAttacker);
			ecPrivAttackerPem = WritePem(ecKeyPairAttacker.Private, privKeyPassword);
		}

		private byte[] WritePem(object obj) {
			using MemoryStream ms = new MemoryStream();
			using StreamWriter writer = new StreamWriter(ms);
			PemWriter pemWriter = new PemWriter(writer);
			pemWriter.WriteObject(obj);
			writer.Flush();
			return ms.ToArray();
		}

		private byte[] WritePem(object obj, char[] password) {
			using MemoryStream ms = new MemoryStream();
			using StreamWriter writer = new StreamWriter(ms);
			PemWriter pemWriter = new PemWriter(writer);
			pemWriter.WriteObject(obj, "AES-256-CBC", password, random);
			writer.Flush();
			return ms.ToArray();
		}

		public SecureRandom Random => random;

		public AsymmetricCipherKeyPair SignerKeyPair => signerKeyPair;

		public AsymmetricCipherKeyPair RsaKeyPair1 => rsaKeyPair1;

		public AsymmetricCipherKeyPair RsaKeyPair2 => rsaKeyPair2;

		public AsymmetricCipherKeyPair EcKeyPair1 => ecKeyPair1;

		public AsymmetricCipherKeyPair EcKeyPair2 => ecKeyPair2;

		public X509Certificate RsaCert1 => rsaCert1;

		public X509Certificate RsaCert2 => rsaCert2;

		public X509Certificate EcCert1 => ecCert1;

		public X509Certificate EcCert2 => ecCert2;

		public byte[] SignerPubPem => signerPubPem;

		public byte[] RsaPub1Pem => rsaPub1Pem;

		public byte[] RsaPub2Pem => rsaPub2Pem;

		public byte[] EcPub1Pem => ecPub1Pem;

		public byte[] EcPub2Pem => ecPub2Pem;

		public char[] PrivKeyPassword => privKeyPassword;

		public byte[] SignerPrivPem => signerPrivPem;

		public byte[] RsaPriv1Pem => rsaPriv1Pem;

		public byte[] RsaPriv2Pem => rsaPriv2Pem;

		public byte[] EcPriv1Pem => ecPriv1Pem;

		public byte[] EcPriv2Pem => ecPriv2Pem;

		public byte[] RsaCert1Pem => rsaCert1Pem;

		public byte[] RsaCert2Pem => rsaCert2Pem;

		public byte[] EcCert1Pem => ecCert1Pem;

		public byte[] EcCert2Pem => ecCert2Pem;

		public byte[] RsaPubAttackerPem => rsaPubAttackerPem;

		public byte[] EcPubAttackerPem => ecPubAttackerPem;

		public byte[] RsaPrivAttackerPem => rsaPrivAttackerPem;

		public byte[] EcPrivAttackerPem => ecPrivAttackerPem;

		public byte[] RsaCertAttackerPem => rsaCertAttackerPem;

		public byte[] EcCertAttackerPem => ecCertAttackerPem;

		public AsymmetricCipherKeyPair RsaKeyPairAttacker => rsaKeyPairAttacker;

		public AsymmetricCipherKeyPair EcKeyPairAttacker => ecKeyPairAttacker;

		public AsymmetricCipherKeyPair AttackerSigningKeyPair => attackerSigningKeyPair;

		public X509Certificate RsaCertAttacker => rsaCertAttacker;

		public X509Certificate EcCertAttacker => ecCertAttacker;
	}
}
