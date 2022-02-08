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
			using var encryptedContent1 = new MemoryStream();
			EncryptionInfo metadata1;

			var keyEncryptor = new KeyEncryptor(certStore.ListKnownKeyIdsAndPublicKeys().ToList(), fixture.Random);
			{
				var dataEncryptor = new DataEncryptor(fixture.Random);
				using var clearInputStream1 = new MemoryStream(clearTextInput1);
				using var encStream = dataEncryptor.OpenEncryptionWriteStream(encryptedContent1);
				await clearInputStream1.CopyToAsync(encStream);
				metadata1 = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
			}

			Assert.Equal(DataEncryptionMode.AES_256_CCM, metadata1.DataMode);
			Assert.Equal(2, metadata1.DataKeys.Values.Count);
			Assert.All(metadata1.DataKeys.Values, dki => Assert.InRange(dki.EncryptedKey.Length, 32, 1024));

			var privKeyStore = new PrivateKeyStore();
			using var privKeyPemReader = new StreamReader(new MemoryStream(recipientPrivateKeyPem));
			privKeyStore.LoadKeyPair(privKeyPemReader, fixture.PrivKeyPassword);
			using var clearOutputStream1 = new MemoryStream();
			var keyDecryptor = new KeyDecryptor(privKeyStore.KeyPair);
			if (!expectMissingDataKey) {
				{
					using var encryptedContentReadStream1 = new MemoryStream(encryptedContent1.ToArray());
					var dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata1, keyDecryptor);
					Assert.NotNull(dataDecryptor);
					if (dataDecryptor == null) throw new Exception("No matching encrypted data key found.");
					using var decStream = dataDecryptor.OpenDecryptionReadStream(encryptedContentReadStream1);
					await decStream.CopyToAsync(clearOutputStream1);
				}

				Assert.Equal(clearTextInput1, clearOutputStream1.ToArray());
			}
			else {
				var dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata1, keyDecryptor);
				Assert.Null(dataDecryptor);
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
			{
				var dataEncryptor = new DataEncryptor(fixture.Random);
				using var clearInputStream1 = new MemoryStream(clearTextInput1);
				using var encStream = dataEncryptor.OpenEncryptionWriteStream(encryptedContent1);
				await clearInputStream1.CopyToAsync(encStream);
				metadata1 = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
			}

			Assert.Equal(DataEncryptionMode.AES_256_CCM, metadata1.DataMode);
			Assert.Equal(2, metadata1.DataKeys.Values.Count);
			Assert.All(metadata1.DataKeys.Values, dki => Assert.InRange(dki.EncryptedKey.Length, 32, 1024));

			var privKeyStore = new PrivateKeyStore();
			using var privKeyPemReader = new StreamReader(new MemoryStream(attackerPrivateKeyPem));
			privKeyStore.LoadKeyPair(privKeyPemReader, fixture.PrivKeyPassword);
			using var clearOutputStream1 = new MemoryStream();
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
			{
				var dataEncryptor = new DataEncryptor(fixture.Random);
				using var clearInputStream1 = new MemoryStream(clearTextInput1);
				using var encStream = dataEncryptor.OpenEncryptionWriteStream(encryptedContent1);
				await clearInputStream1.CopyToAsync(encStream);
				metadata1 = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
			}

			Assert.Equal(DataEncryptionMode.AES_256_CCM, metadata1.DataMode);
			Assert.Equal(2, metadata1.DataKeys.Values.Count);
			Assert.All(metadata1.DataKeys.Values, dki => Assert.InRange(dki.EncryptedKey.Length, 32, 1024));

			var privKeyStore = new PrivateKeyStore();
			using var privKeyPemReader = new StreamReader(new MemoryStream(attackerPrivateKeyPem));
			privKeyStore.LoadKeyPair(privKeyPemReader, fixture.PrivKeyPassword);
			using var clearOutputStream1 = new MemoryStream();
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
