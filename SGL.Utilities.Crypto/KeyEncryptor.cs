using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SGL.Utilities.Crypto {
	public class KeyEncryptor {
		private readonly List<KeyValuePair<KeyId, AsymmetricKeyParameter>> trustedRecipients;
		private readonly SecureRandom random;
		private readonly bool useSharedSenderKeyPair;
		private readonly DerObjectIdentifier? ecSharedSenderKeyPairCurveName = null;

		public KeyEncryptor(List<KeyValuePair<KeyId, AsymmetricKeyParameter>> trustedRecipients, SecureRandom random, bool allowSharedSenderKeyPair = false) {
			this.trustedRecipients = trustedRecipients;
			this.random = random;
			if (allowSharedSenderKeyPair) {
				// Determine named curve that has the most recipients using it, to handle all those recipients using a shared sender EC public key.
				var ecRecipientKeyCurveNames = trustedRecipients.Select(tr => tr.Value)
					.OfType<ECPublicKeyParameters>()
					.Where(pk => pk.PublicKeyParamSet != null)
					.GroupBy(pk => pk.PublicKeyParamSet.Id)
					.OrderByDescending(grp => grp.Count())
					.ToList();
				if (ecRecipientKeyCurveNames.Count() > 0) {
					ecSharedSenderKeyPairCurveName = ecRecipientKeyCurveNames.First().First().PublicKeyParamSet;
					useSharedSenderKeyPair = true;
				}
				else {
					useSharedSenderKeyPair = false;
				}
			}
			else {
				useSharedSenderKeyPair = false;
			}
		}

		public (Dictionary<KeyId, DataKeyInfo> dataKeys, byte[]? senderPubKey) EncryptDataKey(byte[] dataKey) {
			AsymmetricCipherKeyPair? sharedSenderKeyPair = null;
			byte[]? encodedSharedSenderPublicKey = null;
			if (useSharedSenderKeyPair && ecSharedSenderKeyPairCurveName != null) {
				sharedSenderKeyPair = GenerateECKeyPair(ecSharedSenderKeyPairCurveName);
				encodedSharedSenderPublicKey = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(sharedSenderKeyPair.Public).GetEncoded();
			}
			return (trustedRecipients.ToDictionary(kv => kv.Key, kv => EncryptDataKey(kv.Value, dataKey, sharedSenderKeyPair, encodedSharedSenderPublicKey)), encodedSharedSenderPublicKey);
		}

		private DataKeyInfo EncryptDataKey(AsymmetricKeyParameter recipientKey, byte[] dataKey, AsymmetricCipherKeyPair? sharedSenderKeyPair, byte[]? encodedSharedSenderPublicKey) {
			switch (recipientKey) {
				case RsaKeyParameters rsa when !rsa.IsPrivate:
					return EncryptDataKeyRsa(rsa, dataKey);
				case ECPublicKeyParameters ec:
					return EncryptDataKeyEcdhAes(ec, dataKey, sharedSenderKeyPair, encodedSharedSenderPublicKey);
				default:
					throw new ArgumentException($"Unsupported recipient key type {recipientKey.GetType().FullName}.");
			}
		}
		private DataKeyInfo EncryptDataKeyRsa(RsaKeyParameters recipientKey, byte[] dataKey) {
			var rsa = new Pkcs1Encoding(new RsaEngine());
			rsa.Init(forEncryption: true, recipientKey);
			var encryptedDataKey = rsa.ProcessBlock(dataKey, 0, dataKey.Length);
			return new DataKeyInfo() { Mode = KeyEncryptionMode.RSA_PKCS1, EncryptedKey = encryptedDataKey };
		}
		private DataKeyInfo EncryptDataKeyEcdhAes(ECPublicKeyParameters recipientKey, byte[] dataKey, AsymmetricCipherKeyPair? sharedSenderKeyPair, byte[]? encodedSharedSenderPublicKey) {
			bool useSharedSenderKPHere = sharedSenderKeyPair != null && sharedSenderKeyPair.Private is ECPrivateKeyParameters sharedEC &&
							sharedEC.PublicKeyParamSet != null && recipientKey.PublicKeyParamSet != null && sharedEC.PublicKeyParamSet.Id == recipientKey.PublicKeyParamSet.Id;
			AsymmetricCipherKeyPair senderKeyPair = useSharedSenderKPHere ? sharedSenderKeyPair! : GenerateECKeyPair(recipientKey.PublicKeyParamSet, recipientKey.Parameters);
			byte[] encodedSenderPublicKey = useSharedSenderKPHere ? encodedSharedSenderPublicKey! : SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(senderKeyPair.Public).GetEncoded();
			var ecdh = new ECDHBasicAgreement();
			ecdh.Init(senderKeyPair.Private);
			var agreement = ecdh.CalculateAgreement(recipientKey);

			var cipher = new BufferedAeadBlockCipher(new CcmBlockCipher(new AesEngine()));
			var keyParams = EcdhKdfHelper.DeriveKeyAndIV(agreement.ToByteArray(), encodedSenderPublicKey);
			cipher.Init(forEncryption: true, keyParams);
			var encryptedDataKey = cipher.DoFinal(dataKey);

			return new DataKeyInfo() {
				Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
				EncryptedKey = encryptedDataKey,
				SenderPublicKey = useSharedSenderKPHere ? null : encodedSenderPublicKey
			};
		}

		private AsymmetricCipherKeyPair GenerateECKeyPair(DerObjectIdentifier? curveName, ECDomainParameters? domainParams = null) {
			if (curveName == null && domainParams == null) {
				throw new ArgumentNullException(nameof(curveName) + " and " + nameof(domainParams));
			}
			var ecKeyGenParams = curveName != null ?
							new ECKeyGenerationParameters(curveName, random) :
							new ECKeyGenerationParameters(domainParams, random);
			var ecKeyGen = new ECKeyPairGenerator();
			ecKeyGen.Init(ecKeyGenParams);
			return ecKeyGen.GenerateKeyPair();
		}
	}
}
