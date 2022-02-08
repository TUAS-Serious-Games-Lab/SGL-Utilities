using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.IO;

namespace SGL.Utilities.Crypto {
	public class DataEncryptor {
		private SecureRandom random;
		private byte[] iv;
		private byte[] dataKey;

		public DataEncryptor(SecureRandom random) {
			this.random = random;
			iv = new byte[7];
			random.NextBytes(iv);
			dataKey = new byte[32];
			random.NextBytes(dataKey);
		}

		public CipherStream OpenEncryptionWriteStream(Stream outputStream) {
			var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
			var keyParams = new ParametersWithIV(new KeyParameter(dataKey), iv);
			cipher.Init(forEncryption: true, keyParams);
			return new CipherStream(outputStream, null, cipher);
		}

		public EncryptionInfo GenerateEncryptionInfo(KeyEncryptor keyEncryptor) {
			EncryptionInfo result = new EncryptionInfo();
			result.DataMode = DataEncryptionMode.AES_256_CCM;
			result.IV = iv;
			(result.DataKeys, result.SenderPublicKey) = keyEncryptor.EncryptDataKey(dataKey);
			return result;
		}
	}
}
