using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Internals;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents the private key of a <see cref="KeyPair"/>.
	/// This type of key needs to be kept secret and is used to decrypt data, encrypted using the matching public key, or to cryptographically sign data.
	/// </summary>
	public class PrivateKey {
		internal AsymmetricKeyParameter wrapped;

		internal PrivateKey(AsymmetricKeyParameter wrapped) {
			if (!wrapped.IsPrivate) throw new KeyException("Expecting a private key but got a public key");
			if (!IsValidWrappedType(wrapped)) throw new KeyException("Unexpected key type");
			this.wrapped = wrapped;
		}

		internal static bool IsValidWrappedType(AsymmetricKeyParameter wrapped) => TryGetKeyType(wrapped) != null;
		internal static KeyType? TryGetKeyType(AsymmetricKeyParameter wrapped) => wrapped switch {
			RsaPrivateCrtKeyParameters rsa => KeyType.RSA,
			ECPrivateKeyParameters => KeyType.EllipticCurves,
			_ => null
		};

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is PrivateKey privateKey && wrapped.Equals(privateKey.wrapped);
		/// <inheritdoc/>
		public override int GetHashCode() => wrapped.GetHashCode();
		/// <inheritdoc/>
		public override string? ToString() => wrapped.ToString();

		/// <summary>
		/// Returns the type of the key.
		/// </summary>
		public KeyType Type => TryGetKeyType(wrapped) ?? throw new KeyException("Unexpected key type");

		/// <summary>
		/// Loads one private key from the PEM-encoded data in <paramref name="reader"/>.
		/// If a full key pair is read from the PEM data, its private key will be used.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> containing at least one PEM-encoded key pair or private key.</param>
		/// <param name="passwordGetter">A function object that is called to obtain the password used for decrypting the private key.</param>
		/// <returns>The loaded private key.</returns>
		public static PrivateKey LoadOneFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadPrivateKey(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		/// <summary>
		/// Loads all private keys from the PEM-encoded data in <paramref name="reader"/>.
		/// If a full key pair is read from the PEM data, its private key will be used.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> containing PEM-encoded private keys.</param>
		/// <param name="passwordGetter">A function object that is called to obtain the password used for decrypting the private keys.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> over all private keys loaded from <paramref name="reader"/>.</returns>
		public static IEnumerable<PrivateKey> LoadAllFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadPrivateKeys(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		/// <summary>
		/// Writes the private key to <paramref name="writer"/> in PEM-encoded form.
		/// </summary>
		/// <param name="writer">The writer to which the private key should be written.</param>
		/// <param name="encMode">The encryption mode to use for the private key.</param>
		/// <param name="password">The password to use for encryption of the private key.</param>
		/// <param name="random">A random generator to be used for generating an initialization vector and password salt.</param>
		public void StoreToPem(TextWriter writer, PemEncryptionMode encMode, char[] password, RandomGenerator random) => PemHelper.Write(writer, this, encMode, password, random);

		/// <summary>
		/// Derives the matching public key for this private key.
		/// </summary>
		/// <returns>The matching public key.</returns>
		/// <exception cref="KeyException">If public key derivation is not implemented for this key type yet.</exception>
		public PublicKey DerivePublicKey() {
			if (wrapped is RsaPrivateCrtKeyParameters rsa) {
				// We have a RSA private key, extract the public key from the private key.
				// The public key consists only of two components, that are also present in the private key: The modulus and the public exponent.
				return new PublicKey(new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent));
			}
			else if (wrapped is ECPrivateKeyParameters ecPriv) {
				// We have an Elliptic Curves private key, derive the public key from the private key.
				// The public key is a point on the curve, determined by multiplying the generator point (which is part of the curve domain definition)
				// by the integer that forms the private key.
				var q = ecPriv.Parameters.G.Multiply(ecPriv.D);
				return new PublicKey(ecPriv.PublicKeyParamSet != null ? new ECPublicKeyParameters(ecPriv.AlgorithmName, q, ecPriv.PublicKeyParamSet) : new ECPublicKeyParameters(q, ecPriv.Parameters));
			}
			else {
				throw new KeyException("This private key type does not support deriving the public key (yet).");
			}
		}
		/// <summary>
		/// Derives the matching public key for this private key (as is done by <see cref="DerivePublicKey"/>) and combines them into a <see cref="KeyPair"/>.
		/// </summary>
		/// <returns>A key pair consisting of this private key and its matching public key.</returns>
		public KeyPair DeriveKeyPair() => new KeyPair(DerivePublicKey(), this);
	}
}
