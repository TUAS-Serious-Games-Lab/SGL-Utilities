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
	/// <summary>
	/// Provides the functionality to encrypt data keys of data objects for a list of recipients and to generate the metadata associating recipient key ids with the recipient's encrypted copy of the data key.
	/// </summary>
	public class KeyEncryptor {
		private readonly List<KeyValuePair<KeyId, AsymmetricKeyParameter>> trustedRecipients;
		private readonly SecureRandom random;
		private readonly bool useSharedSenderKeyPair;
		private readonly DerObjectIdentifier? ecSharedSenderKeyPairCurveName = null;

		/// <summary>
		/// Creates a KeyEncryptor that encrypts a copy of the data key for each of the recipients who have their public key (and its id) listed in <paramref name="trustedRecipients"/>.
		/// </summary>
		/// <param name="trustedRecipients">The list of authorized recipient public keys that shall be able to decrypt data objects the data keys of which are encrypted using <see cref="EncryptDataKey(byte[])"/>.</param>
		/// <param name="random">The random generator to generate sender key pairs for the <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> mode, which is used for recipients that use an Elliptic Curve key pair.</param>
		/// <param name="allowSharedSenderKeyPair">
		/// Specifies whether the <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> mode is allowed to use a shared sender key pair for all or most of the recipients with Elliptic Curve key pairs.
		/// If this is set to true, the named curve used by the most recipient key pairs is determined and a shared sender key pair is generated that will be used for all recipients that use that curve.
		/// The other Elliptic-Curve-using recipients will still use a specific sender key pair.
		/// If this is set to false, all Elliptic-Curve-using recipients will get their own specific sender key pair.
		/// </param>
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

		/// <summary>
		/// Encrypts the given data key for each of the recipients specified by their public key in <see cref="KeyEncryptor(List{KeyValuePair{KeyId, AsymmetricKeyParameter}}, SecureRandom, bool)"/>.
		/// </summary>
		/// <param name="dataKey">The plain data key.</param>
		/// <returns>
		/// A <see cref="Dictionary{KeyId, DataKeyInfo}"/> containing the encrypted data keys, encryption modes, and where needed a sender public key, for each recipient, indexed by the recipient's public key <see cref="KeyId"/>.
		/// Additionally, a shared sender public key in encoded form is returned if <c>allowSharedSenderKeyPair=true</c> was specified in <see cref="KeyEncryptor(List{KeyValuePair{KeyId, AsymmetricKeyParameter}}, SecureRandom, bool)"/>
		/// and at least one recipient uses an Elliptic Curve key pair with a named curve. This key needs to be stored together with the encrypted data keys in the metadata of the data object that is encrypted using <paramref name="dataKey"/>.
		/// </returns>
		public (Dictionary<KeyId, DataKeyInfo> dataKeys, byte[]? senderPubKey) EncryptDataKey(byte[] dataKey) {
			AsymmetricCipherKeyPair? sharedSenderKeyPair = null;
			byte[]? encodedSharedSenderPublicKey = null;
			if (useSharedSenderKeyPair && ecSharedSenderKeyPairCurveName != null) {
				sharedSenderKeyPair = GenerateECKeyPair(ecSharedSenderKeyPairCurveName);
				encodedSharedSenderPublicKey = EcdhKdfHelper.EncodeEcPublicKey((ECPublicKeyParameters)(sharedSenderKeyPair.Public));
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
			byte[] encodedSenderPublicKey = useSharedSenderKPHere ? encodedSharedSenderPublicKey! : EcdhKdfHelper.EncodeEcPublicKey((ECPublicKeyParameters)(senderKeyPair.Public));
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
