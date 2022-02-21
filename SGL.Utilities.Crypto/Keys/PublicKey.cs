﻿using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using SGL.Utilities.Crypto.Internals;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents the public key of a <see cref="KeyPair"/>.
	/// This type of key can be shared openly an is used to encrypt data such that it can only be decrypted by the matching private key.
	/// Public keys are also used to verify cryptographic signatures made using the matching private key.
	/// </summary>
	public class PublicKey {
		internal AsymmetricKeyParameter wrapped;

		internal PublicKey(AsymmetricKeyParameter wrapped) {
			if (wrapped.IsPrivate) throw new KeyException("Expecting a public key but got a private key");
			if (!IsValidWrappedType(wrapped)) throw new KeyException("Unexpected key type");
			this.wrapped = wrapped;
		}

		internal static bool IsValidWrappedType(AsymmetricKeyParameter wrapped) => TryGetKeyType(wrapped) != null;
		internal static KeyType? TryGetKeyType(AsymmetricKeyParameter wrapped) => wrapped switch {
			RsaKeyParameters rsa => KeyType.RSA,
			ECPublicKeyParameters => KeyType.EllipticCurves,
			_ => null
		};

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is PublicKey publicKey && wrapped.Equals(publicKey.wrapped);
		/// <inheritdoc/>
		public override int GetHashCode() => wrapped.GetHashCode();
		/// <inheritdoc/>
		public override string? ToString() => wrapped.ToString();

		/// <summary>
		/// Returns the type of the key.
		/// </summary>
		public KeyType Type => TryGetKeyType(wrapped) ?? throw new KeyException("Unexpected key type");
		/// <summary>
		/// Loads one public key from the PEM-encoded data in <paramref name="reader"/>.
		/// If a full key pair is read from the PEM data, its public key will be used.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> containing at least one PEM-encoded key pair or public key.</param>
		/// <returns>The loaded public key.</returns>
		public static PublicKey LoadOneFromPem(TextReader reader) => PemHelper.LoadPublicKey(reader);
		/// <summary>
		/// Loads all public keys from the PEM-encoded data in <paramref name="reader"/>.
		/// If a full key pair is read from the PEM data, its public key will be used.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> containing PEM-encoded public keys.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> over all public keys loaded from <paramref name="reader"/>.</returns>
		public static IEnumerable<PublicKey> LoadAllFromPem(TextReader reader) => PemHelper.LoadPublicKeys(reader);
		/// <summary>
		/// Writes the public key to <paramref name="writer"/> in PEM-encoded form.
		/// </summary>
		/// <param name="writer">The writer to which the public key should be written.</param>
		public void StoreToPem(TextWriter writer) => PemHelper.Write(writer, this);
	}
}
