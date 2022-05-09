using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SGL.Utilities.Crypto.EndToEnd {
	/// <summary>
	/// Provides the functionality to encrypt the content of a data object using a randomly generated data key for each DataEncryptor object.
	/// The data key can then be encrypted using a <see cref="KeyEncryptor"/> to obtain an <see cref="EncryptionInfo"/> object representing the key material for the data object.
	/// A data object can consist of multiple streams that are encrypted with the same data key, e.g. a message with attachments where the message text and the attachments each have their own stream.
	/// These streams are identified using an index within the data object and have their own initialization vector generated for each stream.
	/// </summary>
	public class DataEncryptor {
		private List<byte[]> ivs;
		private byte[] dataKey;
		private readonly DataEncryptionMode dataMode;

		/// <summary>
		/// Created a DataEncryptor for a data object with given number of streams.
		/// The data key and an initialization vector for each stream is generated upon construction from <paramref name="random"/>.
		/// </summary>
		/// <param name="random">The random generator to use for generating the data key and the initialization vector.</param>
		/// <param name="numberOfStreams">The number of streams, the data object consists of. This determines the number of initialization vectors that will be generated.</param>
		/// <param name="dataEncryptionMode">The mode used for encrypting the data.</param>
		public DataEncryptor(RandomGenerator random, int numberOfStreams = 1, DataEncryptionMode dataEncryptionMode = DataEncryptionMode.AES_256_CCM) {
			this.dataMode = dataEncryptionMode;
			dataKey = random.GetBytes(32);
			ivs = Enumerable.Range(0, numberOfStreams).Select(_ => {
				var iv = random.GetBytes(GetIvLength(dataEncryptionMode));
				return iv;
			}).ToList();
		}

		private int GetIvLength(DataEncryptionMode dataEncryptionMode) => dataEncryptionMode switch {
			DataEncryptionMode.AES_256_CCM => 7,
			_ => throw new CryptographyException($"Unsupported encryption mode {dataEncryptionMode}."),
		};

		private IBufferedCipher GetCipher(int streamIndex) {
			IBufferedCipher cipher = dataMode switch {
				DataEncryptionMode.AES_256_CCM => new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine())),
				_ => throw new CryptographyException($"Unsupported encryption mode {dataMode}.")
			};
			var keyParams = new ParametersWithIV(new KeyParameter(dataKey), ivs[streamIndex]);
			cipher.Init(forEncryption: true, keyParams);
			return cipher;
		}

		/// <summary>
		/// Opens a <see cref="CipherStream"/> backed by <paramref name="inputStream"/> using the data key of the encryptor and the initialization vector of the stream with the index given in <paramref name="streamIndex"/>.
		/// The <see cref="CipherStream"/> is setup to encrypt the data read from it.
		/// </summary>
		/// <param name="inputStream">The backing stream for the encrypting stream.</param>
		/// <param name="streamIndex">The logical index of the stream within the data object.</param>
		/// <returns>A stream that reads data from <paramref name="inputStream"/>, encrypts it and returns the encrypted data to the reader.</returns>
		public CipherStream OpenEncryptionReadStream(Stream inputStream, int streamIndex) {
			try {
				var cipher = GetCipher(streamIndex);
				return new CipherStream(new Org.BouncyCastle.Crypto.IO.CipherStream(inputStream, cipher, null), CipherStreamOperationMode.EncryptingRead);
			}
			catch (Exception ex) {
				throw new EncryptionException("Failed to open encryption stream.", ex);
			}
		}

		/// <summary>
		/// Opens a <see cref="CipherStream"/> backed by <paramref name="outputStream"/> using the data key of the encryptor and the initialization vector of the stream with the index given in <paramref name="streamIndex"/>.
		/// The <see cref="CipherStream"/> is setup to encrypt the data written to it.
		/// </summary>
		/// <param name="outputStream">The backing stream for the encrypting stream.</param>
		/// <param name="streamIndex">The logical index of the stream within the data object.</param>
		/// <returns>A stream that encrypts data written to it and then writes the encrypted data to <paramref name="outputStream"/>.</returns>
		public CipherStream OpenEncryptionWriteStream(Stream outputStream, int streamIndex) {
			try {
				var cipher = GetCipher(streamIndex);
				return new CipherStream(new Org.BouncyCastle.Crypto.IO.CipherStream(outputStream, null, cipher), CipherStreamOperationMode.EncryptingWrite);
			}
			catch (Exception ex) {
				throw new EncryptionException("Failed to open encryption stream.", ex);
			}
		}

		/// <summary>
		/// Directly encrypts the given <paramref name="clearTextContent"/> with the data key of this <see cref="DataEncryptor"/>
		/// and the initialization vector associated with the given <paramref name="streamIndex"/>.
		/// This operation is logically equivalent to opening a stream using <see cref="OpenEncryptionWriteStream(Stream, int)"/> on an underlying <see cref="MemoryStream"/>,
		/// writing <paramref name="clearTextContent"/> to the stream and then returning <see cref="MemoryStream.ToArray"/> of the underlying stream.
		/// It is however more convenient to use and can avoid the overhead of creating the streams.
		/// </summary>
		/// <param name="clearTextContent">The content to encrypt.</param>
		/// <param name="streamIndex">The logical stream in the data object, the content of which is represented by the given bytes.</param>
		/// <returns>The encrypted content.</returns>
		public byte[] EncryptData(byte[] clearTextContent, int streamIndex) {
			try {
				var cipher = GetCipher(streamIndex);
				return cipher.DoFinal(clearTextContent);
			}
			catch (Exception ex) {
				throw new DecryptionException("Failed to encrypt data.", ex);
			}
		}

		/// <summary>
		/// Encrypts the data key using <paramref name="keyEncryptor"/> and returns an <see cref="EncryptionInfo"/> object containing the initialization vectors of the streams in the data object and the encrypted copies of the data keys for the different recipients as setup in <paramref name="keyEncryptor"/>.
		/// </summary>
		/// <param name="keyEncryptor">The <see cref="KeyEncryptor"/> to encrypt the data key with.</param>
		/// <returns>The key material an metadata for the data object.</returns>
		public EncryptionInfo GenerateEncryptionInfo(IKeyEncryptor keyEncryptor) {
			EncryptionInfo result = new EncryptionInfo();
			result.DataMode = dataMode;
			result.IVs = ivs;
			(result.DataKeys, result.MessagePublicKey) = keyEncryptor.EncryptDataKey(dataKey);
			return result;
		}
	}
}
