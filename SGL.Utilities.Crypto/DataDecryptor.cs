using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.IO;

namespace SGL.Utilities.Crypto {
	public class DataDecryptor {
		private byte[] iv;
		private byte[] dataKey;

		public DataDecryptor(DataEncryptionMode dataMode, byte[] iv, byte[] dataKey) {
			if (dataMode != DataEncryptionMode.AES_256_CCM) {
				throw new ArgumentException("Unsupported data encryption mode.");
			}
			this.iv = iv;
			this.dataKey = dataKey;
		}

		public CipherStream OpenDecryptionReader(Stream inputStream) {
			var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
			var keyParams = new ParametersWithIV(new KeyParameter(dataKey), iv);
			cipher.Init(forEncryption: false, keyParams);
			return new CipherStream(inputStream, cipher, null);
		}
	}
}
