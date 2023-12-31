﻿using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Tests {
	public class E2ECryptoFixture {
		private readonly RandomGenerator random;
		private readonly KeyPair signerKeyPair;
		private readonly KeyPair rsaKeyPair1;
		private readonly KeyPair rsaKeyPair2;
		private readonly KeyPair ecKeyPair1;
		private readonly KeyPair ecKeyPair2;
		private readonly KeyPair ecKeyPair3;
		private readonly KeyPair ecKeyPair4;

		private readonly KeyPair rsaKeyPairAttacker;
		private readonly KeyPair ecKeyPairAttacker;

		private readonly Certificate signerCert;
		private readonly Certificate rsaCert1;
		private readonly Certificate rsaCert2;
		private readonly Certificate ecCert1;
		private readonly Certificate ecCert2;
		private readonly Certificate ecCert3;
		private readonly Certificate ecCert4;

		private readonly KeyPair attackerSigningKeyPair;
		private readonly Certificate rsaCertAttacker;
		private readonly Certificate ecCertAttacker;

		private readonly byte[] signerPubPem;
		private readonly byte[] rsaPub1Pem;
		private readonly byte[] rsaPub2Pem;
		private readonly byte[] ecPub1Pem;
		private readonly byte[] ecPub2Pem;
		private readonly byte[] ecPub3Pem;
		private readonly byte[] ecPub4Pem;

		private readonly byte[] rsaPubAttackerPem;
		private readonly byte[] ecPubAttackerPem;

		private readonly char[] privKeyPassword;
		private readonly byte[] signerPrivPem;
		private readonly byte[] rsaPriv1Pem;
		private readonly byte[] rsaPriv2Pem;
		private readonly byte[] ecPriv1Pem;
		private readonly byte[] ecPriv2Pem;
		private readonly byte[] ecPriv3Pem;
		private readonly byte[] ecPriv4Pem;

		private readonly byte[] rsaPrivAttackerPem;
		private readonly byte[] ecPrivAttackerPem;

		private readonly byte[] signerCertPem;
		private readonly byte[] rsaCert1Pem;
		private readonly byte[] rsaCert2Pem;
		private readonly byte[] ecCert1Pem;
		private readonly byte[] ecCert2Pem;
		private readonly byte[] ecCert3Pem;
		private readonly byte[] ecCert4Pem;

		private readonly byte[] rsaCertAttackerPem;
		private readonly byte[] ecCertAttackerPem;

		private Task<KeyPair> GenerateRsaKeyPairAsync(int length) {
			var rnd = random.DeriveGenerator(1024);
			return Task.Run(() => KeyPair.GenerateRSA(rnd, length));
		}
		private Task<KeyPair> GenerateEcKeyPairAsync(int length) {
			var rnd = random.DeriveGenerator(1024);
			return Task.Run(() => KeyPair.GenerateEllipticCurves(rnd, length));
		}
		private Task<KeyPair> GenerateEcKeyPairAsync(KeyGenerationParameters ecKeyParams) {
			return Task.Run(() => {
				ECKeyPairGenerator ecGen = new ECKeyPairGenerator();
				ecGen.Init(ecKeyParams);
				return new KeyPair(ecGen.GenerateKeyPair());
			});
		}

		public E2ECryptoFixture() {
			random = new RandomGenerator();

			var rsaGeneratorTasks = Enumerable.Range(0, 5).Select(_ => GenerateRsaKeyPairAsync(4096)).ToArray();
			var ecGeneratorTasks = Enumerable.Range(0, 3).Select(_ => GenerateEcKeyPairAsync(521))
				// one key pair with a non-matching key length -> ineligible for shared message key
				.Append(GenerateEcKeyPairAsync(384))
				// one key pair with a explicit parameters -> ineligible for shared message key
				.Append(GenerateEcKeyPairAsync(new ECKeyGenerationParameters(new ECDomainParameters(ECNamedCurveTable.GetByName("secp521r1")), random.DeriveGenerator(1024).wrapped))).ToArray();

			Task.WaitAll(rsaGeneratorTasks.Concat(ecGeneratorTasks).ToArray());

			signerKeyPair = rsaGeneratorTasks.ElementAt(0).Result;
			attackerSigningKeyPair = rsaGeneratorTasks.ElementAt(1).Result;
			rsaKeyPair1 = rsaGeneratorTasks.ElementAt(2).Result;
			rsaKeyPair2 = rsaGeneratorTasks.ElementAt(3).Result;
			rsaKeyPairAttacker = rsaGeneratorTasks.ElementAt(4).Result;
			ecKeyPair1 = ecGeneratorTasks.ElementAt(0).Result;
			ecKeyPair2 = ecGeneratorTasks.ElementAt(1).Result;
			ecKeyPairAttacker = ecGeneratorTasks.ElementAt(2).Result;
			ecKeyPair3 = ecGeneratorTasks.ElementAt(3).Result;
			ecKeyPair4 = ecGeneratorTasks.ElementAt(4).Result;

			BigInteger serialBase = new BigInteger(128, random.wrapped);

			var issuerDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test Signer") });
			signerCert = Certificate.Generate(issuerDN, signerKeyPair.Private, issuerDN, signerKeyPair.Public, TimeSpan.FromHours(1), serialBase.ToByteArray(), generateSubjectKeyIdentifier: true, keyUsages: KeyUsages.KeyCertSign, caConstraint: (true, 0));

			var subj1DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 1") });
			var subj2DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 2") });
			var subj3DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 3") });
			var subj4DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 4") });
			var subj5DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 5") });
			var subj6DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 6") });
			rsaCert1 = Certificate.Generate(issuerDN, signerKeyPair.Private, subj1DN, rsaKeyPair1.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(1)).ToByteArray());
			rsaCert2 = Certificate.Generate(issuerDN, signerKeyPair.Private, subj2DN, rsaKeyPair2.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(2)).ToByteArray());
			ecCert1 = Certificate.Generate(issuerDN, signerKeyPair.Private, subj3DN, ecKeyPair1.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(3)).ToByteArray());
			ecCert2 = Certificate.Generate(issuerDN, signerKeyPair.Private, subj4DN, ecKeyPair2.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(4)).ToByteArray(), authorityKeyIdentifier: signerCert.SubjectKeyIdentifier);
			ecCert3 = Certificate.Generate(issuerDN, signerKeyPair.Private, subj5DN, ecKeyPair3.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(5)).ToByteArray(), authorityKeyIdentifier: signerCert.SubjectKeyIdentifier);
			ecCert4 = Certificate.Generate(issuerDN, signerKeyPair.Private, subj6DN, ecKeyPair4.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(6)).ToByteArray(), authorityKeyIdentifier: signerCert.SubjectKeyIdentifier, generateSubjectKeyIdentifier: true);

			var subj7DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Attacker 1") });
			var subj8DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Attacker 2") });
			rsaCertAttacker = Certificate.Generate(issuerDN, attackerSigningKeyPair.Private, subj7DN, rsaKeyPairAttacker.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(7)).ToByteArray());
			ecCertAttacker = Certificate.Generate(issuerDN, attackerSigningKeyPair.Private, subj8DN, ecKeyPairAttacker.Public, TimeSpan.FromHours(1), serialBase.Add(BigInteger.ValueOf(8)).ToByteArray());

			privKeyPassword = SecretGenerator.Instance.GenerateSecret(16).ToCharArray();

			signerPubPem = WritePem(signerKeyPair.Public);
			signerCertPem = WritePem(signerCert);
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

			ecPub3Pem = WritePem(ecKeyPair3.Public);
			ecCert3Pem = WritePem(ecCert3);
			ecPriv3Pem = WritePem(ecKeyPair3.Private, privKeyPassword);

			ecPub4Pem = WritePem(ecKeyPair4.Public);
			ecCert4Pem = WritePem(ecCert4);
			ecPriv4Pem = WritePem(ecKeyPair4.Private, privKeyPassword);

			rsaPubAttackerPem = WritePem(rsaKeyPairAttacker.Public);
			rsaCertAttackerPem = WritePem(rsaCertAttacker);
			rsaPrivAttackerPem = WritePem(rsaKeyPairAttacker.Private, privKeyPassword);

			ecPubAttackerPem = WritePem(ecKeyPairAttacker.Public);
			ecCertAttackerPem = WritePem(ecCertAttacker);
			ecPrivAttackerPem = WritePem(ecKeyPairAttacker.Private, privKeyPassword);
		}

		private byte[] WritePem(Certificate cert) {
			using MemoryStream ms = new MemoryStream();
			using StreamWriter writer = new StreamWriter(ms);
			cert.StoreToPem(writer);
			writer.Flush();
			return ms.ToArray();
		}
		private byte[] WritePem(PublicKey pubKey) {
			using MemoryStream ms = new MemoryStream();
			using StreamWriter writer = new StreamWriter(ms);
			pubKey.StoreToPem(writer);
			writer.Flush();
			return ms.ToArray();
		}

		private byte[] WritePem(PrivateKey privKey, char[] password) {
			using MemoryStream ms = new MemoryStream();
			using StreamWriter writer = new StreamWriter(ms);
			privKey.StoreToPem(writer, PemEncryptionMode.AES_256_CBC, password, Random);
			writer.Flush();
			return ms.ToArray();
		}

		public RandomGenerator Random => random;

		public KeyPair SignerKeyPair => signerKeyPair;

		public KeyPair RsaKeyPair1 => rsaKeyPair1;

		public KeyPair RsaKeyPair2 => rsaKeyPair2;

		public KeyPair EcKeyPair1 => ecKeyPair1;

		public KeyPair EcKeyPair2 => ecKeyPair2;

		public Certificate RsaCert1 => rsaCert1;

		public Certificate RsaCert2 => rsaCert2;

		public Certificate EcCert1 => ecCert1;

		public Certificate EcCert2 => ecCert2;

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

		public KeyPair RsaKeyPairAttacker => rsaKeyPairAttacker;

		public KeyPair EcKeyPairAttacker => ecKeyPairAttacker;

		public KeyPair AttackerSigningKeyPair => attackerSigningKeyPair;

		public Certificate RsaCertAttacker => rsaCertAttacker;

		public Certificate EcCertAttacker => ecCertAttacker;

		public Certificate EcCert3 => ecCert3;

		public Certificate EcCert4 => ecCert4;

		public byte[] EcPub3Pem => ecPub3Pem;

		public byte[] EcPub4Pem => ecPub4Pem;

		public byte[] EcPriv3Pem => ecPriv3Pem;

		public byte[] EcPriv4Pem => ecPriv4Pem;

		public byte[] EcCert3Pem => ecCert3Pem;

		public byte[] EcCert4Pem => ecCert4Pem;

		public KeyPair EcKeyPair3 => ecKeyPair3;

		public KeyPair EcKeyPair4 => ecKeyPair4;

		public Certificate SignerCert => signerCert;

		public byte[] SignerCertPem => signerCertPem;
	}
}
