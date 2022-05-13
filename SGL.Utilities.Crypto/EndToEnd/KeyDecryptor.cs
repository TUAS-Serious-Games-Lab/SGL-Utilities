using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System;

namespace SGL.Utilities.Crypto.EndToEnd {
	/// <summary>
	/// Provides the functionality to decrypt data keys of data objects for a specific recipient that must be in the list of recipients of the data object.
	/// The encrypted data key is taken from the data object's metadata and decrypted using an authorized private key that must be supplied to the KeyDecyrptor.
	/// </summary>
	public class KeyDecryptor : IKeyDecryptor {
		private KeyPair keyPair;
		private KeyId keyId;

		/// <summary>
		/// Constructs a KeyDecryptor using the given recipient key pair.
		/// </summary>
		/// <param name="keyPair">The recipient key pair to use for decryption.</param>
		public KeyDecryptor(KeyPair keyPair) {
			this.keyPair = keyPair;
			keyId = keyPair.Public.CalculateId();
		}

		/// <summary>
		/// Looks up the data key for this KeyDecryptors key pair in the data object metadata provided in <paramref name="encryptionInfo"/> and decrypts the data key if one is found.
		/// </summary>
		/// <param name="encryptionInfo">The metadata for a data object, for which the recipient handled by this KeyDecryptor is an authorized recipient.</param>
		/// <returns>The decrypted data key, or null if there is no data key </returns>
		/// <exception cref="DecryptionException">If the found data key in <see cref="EncryptionInfo.DataKeys"/> of <paramref name="encryptionInfo"/> uses an unsupported encryption mode or if the decryption itself fails.</exception>
		public byte[]? DecryptKey(EncryptionInfo encryptionInfo) {
			if (encryptionInfo.DataKeys.TryGetValue(keyId, out var dataKeyInfo)) {
				return DecryptKey(dataKeyInfo, encryptionInfo.MessagePublicKey);
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Decrypts the data key given in <paramref name="dataKeyInfo"/> and returns the raw data key.
		/// </summary>
		/// <param name="dataKeyInfo">The key information containing the encrypted data key.</param>
		/// <param name="sharedMessagePublicKey">
		/// If <paramref name="dataKeyInfo"/> uses <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> with a shared message key, the shared public message key must be supplied here.
		/// It is usually taken from <see cref="EncryptionInfo.MessagePublicKey"/>.
		/// </param>
		/// <returns>The decrypted data key.</returns>
		/// <exception cref="DecryptionException">If <paramref name="dataKeyInfo"/> uses an unsupported encryption mode or if the decryption itself fails.</exception>
		public byte[] DecryptKey(DataKeyInfo dataKeyInfo, byte[]? sharedMessagePublicKey) {
			switch (dataKeyInfo.Mode) {
				case KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM:
					return DecryptKeyEcdhAes(dataKeyInfo, sharedMessagePublicKey);
				case KeyEncryptionMode.RSA_PKCS1:
					return DecryptKeyRsa(dataKeyInfo);
				default:
					throw new DecryptionException("Unsupported key encryption mode.");
			}
		}

		private byte[] DecryptKeyRsa(DataKeyInfo dataKeyInfo) {
			try {
				var engine = new Pkcs1Encoding(new RsaEngine());
				engine.Init(forEncryption: false, keyPair.Private.wrapped);
				return engine.ProcessBlock(dataKeyInfo.EncryptedKey, 0, dataKeyInfo.EncryptedKey.Length);
			}
			catch (Exception ex) {
				throw new DecryptionException("Couldn't decrypt the data key using RSA with given private key.", ex);
			}
		}

		private byte[] DecryptKeyEcdhAes(DataKeyInfo dataKeyInfo, byte[]? sharedMessagePublicKey) {
			byte[]? messagePublicKeyEncoded = dataKeyInfo.MessagePublicKey ?? sharedMessagePublicKey;
			if (messagePublicKeyEncoded == null && messagePublicKeyEncoded == null) {
				throw new KeyException("Recipient-specific and shared message public key must not both be missing");
			}
			ECPublicKeyParameters messagePublicKey = EcdhKdfHelper.DecodeEcPublicKey(messagePublicKeyEncoded);
			BigInteger agreement;
			try {
				var ecdh = new ECDHBasicAgreement();
				ecdh.Init(keyPair.Private.wrapped);
				agreement = ecdh.CalculateAgreement(messagePublicKey);
			}
			catch (Exception ex) {
				throw new DecryptionException("Failed to calculate ECDH agreement.", ex);
			}
			var keyParams = EcdhKdfHelper.DeriveKeyAndIV(agreement.ToByteArray(), messagePublicKeyEncoded);
			try {
				var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
				cipher.Init(forEncryption: false, keyParams);
				return cipher.DoFinal(dataKeyInfo.EncryptedKey);
			}
			catch (Exception ex) {
				throw new DecryptionException("Failed to decrypt data key.", ex);
			}
		}
	}
}
