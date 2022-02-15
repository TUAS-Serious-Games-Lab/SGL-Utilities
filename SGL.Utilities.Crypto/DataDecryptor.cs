using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto {
	/// <summary>
	/// Provides the functionality to decrypt the content of a data object using a data key obtained from the encrypted data object's metadata.
	/// Each data object requires a separate DataEncryptor object.
	/// The data key and the initialization vectors can either be specified directly using <see cref="DataDecryptor(DataEncryptionMode, List{byte[]}, byte[])"/> or can be obtained from an <see cref="EncryptionInfo"/> representing the key material for the data object using a <see cref="KeyDecryptor"/>.
	/// A data object can consist of multiple streams that are encrypted with the same data key, e.g. a message with attachments where the message text and the attachments each have their own stream.
	/// These streams are identified using an index within the data object and have their own initialization vector for each stream.
	/// </summary>
	public class DataDecryptor {
		private List<byte[]> ivs;
		private byte[] dataKey;

		/// <summary>
		/// Returns the number of streams in the data object that this DataDecryptor decrypts.
		/// </summary>
		public int StreamCount => ivs.Count;

		/// <summary>
		/// Constructs a DataDecryptor that uses the given data key, per-stream initialization vectors and encryption mode.
		/// </summary>
		/// <param name="dataMode">The encryption mode to use for the decryption. Currently, only <see cref="DataEncryptionMode.AES_256_CCM"/> is supported.</param>
		/// <param name="ivs">The initialization vectors of the data object's streams, on for each stream.</param>
		/// <param name="dataKey">The data key for the data object.</param>
		public DataDecryptor(DataEncryptionMode dataMode, List<byte[]> ivs, byte[] dataKey) {
			if (dataMode != DataEncryptionMode.AES_256_CCM) {
				throw new ArgumentException("Unsupported data encryption mode.");
			}
			this.ivs = ivs;
			this.dataKey = dataKey;
		}

		/// <summary>
		/// Opens a <see cref="CipherStream"/> backed by <paramref name="inputStream"/> using the data key of the decryptor and the initialization vector of the stream with the index given in <paramref name="streamIndex"/>.
		/// The <see cref="CipherStream"/> is setup to decrypt the data read through it.
		/// </summary>
		/// <param name="inputStream">The backing stream for the decrypting stream.</param>
		/// <param name="streamIndex">The logical index of the stream within the data object.</param>
		/// <returns>A stream that reads the encrypted data from <paramref name="inputStream"/> and decrypts data when the stream is read from.</returns>
		public CipherStream OpenDecryptionReadStream(Stream inputStream, int streamIndex) {
			var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
			var keyParams = new ParametersWithIV(new KeyParameter(dataKey), ivs[streamIndex]);
			cipher.Init(forEncryption: false, keyParams);
			return new CipherStream(inputStream, cipher, null);
		}

		/// <summary>
		/// Attempts to construct a DataDecryptor using the data key obtained from <paramref name="encryptionInfo"/> using <paramref name="keyDecryptor"/>.
		/// If <paramref name="keyDecryptor"/> can't obtain a data key, usually because there is no encrypted data key copy for the recipient key setup in <paramref name="keyDecryptor"/>, i.e. because the receiver is not an authorized recipient for the data object, <see langword="null"/> is returned instead.
		/// </summary>
		/// <param name="encryptionInfo">The encryption metadata, including encrypted data keys for a data object, for which a DataDecryptor should be returned.</param>
		/// <param name="keyDecryptor">A <see cref="KeyDecryptor"/> to use for decrypting the data key in <paramref name="encryptionInfo"/>.</param>
		/// <returns>A DataDecryptor for the data object associated with <paramref name="encryptionInfo"/>, or <see langword="null"/> if the data key could not be decrypted using <paramref name="keyDecryptor"/>.</returns>
		public static DataDecryptor? FromEncryptionInfo(EncryptionInfo encryptionInfo, IKeyDecryptor keyDecryptor) {
			var dataKey = keyDecryptor.DecryptKey(encryptionInfo);
			if (dataKey != null) {
				return new DataDecryptor(encryptionInfo.DataMode, encryptionInfo.IVs, dataKey);
			}
			else {
				return null;
			}
		}
	}
}
