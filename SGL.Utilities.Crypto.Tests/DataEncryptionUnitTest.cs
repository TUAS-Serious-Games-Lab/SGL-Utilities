using Org.BouncyCastle.Crypto;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class DataEncryptionUnitTest {
		// Doesn't actually encrypt the data key, bug just stores it, to test data encryption in isolation.
		private class DummyKeyEncryptorDecryptor : IKeyEncryptor, IKeyDecryptor {
			private readonly KeyId dummyKeyId = KeyId.Parse("01:00000000:00000000:00000000:00000000:00000000:00000000:00000000:00000000");

			public byte[] DecryptKey(DataKeyInfo dataKeyInfo, byte[]? sharedSenderPublicKey) {
				return dataKeyInfo.EncryptedKey;
			}

			public byte[]? DecryptKey(EncryptionInfo encryptionInfo) {
				return encryptionInfo.DataKeys[dummyKeyId].EncryptedKey;
			}

			public (Dictionary<KeyId, DataKeyInfo> dataKeys, byte[]? senderPubKey) EncryptDataKey(byte[] dataKey) {
				return (new Dictionary<KeyId, DataKeyInfo> { [dummyKeyId] = new DataKeyInfo() { EncryptedKey = dataKey, Mode = KeyEncryptionMode.RSA_PKCS1, SenderPublicKey = null } }, null);
			}
		}

		private readonly RandomGenerator random = new RandomGenerator();

		[Fact]
		public async Task DataEncryptorAndDecryptorCorrectlyRoundTripDataEncryptionForSingleStream() {
			byte[] testData = new byte[1 << 20];
			random.NextBytes(testData);
			using var encMemStream = new MemoryStream();

			var encryptor = new DataEncryptor(random);
			{
				using var encStream = encryptor.OpenEncryptionWriteStream(encMemStream, 0);
				await encStream.WriteAsync(testData);
			}
			var dummyKeyCryptor = new DummyKeyEncryptorDecryptor();
			var encInfo = encryptor.GenerateEncryptionInfo(dummyKeyCryptor);
			using var decMemStream = new MemoryStream(encMemStream.ToArray());
			byte[] decryptedData;
			var decryptor = DataDecryptor.FromEncryptionInfo(encInfo, dummyKeyCryptor);
			Assert.NotNull(decryptor);
			{
				using var decStream = decryptor!.OpenDecryptionReadStream(decMemStream, 0);
				using var tempMemStream = new MemoryStream();
				await decStream.CopyToAsync(tempMemStream);
				decryptedData = tempMemStream.ToArray();
			}
			Assert.Equal(testData, decryptedData);
		}

		[Fact]
		public async Task DataEncryptorAndDecryptorCorrectlyRoundTripDataEncryptionForMultipleStreams() {
			var testData = Enumerable.Range(0, 4).Select(_ => {
				var d = new byte[1 << 20];
				random.NextBytes(d);
				return d;
			}).ToArray();
			var encMemStreams = testData.Select(_ => new MemoryStream()).ToArray();

			var encryptor = new DataEncryptor(random, testData.Length);
			for (int i = 0; i < testData.Length; ++i) {
				using var encStream = encryptor.OpenEncryptionWriteStream(encMemStreams[i], i);
				await encStream.WriteAsync(testData[i]);
			}

			var dummyKeyCryptor = new DummyKeyEncryptorDecryptor();
			var encInfo = encryptor.GenerateEncryptionInfo(dummyKeyCryptor);

			var decMemStreams = encMemStreams.Select(es => new MemoryStream(es.ToArray())).ToArray();

			var decryptor = DataDecryptor.FromEncryptionInfo(encInfo, dummyKeyCryptor);
			Assert.NotNull(decryptor);
			Assert.Equal(testData.Length, decryptor.StreamCount);
			byte[][] decryptedData = new byte[decryptor!.StreamCount][];
			for (int i = 0; i < decryptor.StreamCount; ++i) {
				using var decStream = decryptor.OpenDecryptionReadStream(decMemStreams[i], i);
				using var tempMemStream = new MemoryStream();
				await decStream.CopyToAsync(tempMemStream);
				decryptedData[i] = tempMemStream.ToArray();
			}
			Assert.All(Enumerable.Range(0, testData.Length), i => Assert.Equal(testData[i], decryptedData[i]));
			for (int i = 0; i < encInfo.IVs.Count; ++i) {
				for (int j = 0; j < encInfo.IVs.Count; ++j) {
					if (i == j) continue;
					Assert.NotEqual(encInfo.IVs[i], encInfo.IVs[j]);
				}
			}
		}

		[Fact]
		public async Task DataEncryptorStreamsAreNotInterchangeable() {
			var testData = Enumerable.Range(0, 4).Select(_ => {
				var d = new byte[1 << 20];
				random.NextBytes(d);
				return d;
			}).ToArray();
			var encMemStreams = testData.Select(_ => new MemoryStream()).ToArray();

			var encryptor = new DataEncryptor(random, testData.Length);
			for (int i = 0; i < testData.Length; ++i) {
				using var encStream = encryptor.OpenEncryptionWriteStream(encMemStreams[i], i);
				await encStream.WriteAsync(testData[i]);
			}

			var dummyKeyCryptor = new DummyKeyEncryptorDecryptor();
			var encInfo = encryptor.GenerateEncryptionInfo(dummyKeyCryptor);

			var decMemStreams = encMemStreams.Select(es => new MemoryStream(es.ToArray())).ToArray();

			var decryptor = DataDecryptor.FromEncryptionInfo(encInfo, dummyKeyCryptor);
			Assert.NotNull(decryptor);
			Assert.Equal(testData.Length, decryptor.StreamCount);
			byte[][] decryptedData = new byte[decryptor!.StreamCount][];
			for (int i = 0; i < decryptor.StreamCount; ++i) {
				using var decStream = decryptor.OpenDecryptionReadStream(decMemStreams[i], decryptor.StreamCount - i - 1);
				using var tempMemStream = new MemoryStream();
				await Assert.ThrowsAnyAsync<InvalidCipherTextException>(() => decStream.CopyToAsync(tempMemStream));
			}
		}
	}
}
