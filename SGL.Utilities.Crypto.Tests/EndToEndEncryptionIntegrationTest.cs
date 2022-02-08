using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
			using var encStream = dataEncryptor.OpenEncryptionWriteStream(encryptedContentStream);
			await clearInputStream.CopyToAsync(encStream);
			return dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
		}

		private async Task<MemoryStream> decrypt(MemoryStream encryptedContent, EncryptionInfo metadata, KeyDecryptor keyDecryptor) {
			var clearOutputStream = new MemoryStream();
			using var encryptedContentReadStream = new MemoryStream(encryptedContent.ToArray());
			var dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata, keyDecryptor);
			Assert.NotNull(dataDecryptor);
			if (dataDecryptor == null) throw new Exception("No matching encrypted data key found.");
			using var decStream = dataDecryptor.OpenDecryptionReadStream(encryptedContentReadStream);
			await decStream.CopyToAsync(clearOutputStream);
			return clearOutputStream;
		}

		private static void assertMetadata(EncryptionInfo metadata1, int expectedDataKeyCount) {
			Assert.Equal(DataEncryptionMode.AES_256_CCM, metadata1.DataMode);
			Assert.Equal(expectedDataKeyCount, metadata1.DataKeys.Values.Count);
			Assert.All(metadata1.DataKeys.Values, dki => Assert.InRange(dki.EncryptedKey.Length, 32, 1024));
		}

		private async Task TestE2EELoadingAndCorrectRoundTrip(byte[] recipientPrivateKeyPem, bool expectMissingDataKey) {
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			validator.LoadPublicKeysFromReader(signerPemReader, "TestSigner.pem");
			Assert.True(validator.CheckCertificate(fixture.RsaCert1));
			Assert.True(validator.CheckCertificate(fixture.RsaCert2));
			Assert.True(validator.CheckCertificate(fixture.EcCert1));
			Assert.True(validator.CheckCertificate(fixture.EcCert2));

			var certStore = new CertificateStore(loggerFactory.CreateLogger<CertificateStore>(), validator);
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(2, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.RsaKeyPair1.Public)));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.EcKeyPair1.Public)));

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
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			validator.LoadPublicKeysFromReader(signerPemReader, "TestSigner.pem");
			Assert.False(validator.CheckCertificate(fixture.RsaCertAttacker));
			Assert.False(validator.CheckCertificate(fixture.EcCertAttacker));

			var certStore = new CertificateStore(loggerFactory.CreateLogger<CertificateStore>(), validator);
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).Concat(fixture.RsaCertAttackerPem).Concat(fixture.EcCertAttackerPem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(2, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.RsaKeyPair1.Public)));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.EcKeyPair1.Public)));
			Assert.Null(certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.RsaKeyPairAttacker.Public)));
			Assert.Null(certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.EcKeyPairAttacker.Public)));

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
			KeyOnlyTrustValidator validator = new KeyOnlyTrustValidator(loggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			using var signerPemReader = new StreamReader(new MemoryStream(fixture.SignerPubPem));
			validator.LoadPublicKeysFromReader(signerPemReader, "TestSigner.pem");
			Assert.False(validator.CheckCertificate(fixture.RsaCertAttacker));
			Assert.False(validator.CheckCertificate(fixture.EcCertAttacker));

			var certStore = new CertificateStore(loggerFactory.CreateLogger<CertificateStore>(), validator);
			using var certsPemReader = new StreamReader(new MemoryStream(fixture.RsaCert1Pem.Concat(fixture.EcCert1Pem).Concat(fixture.RsaCertAttackerPem).Concat(fixture.EcCertAttackerPem).ToArray()));
			certStore.LoadCertificatesFromReader(certsPemReader, "Recipients.pem");
			Assert.Equal(2, certStore.ListKnownCertificates().Count());
			Assert.Equal(fixture.RsaCert1, certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.RsaKeyPair1.Public)));
			Assert.Equal(fixture.EcCert1, certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.EcKeyPair1.Public)));
			Assert.Null(certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.RsaKeyPairAttacker.Public)));
			Assert.Null(certStore.GetCertificateByKeyId(KeyId.CalculateId(fixture.EcKeyPairAttacker.Public)));

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

			Assert.Throws<InvalidCipherTextException>(() => keyDecryptor.DecryptKey(metadata1.DataKeys[impersonatedRecipient], metadata1.SenderPublicKey));
		}

		[Fact]
		public async Task DataKeyCantBeDecryptedByOtherRsaKeyPairs() {
			await DataKeyCantBeDecryptedByOtherKeyPairs(KeyId.CalculateId(fixture.RsaKeyPair1.Public), fixture.RsaPrivAttackerPem);
		}

		[Fact]
		public async Task DataKeyCantBeDecryptedByOtherEcKeyPairs() {
			await DataKeyCantBeDecryptedByOtherKeyPairs(KeyId.CalculateId(fixture.EcKeyPair1.Public), fixture.EcPrivAttackerPem);
		}
	}
}
