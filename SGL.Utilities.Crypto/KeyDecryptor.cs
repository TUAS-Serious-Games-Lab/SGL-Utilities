using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;

namespace SGL.Utilities.Crypto {
	public class KeyDecryptor {
		private AsymmetricCipherKeyPair keyPair;
		private KeyId keyId;

		public KeyDecryptor(AsymmetricCipherKeyPair keyPair) {
			this.keyPair = keyPair;
			keyId = KeyId.CalculateId(keyPair.Public);
		}

		public byte[]? DecryptKey(EncryptionInfo encryptionInfo) {
			if (encryptionInfo.DataKeys.TryGetValue(keyId, out var dataKeyInfo)) {
				return DecryptKey(dataKeyInfo, encryptionInfo.SenderPublicKey);
			}
			else {
				return null;
			}
		}

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
