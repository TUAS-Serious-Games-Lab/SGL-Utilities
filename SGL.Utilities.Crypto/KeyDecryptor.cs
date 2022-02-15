using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;

namespace SGL.Utilities.Crypto {
	/// <summary>
	/// Provides the functionality to decrypt data keys of data objects for a specific recipient that must be in the list of recipients of the data object.
	/// The encrypted data key is taken from the data object's metadata and decrypted using an authorized private key that must be supplied to the KeyDecyrptor.
	/// </summary>
	public class KeyDecryptor : IKeyDecryptor {
		private AsymmetricCipherKeyPair keyPair;
		private KeyId keyId;

		/// <summary>
		/// Constructs a KeyDecryptor using the given recipient key pair.
		/// </summary>
		/// <param name="keyPair">The recipient key pair to use for decryption.</param>
		public KeyDecryptor(AsymmetricCipherKeyPair keyPair) {
			this.keyPair = keyPair;
			keyId = KeyId.CalculateId(keyPair.Public);
		}

		/// <summary>
		/// Looks up the data key for this KeyDecryptors key pair in the data object metadata provided in <paramref name="encryptionInfo"/> and decrypts the data key if one is found.
		/// </summary>
		/// <param name="encryptionInfo">The metadata for a data object, for which the recipient handled by this KeyDecryptor is an authorized recipient.</param>
		/// <returns>The decrypted data key, or null if there is no data key </returns>
		/// <exception cref="ArgumentException">If the found data key in <see cref="EncryptionInfo.DataKeys"/> of <paramref name="encryptionInfo"/> uses an unsupported encryption mode.</exception>
		/// <exception cref="CryptoException">If the decryption itself fails.</exception>
		public byte[]? DecryptKey(EncryptionInfo encryptionInfo) {
			if (encryptionInfo.DataKeys.TryGetValue(keyId, out var dataKeyInfo)) {
				return DecryptKey(dataKeyInfo, encryptionInfo.SenderPublicKey);
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Decrypts the data key given in <paramref name="dataKeyInfo"/> and returns the raw data key.
		/// </summary>
		/// <param name="dataKeyInfo">The key information containing the encrypted data key.</param>
		/// <param name="sharedSenderPublicKey">
		/// If <paramref name="dataKeyInfo"/> uses <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> with a shared sender key, the shared public sender key must be supplied here.
		/// It is usually taken from <see cref="EncryptionInfo.SenderPublicKey"/>.
		/// </param>
		/// <returns>The decrypted data key.</returns>
		/// <exception cref="ArgumentException">If <paramref name="dataKeyInfo"/> uses an unsupported encryption mode.</exception>
		/// <exception cref="CryptoException">If the decryption itself fails.</exception>
		public byte[] DecryptKey(DataKeyInfo dataKeyInfo, byte[]? sharedSenderPublicKey) {
			switch (dataKeyInfo.Mode) {
				case KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM:
					return DecryptKeyEcdhAes(dataKeyInfo, sharedSenderPublicKey);
				case KeyEncryptionMode.RSA_PKCS1:
					return DecryptKeyRsa(dataKeyInfo);
				default:
					throw new ArgumentException("Unsupported key encryption mode.");
			}
		}

		private byte[] DecryptKeyRsa(DataKeyInfo dataKeyInfo) {
			var engine = new Pkcs1Encoding(new RsaEngine());
			engine.Init(forEncryption: false, keyPair.Private);
			return engine.ProcessBlock(dataKeyInfo.EncryptedKey, 0, dataKeyInfo.EncryptedKey.Length);
		}

		private byte[] DecryptKeyEcdhAes(DataKeyInfo dataKeyInfo, byte[]? sharedSenderPublicKey) {
			byte[]? senderPublicKeyEncoded = dataKeyInfo.SenderPublicKey ?? sharedSenderPublicKey;
			if (senderPublicKeyEncoded == null && senderPublicKeyEncoded == null) {
				throw new ArgumentException("Recipient-specific and shared sender public key must not both be missing");
			}
			ECPublicKeyParameters senderPublicKey = EcdhKdfHelper.DecodeEcPublicKey(senderPublicKeyEncoded);
			var ecdh = new ECDHBasicAgreement();
			ecdh.Init(keyPair.Private);
			var agreement = ecdh.CalculateAgreement(senderPublicKey);
			var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
			var keyParams = EcdhKdfHelper.DeriveKeyAndIV(agreement.ToByteArray(), senderPublicKeyEncoded);
			cipher.Init(forEncryption: false, keyParams);
			return cipher.DoFinal(dataKeyInfo.EncryptedKey);
		}
	}
}
