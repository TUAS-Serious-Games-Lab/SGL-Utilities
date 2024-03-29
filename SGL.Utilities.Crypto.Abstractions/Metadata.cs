﻿using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SGL.Utilities.Crypto.EndToEnd {

	/// <summary>
	/// Describes the encryption mode used for encrypting the actual contents of a data object, e.g. a message or file.
	/// </summary>
	/// <remarks>
	/// SGL.Utilities.Crypto usually uses a two stage system, where the contents are encrypted with a mode described by
	/// <see cref="DataEncryptionMode"/> using a random key which is then encrypted for the desired recipients with their
	/// respective key using a mode described by <see cref="KeyEncryptionMode"/>.
	/// </remarks>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum DataEncryptionMode {
		/// <summary>
		/// Indicates that the contents are encrypted using Advanced Encryption Standard (AES) with the 'Counter with CBC-MAC' (CCM) mode of operation and a 256-bit key.
		/// </summary>
		AES_256_CCM = 1,
		/// <summary>
		/// Indicates that the contents are not encrypted at all and thus, the whole encryption mechanism is not used for the object.
		/// Obviously, this should only be used for special purposes where the contents are not private.
		/// Use-cases include publically available objects in a format that needs to also support encryption for private objects which use the same format,
		/// or objects used in testing code that is not concerned with the encryption aspect.
		/// </summary>
		Unencrypted = 0xFFFF
	}

	/// <summary>
	/// Describes the encryption mode used for encrypting the data keys for the desired recipients.
	/// </summary>
	/// <remarks>
	/// SGL.Utilities.Crypto usually uses a two stage system, where the contents are encrypted with a mode described by
	/// <see cref="DataEncryptionMode"/> using a random key which is then encrypted for the desired recipients with their
	/// respective key using a mode described by <see cref="KeyEncryptionMode"/>.
	/// </remarks>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum KeyEncryptionMode {
		/// <summary>
		/// Indicates that the data keys are encrypted using RSA with the PKCS1 encoding.
		/// </summary>
		RSA_PKCS1 = 1,
		/// <summary>
		/// Indicates that the data keys are encrypted using the following procedure:
		/// <list type="number">
		/// <item><description>Generate an Elliptic Curves keypair for the message.</description></item>
		/// <item><description>Perform Elliptic Curve Diffie Hellman with the message private key and the recipients public key.</description></item>
		/// <item><description>Derive a 256-bit key and an initialization vector from the agreement value and the message public key using Key Derivation Function 2 (KDF2).</description></item>
		/// <item><description>Encrypt the data key using Advanced Encryption Standard (AES) with the 'Counter with CBC-MAC' (CCM) mode of operation and with the derived key.</description></item>
		/// </list>
		/// The decryption procedure works the same way, except that in the second step, the keys are replaced with their opposite (i.e. message public key and recipient private key).
		/// This mode requires less key material overhead.
		/// </summary>
		ECDH_KDF2_SHA256_AES_256_CCM = 2
	}

	/// <summary>
	/// Encapsulates the encryption-related metadata abount a data object.
	/// </summary>
	public class EncryptionInfo {
		/// <summary>
		/// Represents the encryption mode used for encrypting the data of the object.
		/// </summary>
		public DataEncryptionMode DataMode { get; set; }
		/// <summary>
		/// Represents the initialization vectors (IV) used for the encryption of the data, one for each stream in the data object.
		/// </summary>
		public List<byte[]> IVs { get; set; } = new();

		/// <summary>
		/// Contains the encrypted data keys for the recipients, indexed by the key id of the recipients.
		/// </summary>
#if !NET6_0_OR_GREATER
		[JsonConverter(typeof(KeyIdDictionaryJsonConverter<DataKeyInfo>))]
#endif
		public Dictionary<KeyId, DataKeyInfo> DataKeys { get; set; } = new Dictionary<KeyId, DataKeyInfo>();
		/// <summary>
		/// When <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> is used with a shared message key pair, this property holds an encoded version of the shared public key.
		/// </summary>
		public byte[]? MessagePublicKey { get; set; }

		/// <summary>
		/// Creates an unencrypted instance of <see cref="EncryptionInfo"/> for the given number of streams.
		/// Obviously, this should only be used for special purposes where the contents are not private.
		/// Use-cases include publically available objects in a format that needs to also support encryption for private objects which use the same format,
		/// or objects used in testing code that is not concerned with the encryption aspect.
		/// </summary>
		/// <param name="numberOfStreams">
		/// The number of streams, the data object consists of.
		/// This determines the number of (empty) entries in <see cref="IVs"/>,
		/// which are created to keep the invariant of having the a matching number of entries for the stream number,
		/// even though <see cref="IVs"/> are not used for unencrypted objects.</param>
		/// <returns>An 'empty' <see cref="EncryptionInfo"/> object that indicates the associated object as unencrypted.</returns>
		public static EncryptionInfo CreateUnencrypted(int numberOfStreams = 1) {
			return new EncryptionInfo {
				DataMode = DataEncryptionMode.Unencrypted,
				DataKeys = new Dictionary<KeyId, DataKeyInfo> { },
				MessagePublicKey = null,
				IVs = Enumerable.Repeat(Array.Empty<byte>(), numberOfStreams).ToList()
			};
		}
	}

	/// <summary>
	/// Encapsulates the recipient-specific encryption metadata for a data object.
	/// Thus, an object of this class for each authorized recipient is contained in an <see cref="EncryptionInfo"/> object.
	/// </summary>
	public class DataKeyInfo {
		/// <summary>
		/// The key encryption mode used for this recipient. Usually the chosen mode depends on the type of the public key that the recipient provided.
		/// </summary>
		public KeyEncryptionMode Mode { get; set; }
		/// <summary>
		/// The randomly generated data key used for the content ecryption, encrypting for the recipients key using the mode indicated by <see cref="Mode"/>.
		/// </summary>
		public byte[] EncryptedKey { get; set; } = Array.Empty<byte>();
		/// <summary>
		/// When <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> is used with a recipient-specific message key pair,
		/// this property holds an encoded version of the public key.
		/// Recipient-specific message key pairs are used when either a shared key is not allowed by the sender's policy,
		/// or when a recipient needs a key that deviates from the shared key. The latter happens when not all recipients use the same named Elliptic Curve
		/// or when the recipient uses excplicit Elliptic Curve parameteres instead of a named curve.
		/// </summary>
		/// <remarks>
		/// Using recipient key pairs with excplicit Elliptic Curve parameteres instead of a named curve should be avoided,
		/// beause it implies that the message key pair will also use explicit parameters.
		/// This will make the message key pair much larger and prevents a shared message key pair.
		/// Thus, it causes the metadata to significantly grow in size.
		/// </remarks>
		public byte[]? MessagePublicKey { get; set; }
	}
}
