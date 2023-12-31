﻿using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {
	public class EndToEndEncryptionIntegrationTest : IClassFixture<E2ECryptoFixture> {
		private readonly E2ECryptoFixture fixture;
		private ILoggerFactory loggerFactory;
		private ITestOutputHelper output;

		public EndToEndEncryptionIntegrationTest(E2ECryptoFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
		}

		private byte[] GenerateTestContent(int size) {
			byte[] content = new byte[size];
			fixture.Random.NextBytes(content);
			return content;
		}

		private async Task<EncryptionInfo> encrypt(byte[] clearTextInput, Stream encryptedContentStream, KeyEncryptor keyEncryptor) {
			var dataEncryptor = new DataEncryptor(fixture.Random);
			using var clearInputStream = new MemoryStream(clearTextInput);
			using var encStream = dataEncryptor.OpenEncryptionWriteStream(encryptedContentStream, 0);
			await clearInputStream.CopyToAsync(encStream);
			return dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
		}

		private async Task<MemoryStream> decrypt(MemoryStream encryptedContent, EncryptionInfo metadata, KeyDecryptor keyDecryptor) {
			var clearOutputStream = new MemoryStream();
			using var encryptedContentReadStream = new MemoryStream(encryptedContent.ToArray());
			var dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata, keyDecryptor);
			Assert.NotNull(dataDecryptor);
			if (dataDecryptor == null) throw new Exception("No matching encrypted data key found.");
			using var decStream = dataDecryptor.OpenDecryptionReadStream(encryptedContentReadStream, 0);
			await decStream.CopyToAsync(clearOutputStream);
			return clearOutputStream;
		}

		private static void assertMetadata(EncryptionInfo metadata1, int expectedDataKeyCount) {
			Assert.Equal(DataEncryptionMode.AES_256_CCM, metadata1.DataMode);
			Assert.Equal(expectedDataKeyCount, metadata1.DataKeys.Values.Count);
			Assert.All(metadata1.DataKeys.Values, dki => Assert.InRange(dki.EncryptedKey.Length, 32, 1024));
		}

		private async Task TestE2EELoadingAndCorrectRoundTrip(byte[] recipientPrivateKeyPem, bool expectMissingDataKey) {
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(signerPemReader, "TestSigner.pem", loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			Assert.True(validator.CheckCertificate(fixture.RsaCert1));
			Assert.True(validator.CheckCertificate(fixture.RsaCert2));
			Assert.True(validator.CheckCertificate(fixture.EcCert1));
			Assert.True(validator.CheckCertificate(fixture.EcCert2));

			var certStore = new CertificateStore(validator, loggerFactory.CreateLogger<CertificateStore>());
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(2, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(fixture.RsaKeyPair1.Public.CalculateId()));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(fixture.EcKeyPair1.Public.CalculateId()));

			var clearTextInput1 = GenerateTestContent(1 << 20);
			var clearTextInput2 = GenerateTestContent(1 << 20);
			var clearTextInput3 = GenerateTestContent(1 << 20);
			using var encryptedContent1 = new MemoryStream();
			using var encryptedContent2 = new MemoryStream();
			using var encryptedContent3 = new MemoryStream();
			EncryptionInfo metadata1;
			EncryptionInfo metadata2;
			EncryptionInfo metadata3;

			var keyEncryptor = new KeyEncryptor(certStore.ListKnownKeyIdsAndPublicKeys().ToList(), fixture.Random);
			metadata1 = await encrypt(clearTextInput1, encryptedContent1, keyEncryptor);
			metadata2 = await encrypt(clearTextInput2, encryptedContent2, keyEncryptor);
			metadata3 = await encrypt(clearTextInput3, encryptedContent3, keyEncryptor);

			assertMetadata(metadata1, 2);
			assertMetadata(metadata2, 2);
			assertMetadata(metadata3, 2);

			var privKeyStore = new PrivateKeyStore();
			using var privKeyPemReader = new StreamReader(new MemoryStream(recipientPrivateKeyPem));
			privKeyStore.LoadKeyPair(privKeyPemReader, fixture.PrivKeyPassword);
			var keyDecryptor = new KeyDecryptor(privKeyStore.KeyPair);
			if (!expectMissingDataKey) {
				using var clearOutputStream1 = await decrypt(encryptedContent1, metadata1, keyDecryptor);
				using var clearOutputStream2 = await decrypt(encryptedContent2, metadata2, keyDecryptor);
				using var clearOutputStream3 = await decrypt(encryptedContent3, metadata3, keyDecryptor);

				Assert.Equal(clearTextInput1, clearOutputStream1.ToArray());
				Assert.Equal(clearTextInput2, clearOutputStream2.ToArray());
				Assert.Equal(clearTextInput3, clearOutputStream3.ToArray());
			}
			else {
				Assert.Null(DataDecryptor.FromEncryptionInfo(metadata1, keyDecryptor));
				Assert.Null(DataDecryptor.FromEncryptionInfo(metadata2, keyDecryptor));
				Assert.Null(DataDecryptor.FromEncryptionInfo(metadata3, keyDecryptor));
			}
		}

		[Fact]
		public async Task E2EEWithRsaDeliversCorrectContent() {
			await TestE2EELoadingAndCorrectRoundTrip(fixture.RsaPriv1Pem, expectMissingDataKey: false);
		}
		[Fact]
		public async Task E2EEWithEcdhAesDeliversCorrectContent() {
			await TestE2EELoadingAndCorrectRoundTrip(fixture.EcPriv1Pem, expectMissingDataKey: false);
		}
		[Fact]
		public async Task UnauthorizedRsaRecipientHasNoDataKey() {
			await TestE2EELoadingAndCorrectRoundTrip(fixture.RsaPriv2Pem, expectMissingDataKey: true);
		}
		[Fact]
		public async Task UnauthorizedEcRecipientHasNoDataKey() {
			await TestE2EELoadingAndCorrectRoundTrip(fixture.EcPriv2Pem, expectMissingDataKey: true);
		}

		private async Task ForgedCertificatesAreRejectedAndDontGetDataKey(byte[] attackerPrivateKeyPem) {
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(signerPemReader, "TestSigner.pem", loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			Assert.False(validator.CheckCertificate(fixture.RsaCertAttacker));
			Assert.False(validator.CheckCertificate(fixture.EcCertAttacker));

			var certStore = new CertificateStore(validator, loggerFactory.CreateLogger<CertificateStore>());
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).Concat(fixture.RsaCertAttackerPem).Concat(fixture.EcCertAttackerPem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(2, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(fixture.RsaKeyPair1.Public.CalculateId()));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(fixture.EcKeyPair1.Public.CalculateId()));
			Assert.Null(certStore.GetCertificateByKeyId(fixture.RsaKeyPairAttacker.Public.CalculateId()));
			Assert.Null(certStore.GetCertificateByKeyId(fixture.EcKeyPairAttacker.Public.CalculateId()));

			var clearTextInput1 = GenerateTestContent(1 << 20);
			using var encryptedContent1 = new MemoryStream();
			EncryptionInfo metadata1;

			var keyEncryptor = new KeyEncryptor(certStore.ListKnownKeyIdsAndPublicKeys().ToList(), fixture.Random);
			metadata1 = await encrypt(clearTextInput1, encryptedContent1, keyEncryptor);

			assertMetadata(metadata1, 2);

			var privKeyStore = new PrivateKeyStore();
			using var privKeyPemReader = new StreamReader(new MemoryStream(attackerPrivateKeyPem));
			privKeyStore.LoadKeyPair(privKeyPemReader, fixture.PrivKeyPassword);
			var keyDecryptor = new KeyDecryptor(privKeyStore.KeyPair);

			var dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata1, keyDecryptor);
			Assert.Null(dataDecryptor);
		}

		[Fact]
		public async Task ForgedRsaCertificatesAreRejectedAndDontGetDataKey() {
			await ForgedCertificatesAreRejectedAndDontGetDataKey(fixture.RsaPrivAttackerPem);
		}

		[Fact]
		public async Task ForgedEcCertificatesAreRejectedAndDontGetDataKey() {
			await ForgedCertificatesAreRejectedAndDontGetDataKey(fixture.EcPrivAttackerPem);
		}

		private async Task DataKeyCantBeDecryptedByOtherKeyPairs(KeyId impersonatedRecipient, byte[] attackerPrivateKeyPem) {
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(signerPemReader, "TestSigner.pem", loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			Assert.False(validator.CheckCertificate(fixture.RsaCertAttacker));
			Assert.False(validator.CheckCertificate(fixture.EcCertAttacker));

			var certStore = new CertificateStore(validator, loggerFactory.CreateLogger<CertificateStore>());
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).Concat(fixture.RsaCertAttackerPem).Concat(fixture.EcCertAttackerPem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(2, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(fixture.RsaKeyPair1.Public.CalculateId()));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(fixture.EcKeyPair1.Public.CalculateId()));
			Assert.Null(certStore.GetCertificateByKeyId(fixture.RsaKeyPairAttacker.Public.CalculateId()));
			Assert.Null(certStore.GetCertificateByKeyId(fixture.EcKeyPairAttacker.Public.CalculateId()));

			var clearTextInput1 = GenerateTestContent(1 << 20);
			using var encryptedContent1 = new MemoryStream();
			EncryptionInfo metadata1;

			var keyEncryptor = new KeyEncryptor(certStore.ListKnownKeyIdsAndPublicKeys().ToList(), fixture.Random);
			metadata1 = await encrypt(clearTextInput1, encryptedContent1, keyEncryptor);

			assertMetadata(metadata1, 2);

			var privKeyStore = new PrivateKeyStore();
			using var privKeyPemReader = new StreamReader(new MemoryStream(attackerPrivateKeyPem));
			privKeyStore.LoadKeyPair(privKeyPemReader, fixture.PrivKeyPassword);
			var keyDecryptor = new KeyDecryptor(privKeyStore.KeyPair);

			Assert.ThrowsAny<DecryptionException>(() => keyDecryptor.DecryptKey(metadata1.DataKeys[impersonatedRecipient], metadata1.MessagePublicKey));
		}

		[Fact]
		public async Task DataKeyCantBeDecryptedByOtherRsaKeyPairs() {
			await DataKeyCantBeDecryptedByOtherKeyPairs(fixture.RsaKeyPair1.Public.CalculateId(), fixture.RsaPrivAttackerPem);
		}

		[Fact]
		public async Task DataKeyCantBeDecryptedByOtherEcKeyPairs() {
			await DataKeyCantBeDecryptedByOtherKeyPairs(fixture.EcKeyPair1.Public.CalculateId(), fixture.EcPrivAttackerPem);
		}

		[Fact]
		public async Task EcSharedMessageKeyModeCorrectlyEncryptsForAllRecipients() {
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(signerPemReader, "TestSigner.pem", loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			Assert.True(validator.CheckCertificate(fixture.RsaCert1));
			Assert.True(validator.CheckCertificate(fixture.EcCert1));
			Assert.True(validator.CheckCertificate(fixture.EcCert2));
			Assert.True(validator.CheckCertificate(fixture.EcCert3));
			Assert.True(validator.CheckCertificate(fixture.EcCert4));

			var certStore = new CertificateStore(validator, loggerFactory.CreateLogger<CertificateStore>());
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).Concat(fixture.EcCert2Pem).Concat(fixture.EcCert3Pem).Concat(fixture.EcCert4Pem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(5, certStore.ListKnownCertificates().Count());
			var keyIdRsa = fixture.RsaKeyPair1.Public.CalculateId();
			var keyIdEc1 = fixture.EcKeyPair1.Public.CalculateId();
			var keyIdEc2 = fixture.EcKeyPair2.Public.CalculateId();
			var keyIdEc3 = fixture.EcKeyPair3.Public.CalculateId();
			var keyIdEc4 = fixture.EcKeyPair4.Public.CalculateId();
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(keyIdRsa));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(keyIdEc1));
			Assert.Equal(fixture.EcCert2, certStore.GetCertificateByKeyId(keyIdEc2));
			Assert.Equal(fixture.EcCert3, certStore.GetCertificateByKeyId(keyIdEc3));
			Assert.Equal(fixture.EcCert4, certStore.GetCertificateByKeyId(keyIdEc4));

			var clearTextInput1 = GenerateTestContent(1 << 20);
			using var encryptedContent1 = new MemoryStream();
			EncryptionInfo metadata1;

			var keyEncryptor = new KeyEncryptor(certStore.ListKnownKeyIdsAndPublicKeys().ToList(), fixture.Random, allowSharedMessageKeyPair: true);
			metadata1 = await encrypt(clearTextInput1, encryptedContent1, keyEncryptor);
			Assert.Equal(512, metadata1.DataKeys[keyIdRsa].EncryptedKey.Length);
			Assert.Equal(KeyEncryptionMode.RSA_PKCS1, metadata1.DataKeys[keyIdRsa].Mode);
			Assert.Null(metadata1.DataKeys[keyIdRsa].MessagePublicKey);

			Assert.NotNull(metadata1.MessagePublicKey);
			Assert.Null(metadata1.DataKeys[keyIdEc1].MessagePublicKey);
			Assert.Null(metadata1.DataKeys[keyIdEc2].MessagePublicKey);
			Assert.NotNull(metadata1.DataKeys[keyIdEc3].MessagePublicKey);
			Assert.NotNull(metadata1.DataKeys[keyIdEc4].MessagePublicKey);
			Assert.All(new[] { keyIdEc1, keyIdEc2, keyIdEc3, keyIdEc4 }, kId => Assert.Equal(KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM, metadata1.DataKeys[kId].Mode));
			Assert.NotEqual(metadata1.DataKeys[keyIdEc1].EncryptedKey, metadata1.DataKeys[keyIdEc2].EncryptedKey);

			var keyStoreRsa = new PrivateKeyStore();
			var keyStoreEc1 = new PrivateKeyStore();
			var keyStoreEc2 = new PrivateKeyStore();
			var keyStoreEc3 = new PrivateKeyStore();
			var keyStoreEc4 = new PrivateKeyStore();
			keyStoreRsa.LoadKeyPair(new StreamReader(new MemoryStream(fixture.RsaPriv1Pem)), fixture.PrivKeyPassword);
			keyStoreEc1.LoadKeyPair(new StreamReader(new MemoryStream(fixture.EcPriv1Pem)), fixture.PrivKeyPassword);
			keyStoreEc2.LoadKeyPair(new StreamReader(new MemoryStream(fixture.EcPriv2Pem)), fixture.PrivKeyPassword);
			keyStoreEc3.LoadKeyPair(new StreamReader(new MemoryStream(fixture.EcPriv3Pem)), fixture.PrivKeyPassword);
			keyStoreEc4.LoadKeyPair(new StreamReader(new MemoryStream(fixture.EcPriv4Pem)), fixture.PrivKeyPassword);

			var keyDecryptorRsa = new KeyDecryptor(keyStoreRsa.KeyPair);
			var keyDecryptorEc1 = new KeyDecryptor(keyStoreEc1.KeyPair);
			var keyDecryptorEc2 = new KeyDecryptor(keyStoreEc2.KeyPair);
			var keyDecryptorEc3 = new KeyDecryptor(keyStoreEc3.KeyPair);
			var keyDecryptorEc4 = new KeyDecryptor(keyStoreEc4.KeyPair);

			Assert.Equal(clearTextInput1, (await decrypt(encryptedContent1, metadata1, keyDecryptorRsa)).ToArray());
			Assert.Equal(clearTextInput1, (await decrypt(encryptedContent1, metadata1, keyDecryptorEc1)).ToArray());
			Assert.Equal(clearTextInput1, (await decrypt(encryptedContent1, metadata1, keyDecryptorEc2)).ToArray());
			Assert.Equal(clearTextInput1, (await decrypt(encryptedContent1, metadata1, keyDecryptorEc3)).ToArray());
			Assert.Equal(clearTextInput1, (await decrypt(encryptedContent1, metadata1, keyDecryptorEc4)).ToArray());
		}

		[Fact]
		public void SignerBasedValidationAcceptsProperlySignedCertificatesAndRejectsInvalidOnes() {
			using var signerCertPemReader = new StreamReader(new MemoryStream(fixture.SignerCertPem));
			CACertTrustValidator validator = new CACertTrustValidator(signerCertPemReader, "Signers.pem", ignoreValidityPeriod: false, loggerFactory.CreateLogger<CACertTrustValidator>(), loggerFactory.CreateLogger<CertificateStore>());
			Assert.True(validator.CheckCertificate(fixture.RsaCert1));
			Assert.True(validator.CheckCertificate(fixture.RsaCert2));
			Assert.True(validator.CheckCertificate(fixture.EcCert1));
			Assert.True(validator.CheckCertificate(fixture.EcCert2));
			Assert.True(validator.CheckCertificate(fixture.EcCert3));
			Assert.True(validator.CheckCertificate(fixture.EcCert4));
			Assert.False(validator.CheckCertificate(fixture.RsaCertAttacker));
			Assert.False(validator.CheckCertificate(fixture.EcCertAttacker));

			var certStore = new CertificateStore(validator, loggerFactory.CreateLogger<CertificateStore>());
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.RsaCert2Pem).Concat(fixture.EcCert1Pem).Concat(fixture.EcCert2Pem).Concat(fixture.EcCert3Pem).Concat(fixture.EcCert4Pem).Concat(fixture.RsaCertAttackerPem).Concat(fixture.EcCertAttackerPem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(6, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(fixture.RsaKeyPair1.Public.CalculateId()));
			Assert.Equal(fixture.RsaCert2, certStore.GetCertificateByKeyId(fixture.RsaKeyPair2.Public.CalculateId()));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(fixture.EcKeyPair1.Public.CalculateId()));
			Assert.Equal(fixture.EcCert2, certStore.GetCertificateByKeyId(fixture.EcKeyPair2.Public.CalculateId()));
			Assert.Equal(fixture.EcCert3, certStore.GetCertificateByKeyId(fixture.EcKeyPair3.Public.CalculateId()));
			Assert.Equal(fixture.EcCert4, certStore.GetCertificateByKeyId(fixture.EcKeyPair4.Public.CalculateId()));
			Assert.Equal(fixture.EcCert4, certStore.GetCertificateBySubjectKeyIdentifier(new KeyIdentifier(new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(fixture.EcKeyPair4.Public.wrapped)))));
			Assert.Null(certStore.GetCertificateByKeyId(fixture.RsaKeyPairAttacker.Public.CalculateId()));
			Assert.Null(certStore.GetCertificateByKeyId(fixture.EcKeyPairAttacker.Public.CalculateId()));
		}
	}
}
