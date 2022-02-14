using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.IO;

namespace SGL.Utilities.Crypto {
	public class DataEncryptor {
		private List<byte[]> ivs;
		private byte[] dataKey;

		public DataEncryptor(SecureRandom random, int numberOfStreams = 1) {
			dataKey = new byte[32];
			random.NextBytes(dataKey);
			ivs = Enumerable.Range(0, numberOfStreams).Select(_ => {
				var iv = new byte[7];
				random.NextBytes(iv);
				return iv;
			}).ToList();
		}

		public CipherStream OpenEncryptionWriteStream(Stream outputStream, int streamIndex) {
			var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
			var keyParams = new ParametersWithIV(new KeyParameter(dataKey), ivs[streamIndex]);
			cipher.Init(forEncryption: true, keyParams);
			return new CipherStream(outputStream, null, cipher);
		}

		public EncryptionInfo GenerateEncryptionInfo(KeyEncryptor keyEncryptor) {
			EncryptionInfo result = new EncryptionInfo();
			result.DataMode = DataEncryptionMode.AES_256_CCM;
			result.IVs = ivs;
			(result.DataKeys, result.SenderPublicKey) = keyEncryptor.EncryptDataKey(dataKey);
			return result;
		}
	}
}
