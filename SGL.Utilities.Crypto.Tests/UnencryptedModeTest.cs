using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.TestUtilities.XUnit;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class UnencryptedModeTest {
		private readonly RandomGenerator random = new RandomGenerator();

		[Fact]
		public void UnencryptedModeUsingDirectMethodPassesDataThroughUnchanged() {
			var origData = random.GetBytes(128);
			var encryptor = new DataEncryptor(null!, 1, DataEncryptionMode.Unencrypted);
			var dataAfterEncryptor = encryptor.EncryptData(origData, 0);
			Assert.Equal(origData, dataAfterEncryptor);
			var encryptionInfo = encryptor.GenerateEncryptionInfo(null!);
			var decryptor = DataDecryptor.FromEncryptionInfo(encryptionInfo, null!);
			Assert.NotNull(decryptor);
			var dataAfterDecryptor = decryptor!.DecryptData(dataAfterEncryptor, 0);
			Assert.Equal(dataAfterEncryptor, dataAfterDecryptor);
		}

		[Fact]
		public async Task UnencryptedModeUsingReadStreamsPassesDataThroughUnchanged() {
			using var origData = new MemoryStream(random.GetBytes(128));
			var encryptor = new DataEncryptor(null!, 1, DataEncryptionMode.Unencrypted);
			using var dataAfterEncryptor = new MemoryStream();
			using var encStream = encryptor.OpenEncryptionReadStream(origData, 0, leaveOpen: true);
			await encStream.CopyToAsync(dataAfterEncryptor);
			origData.Position = 0;
			dataAfterEncryptor.Position = 0;
			StreamUtils.AssertEqualContent(origData, dataAfterEncryptor);
			var encryptionInfo = encryptor.GenerateEncryptionInfo(null!);
			var decryptor = DataDecryptor.FromEncryptionInfo(encryptionInfo, null!);
			Assert.NotNull(decryptor);
			dataAfterEncryptor.Position = 0;
			using var decStream = decryptor!.OpenDecryptionReadStream(dataAfterEncryptor, 0, leaveOpen: true);
			using var dataAfterDecryptor = new MemoryStream();
			await decStream.CopyToAsync(dataAfterDecryptor);
			dataAfterEncryptor.Position = 0;
			dataAfterDecryptor.Position = 0;
			StreamUtils.AssertEqualContent(dataAfterEncryptor, dataAfterDecryptor);
		}
		[Fact]
		public async Task UnencryptedModeUsingWriteStreamsPassesDataThroughUnchanged() {
			using var origData = new MemoryStream(random.GetBytes(128));
			var encryptor = new DataEncryptor(null!, 1, DataEncryptionMode.Unencrypted);
			using var dataAfterEncryptor = new MemoryStream();
			using var encStream = encryptor.OpenEncryptionWriteStream(dataAfterEncryptor, 0, leaveOpen: true);
			await origData.CopyToAsync(encStream);
			origData.Position = 0;
			dataAfterEncryptor.Position = 0;
			StreamUtils.AssertEqualContent(origData, dataAfterEncryptor);
			var encryptionInfo = encryptor.GenerateEncryptionInfo(null!);
			var decryptor = DataDecryptor.FromEncryptionInfo(encryptionInfo, null!);
			Assert.NotNull(decryptor);
			dataAfterEncryptor.Position = 0;
			using var dataAfterDecryptor = new MemoryStream();
			using var decStream = decryptor!.OpenDecryptionWriteStream(dataAfterDecryptor, 0, leaveOpen: true);
			await dataAfterEncryptor.CopyToAsync(decStream);
			dataAfterEncryptor.Position = 0;
			dataAfterDecryptor.Position = 0;
			StreamUtils.AssertEqualContent(dataAfterEncryptor, dataAfterDecryptor);
		}
	}
}
